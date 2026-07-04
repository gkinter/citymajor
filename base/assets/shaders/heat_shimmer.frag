#version 330 core

in vec2 v_TexCoord;

uniform sampler2D u_sceneTexture;
uniform vec2 u_resolution;
uniform float u_time;
uniform float u_shimmerIntensity; // 0.0 = off, 1.0 = full effect

out vec4 FragColor;

void main() {
    vec2 uv = v_TexCoord;

    // Only apply shimmer to the lower 30% of the screen (ground level)
    float groundMask = smoothstep(0.7, 0.85, uv.y);

    if (groundMask > 0.001 && u_shimmerIntensity > 0.001) {
        // Two overlapping sine waves at different frequencies for organic feel
        float wave1 = sin(uv.x * 40.0 + u_time * 2.5) * 0.003;
        float wave2 = sin(uv.x * 25.0 + u_time * 1.8 + 1.3) * 0.002;
        float wave3 = sin(uv.y * 30.0 + u_time * 3.2) * 0.001;

        // Combine waves for UV displacement
        vec2 displacement = vec2(
            (wave1 + wave3) * groundMask * u_shimmerIntensity,
            (wave2 + wave1 * 0.5) * groundMask * u_shimmerIntensity
        );

        uv += displacement;

        // Slight blur on distant (upper) parts within the shimmer zone
        // Achieved by averaging with a neighbor sample
        vec2 texelSize = 1.0 / u_resolution;
        float blurAmount = groundMask * u_shimmerIntensity * 0.3;

        vec3 center = texture(u_sceneTexture, uv).rgb;
        vec3 left   = texture(u_sceneTexture, uv + vec2(-texelSize.x, 0.0)).rgb;
        vec3 right  = texture(u_sceneTexture, uv + vec2( texelSize.x, 0.0)).rgb;
        vec3 up     = texture(u_sceneTexture, uv + vec2(0.0, -texelSize.y)).rgb;
        vec3 down   = texture(u_sceneTexture, uv + vec2(0.0,  texelSize.y)).rgb;

        vec3 blurred = mix(center, (center + left + right + up + down) / 5.0, blurAmount);

        // Warm color shift: slight push toward orange in the shimmer zone
        vec3 warmShift = blurred * vec3(1.04, 1.01, 0.96);
        vec3 result = mix(blurred, warmShift, groundMask * u_shimmerIntensity * 0.5);

        FragColor = vec4(clamp(result, 0.0, 1.0), 1.0);
    } else {
        // No shimmer: pass through
        FragColor = texture(u_sceneTexture, uv);
    }
}
