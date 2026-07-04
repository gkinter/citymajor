#version 330 core

in vec2 v_TexCoord;

uniform sampler2D u_sceneTexture;
uniform vec2 u_resolution;

// Effect toggles
uniform bool u_bloomEnabled;
uniform float u_bloomIntensity;
uniform bool u_vignetteEnabled;
uniform float u_vignetteStrength;
uniform bool u_crtEnabled;
uniform float u_crtScanlineIntensity;

// Day/night cycle
uniform float u_nightStrength; // 0 = day, 1 = night

// Tilt-shift depth of field
uniform bool u_tiltShiftEnabled;
uniform float u_tiltShiftStrength;   // 0-1, overall effect intensity
uniform float u_focusBandCenter;     // 0-1, vertical center of focus band (default 0.5)
uniform float u_focusBandWidth;      // 0-1, width of focus band (default 0.4)

// Atmospheric fog / depth fade
uniform bool u_fogEnabled;
uniform vec3 u_fogColor;             // Fog tint (follows ambient color / time of day)

// Bloom color tint (warm for window glow, cool for moonlight)
uniform vec3 u_bloomTint;            // RGB tint applied to bloom

out vec4 FragColor;

// ============================================================================
// Gaussian weights for 5-tap 1D kernel (sigma ~1.4)
// ============================================================================
const float gaussWeight[5] = float[](0.06136, 0.24477, 0.38774, 0.24477, 0.06136);

// ============================================================================
// Enhanced bloom: 2-pass approximation via separable Gaussian
// Uses a single-pass approximation (cross-shaped sample pattern) for
// compatibility without needing multiple FBOs. Threshold + tint + night scaling.
// ============================================================================
vec3 enhancedBloom(vec2 uv) {
    vec2 texelSize = 1.0 / u_resolution;
    vec3 color = vec3(0.0);
    float totalWeight = 0.0;

    // Horizontal pass
    for (int i = -2; i <= 2; i++) {
        vec2 offset = vec2(float(i) * texelSize.x * 2.0, 0.0);
        vec3 s = texture(u_sceneTexture, uv + offset).rgb;
        float brightness = dot(s, vec3(0.2126, 0.7152, 0.0722));

        // Threshold: only bright pixels contribute (0.8 cutoff with soft knee)
        float contribution = smoothstep(0.7, 0.9, brightness);
        float w = gaussWeight[i + 2] * contribution;
        color += s * w;
        totalWeight += w;
    }

    // Vertical pass (cross pattern approximation)
    for (int i = -2; i <= 2; i++) {
        if (i == 0) continue; // Skip center (already counted in horizontal)
        vec2 offset = vec2(0.0, float(i) * texelSize.y * 2.0);
        vec3 s = texture(u_sceneTexture, uv + offset).rgb;
        float brightness = dot(s, vec3(0.2126, 0.7152, 0.0722));
        float contribution = smoothstep(0.7, 0.9, brightness);
        float w = gaussWeight[i + 2] * contribution;
        color += s * w;
        totalWeight += w;
    }

    if (totalWeight > 0.0) {
        color /= totalWeight;
    }

    // Apply bloom tint (warm orange for window glow, cool blue for moonlight)
    color *= u_bloomTint;

    // Bloom is stronger at night (glowing windows, moonlight reflections)
    float nightBoost = 1.0 + u_nightStrength * 1.5;
    color *= nightBoost;

    return color;
}

// ============================================================================
// Vignette darkening at screen edges
// ============================================================================
float vignette(vec2 uv) {
    vec2 center = uv - 0.5;
    float dist = dot(center, center);
    return 1.0 - dist * u_vignetteStrength * 4.0;
}

// ============================================================================
// CRT scanline effect
// ============================================================================
float scanline(vec2 uv) {
    float scanPos = uv.y * u_resolution.y;
    return 1.0 - u_crtScanlineIntensity * (0.5 + 0.5 * sin(scanPos * 3.14159 * 2.0));
}

