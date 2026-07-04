#version 330 core

in vec2 v_TexCoord;

// Shadow parameters (per-instance, passed as uniforms or via vertex data)
uniform float u_shadowOpacity;     // Base opacity, scales with building height (0-0.6)
uniform float u_shadowSoftness;    // Edge falloff (0 = hard, 1 = very soft)
uniform vec2 u_shadowOffset;       // Offset from building center based on sun direction
uniform float u_nightStrength;     // Shadows fade at night (no directional sun)

out vec4 FragColor;

void main() {
    // UV is [0,1] across the shadow quad, centered at (0.5, 0.5)
    vec2 center = v_TexCoord - 0.5;

    // Elliptical distance: shadows are wider than tall on the isometric ground plane
    // Scale X by 2.0 for the 2:1 isometric diamond ratio
    float dist = length(vec2(center.x, center.y * 2.0)) * 2.0;

    // Radial gradient: dark center, transparent edges
    float softness = max(u_shadowSoftness, 0.01);
    float falloff = 1.0 - smoothstep(0.0, softness, dist);

    // Shadow opacity: scales with building height and fades at night
    float dayFactor = 1.0 - u_nightStrength * 0.8; // Shadows don't fully disappear (moonlight)
    float opacity = u_shadowOpacity * falloff * dayFactor;

    // Pure black shadow with computed alpha
    FragColor = vec4(0.0, 0.0, 0.0, opacity);
}
