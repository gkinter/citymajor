#version 330 core

in vec2 v_TexCoord;

uniform sampler2D u_overlayData;  // R32F texture with values 0.0-1.0
uniform float u_opacity;

out vec4 FragColor;

// Color ramp: blue (low) -> green (mid) -> yellow -> red (high)
vec3 heatmapColor(float value) {
    // 4-stop gradient
    vec3 c0 = vec3(0.0, 0.0, 1.0); // Blue  (0.0)
    vec3 c1 = vec3(0.0, 1.0, 0.0); // Green (0.33)
    vec3 c2 = vec3(1.0, 1.0, 0.0); // Yellow (0.66)
    vec3 c3 = vec3(1.0, 0.0, 0.0); // Red   (1.0)

    float t = clamp(value, 0.0, 1.0);

    if (t < 0.333) {
        return mix(c0, c1, t * 3.0);
    } else if (t < 0.666) {
        return mix(c1, c2, (t - 0.333) * 3.0);
    } else {
        return mix(c2, c3, (t - 0.666) * 3.0);
    }
}

void main() {
    float value = texture(u_overlayData, v_TexCoord).r;

    // Skip zero values (no overlay)
    if (value < 0.001) {
        discard;
    }

    vec3 color = heatmapColor(value);
    FragColor = vec4(color, u_opacity);
}