// ============================================================================
// Tilt-shift depth of field (miniature / diorama effect)
// Blurs above and below a horizontal focus band in the middle of the screen.
// ============================================================================
vec3 tiltShift(vec2 uv, vec3 sharpColor) {
    // Distance from the focus band center
    float distFromCenter = abs(uv.y - u_focusBandCenter);

    // How far outside the focus band (0 = inside, >0 = outside)
    float halfBand = u_focusBandWidth * 0.5;
    float blurAmount = smoothstep(halfBand, halfBand + 0.15, distFromCenter);
    blurAmount *= u_tiltShiftStrength;

    if (blurAmount < 0.01) {
        return sharpColor;
    }

    // Progressive Gaussian blur: radius scales with distance from focus band
    // Max radius: 4 pixels (subtle, not overwhelming)
    float maxRadius = 4.0;
    float radius = blurAmount * maxRadius;
    vec2 texelSize = 1.0 / u_resolution;

    vec3 blurred = vec3(0.0);
    float totalWeight = 0.0;

    // 5-tap separable blur (horizontal + vertical cross pattern)
    for (int i = -2; i <= 2; i++) {
        float w = gaussWeight[i + 2];

        // Horizontal
        vec2 hOffset = vec2(float(i) * texelSize.x * radius, 0.0);
        blurred += texture(u_sceneTexture, uv + hOffset).rgb * w;

        // Vertical (skip center to avoid double-counting)
        if (i != 0) {
            vec2 vOffset = vec2(0.0, float(i) * texelSize.y * radius);
            blurred += texture(u_sceneTexture, uv + vOffset).rgb * w;
            totalWeight += w;
        }
    }

    totalWeight += 1.0; // Account for horizontal sum = 1.0
    blurred /= totalWeight;

    return mix(sharpColor, blurred, blurAmount);
}

// ============================================================================
// Atmospheric fog / depth fade
// Distant tiles (higher on screen in isometric view = further from camera)
// get slightly desaturated and haze-tinted.
// ============================================================================
vec3 atmosphericFog(vec3 color, vec2 uv) {
    // In isometric view, objects further from camera are higher on screen (lower uv.y)
    // and at the edges horizontally. Use distance from screen center-bottom as depth proxy.
    float verticalDepth = 1.0 - uv.y; // 0 at bottom (near), 1 at top (far)

    // Also factor horizontal distance from center for edge haze
    float horizDist = abs(uv.x - 0.5) * 0.5;

    // Combined depth factor with max 20% effect
    float depthFactor = clamp((verticalDepth + horizDist) * 0.25, 0.0, 0.2);

    // Desaturate: convert to luminance and blend back
    float luminance = dot(color, vec3(0.2126, 0.7152, 0.0722));
    vec3 desaturated = mix(color, vec3(luminance), depthFactor);

    // Tint with fog color (follows time-of-day ambient)
    vec3 foggedColor = mix(desaturated, u_fogColor, depthFactor * 0.5);

    return foggedColor;
}

// ============================================================================
// Main
// ============================================================================
void main() {
    vec3 color = texture(u_sceneTexture, v_TexCoord).rgb;

    // Tilt-shift (apply before other effects for correct blur source)
    if (u_tiltShiftEnabled) {
        color = tiltShift(v_TexCoord, color);
    }

    // Bloom
    if (u_bloomEnabled) {
        vec3 bloomColor = enhancedBloom(v_TexCoord);
        color += bloomColor * u_bloomIntensity;
    }

    // Atmospheric fog
    if (u_fogEnabled) {
        color = atmosphericFog(color, v_TexCoord);
    }

    // Vignette
    if (u_vignetteEnabled) {
        color *= vignette(v_TexCoord);
    }

    // CRT scanlines
    if (u_crtEnabled) {
        color *= scanline(v_TexCoord);

        // Slight RGB subpixel shift for CRT feel
        float r = texture(u_sceneTexture, v_TexCoord + vec2(0.5 / u_resolution.x, 0.0)).r;
        float b = texture(u_sceneTexture, v_TexCoord - vec2(0.5 / u_resolution.x, 0.0)).b;
        color.r = mix(color.r, r, 0.15);
        color.b = mix(color.b, b, 0.15);
    }

    FragColor = vec4(clamp(color, 0.0, 1.0), 1.0);
}
