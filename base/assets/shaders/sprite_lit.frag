#version 330 core

in vec2 v_TexCoord;
in vec4 v_Tint;
in vec2 v_ScreenPos;

uniform sampler2D u_atlas;
uniform sampler2D u_normalMap;

// Lighting uniforms (set by LightingSystem)
uniform vec3 u_sunDirection;    // normalized, world-space
uniform vec3 u_sunColor;        // RGB, 0-1
uniform vec3 u_ambientColor;    // RGB, 0-1
uniform float u_nightStrength;  // 0 = day, 1 = night

// Whether a normal map is bound (0 = no, 1 = yes)
uniform int u_hasNormalMap;

// Palette swap (carried over from sprite.frag)
uniform bool u_paletteSwapEnabled;
uniform sampler2D u_paletteTexture;

out vec4 FragColor;

void main() {
    vec4 texColor = texture(u_atlas, v_TexCoord);

    if (u_paletteSwapEnabled) {
        float paletteIndex = texColor.r;
        vec4 paletteColor = texture(u_paletteTexture, vec2(paletteIndex, 0.5));
        texColor = vec4(paletteColor.rgb, texColor.a);
    }

    vec3 diffuse = texColor.rgb * v_Tint.rgb;
    float alpha = texColor.a * v_Tint.a;

    // Discard fully transparent pixels (important for isometric depth sorting)
    if (alpha < 0.01) {
        discard;
    }

    // Normal mapping: sample normal map or use flat normal
    vec3 normal;
    if (u_hasNormalMap != 0) {
        // Normal map: RGB channels encode XYZ in tangent space [0,1] -> [-1,1]
        vec3 rawNormal = texture(u_normalMap, v_TexCoord).rgb;
        normal = normalize(rawNormal * 2.0 - 1.0);
    } else {
        // Flat normal: surface faces directly toward camera
        normal = vec3(0.0, 0.0, 1.0);
    }

    // Directional lighting
    // Flip sun direction for dot product (light direction vs surface normal)
    vec3 lightDir = normalize(-u_sunDirection);

    // For isometric 2D sprites, we treat the normal map as being in screen space.
    // The sun direction's XY components map to screen-space light direction,
    // and Z component represents depth (into/out of screen).
    float NdotL = max(dot(normal, lightDir), 0.0);

    // Modulate directional light by night strength (dims as night falls)
    float directionalStrength = 1.0 - u_nightStrength;
    vec3 directional = u_sunColor * NdotL * directionalStrength;

    // Ambient light: always present, tinted by time of day
    vec3 ambient = u_ambientColor;

    // Final lit color
    vec3 litColor = (ambient + directional) * diffuse;

    FragColor = vec4(clamp(litColor, 0.0, 1.0), alpha);
}
