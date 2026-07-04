#version 330 core

in vec2 v_TexCoord;
in vec4 v_Tint;

uniform sampler2D u_atlas;

// Palette swap: if enabled, use the red channel as an index into a 256x1 palette texture
uniform bool u_paletteSwapEnabled;
uniform sampler2D u_paletteTexture;

out vec4 FragColor;

void main() {
    vec4 texColor = texture(u_atlas, v_TexCoord);

    if (u_paletteSwapEnabled) {
        // Use red channel as palette index (for indexed-color sprite sheets)
        float paletteIndex = texColor.r;
        vec4 paletteColor = texture(u_paletteTexture, vec2(paletteIndex, 0.5));
        FragColor = paletteColor * v_Tint;
        FragColor.a = texColor.a * v_Tint.a;
    } else {
        FragColor = texColor * v_Tint;
    }

    // Discard fully transparent pixels (important for isometric depth sorting)
    if (FragColor.a < 0.01) {
        discard;
    }
}
