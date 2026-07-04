#version 330 core

in vec2 v_TexCoord;
in vec4 v_Tint;
in vec2 v_ScreenPos;

uniform float u_time;           // game time for animation (seconds)
uniform float u_pollution;      // 0.0 = crystal clear, 1.0 = heavily polluted
uniform sampler2D u_waterTex;   // base water texture (atlas)
uniform sampler2D u_reflectionTex; // pre-rendered reflection of buildings above water
uniform vec2 u_resolution;      // screen dimensions
uniform float u_reflectionStrength; // 0.0 - 1.0, how strong the reflection is

out vec4 FragColor;

// Pseudo-random hash for wave perturbation
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Simple 2D noise for wave displacement
float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f); // smoothstep

    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));

    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void main() {
    // === UV Animation: scroll for water flow effect ===
    // Two layers scrolling at different speeds for depth illusion
    vec2 flowUV1 = v_TexCoord + vec2(u_time * 0.02, u_time * 0.01);
    vec2 flowUV2 = v_TexCoord + vec2(-u_time * 0.015, u_time * 0.008);

    // Add wave distortion to UVs
    float waveX = noise(v_TexCoord * 8.0 + vec2(u_time * 0.5, 0.0)) * 0.01;
    float waveY = noise(v_TexCoord * 8.0 + vec2(0.0, u_time * 0.3)) * 0.01;
    flowUV1 += vec2(waveX, waveY);
    flowUV2 += vec2(-waveX * 0.7, waveY * 0.7);

    // Sample water texture at both flow layers
    vec4 waterSample1 = texture(u_waterTex, flowUV1);
    vec4 waterSample2 = texture(u_waterTex, flowUV2);
    vec4 waterBase = mix(waterSample1, waterSample2, 0.5);

    // === Pollution: shift color from blue to murky brown-green ===
    vec3 cleanColor = vec3(0.2, 0.4, 0.8);   // clear blue
    vec3 dirtyColor = vec3(0.3, 0.35, 0.15);  // murky brown-green
    vec3 waterColor = mix(cleanColor, dirtyColor, u_pollution);

    // Blend water texture with pollution-tinted color
    vec3 baseColor = mix(waterColor, waterBase.rgb * waterColor, 0.6);

    // === Specular highlights (sun sparkle) ===
    float sparkle = noise(v_TexCoord * 20.0 + vec2(u_time * 1.5, u_time * 0.8));
    sparkle = smoothstep(0.85, 0.95, sparkle) * (1.0 - u_pollution * 0.7);
    baseColor += vec3(sparkle * 0.3);

    // === Reflection: simple flip with reduced alpha ===
    vec2 reflectionUV = v_ScreenPos / u_resolution;
    reflectionUV.y = 1.0 - reflectionUV.y; // flip Y for reflection

    // Add ripple distortion to reflection
    float ripple = sin(v_TexCoord.x * 30.0 + u_time * 2.0) * 0.003 +
                   sin(v_TexCoord.y * 25.0 + u_time * 1.5) * 0.002;
    reflectionUV.x += ripple;
    reflectionUV.y += ripple * 0.5;

    vec4 reflection = texture(u_reflectionTex, reflectionUV);
    float reflectionAlpha = u_reflectionStrength * (1.0 - u_pollution * 0.5);
    baseColor = mix(baseColor, reflection.rgb, reflection.a * reflectionAlpha * 0.3);

    // === Edge foam (where water meets land — encoded in tint alpha) ===
    float foam = smoothstep(0.7, 1.0, v_Tint.a);
    float foamPattern = noise(v_TexCoord * 15.0 + vec2(u_time * 0.3, 0.0));
    foamPattern = smoothstep(0.4, 0.6, foamPattern);
    baseColor = mix(baseColor, vec3(0.9, 0.95, 1.0), foam * foamPattern * 0.4);

    // === Transparency: clean water is more transparent ===
    float alpha = mix(0.75, 0.95, u_pollution); // cleaner = more see-through

    // Apply instance tint (for day/night cycle coloring)
    baseColor *= v_Tint.rgb;

    FragColor = vec4(clamp(baseColor, 0.0, 1.0), alpha);
}
