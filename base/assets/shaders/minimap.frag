#version 330 core

in vec2 v_uv;

uniform sampler2D u_minimapTex;   // RGBA8 minimap texture
uniform float u_opacity;          // Background opacity (0.0-1.0)

out vec4 FragColor;

void main()
{
    vec4 color = texture(u_minimapTex, v_uv);
    FragColor = vec4(color.rgb, color.a * u_opacity);
}
