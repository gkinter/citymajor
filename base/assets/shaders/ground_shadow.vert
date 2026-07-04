#version 330 core

// Per-vertex: position (xy) + uv (zw)
layout(location = 0) in vec4 a_QuadVertex;

// Per-instance: xy = screen position, zw = size of shadow quad
layout(location = 1) in vec4 a_PosSize;

uniform vec2 u_resolution;

out vec2 v_TexCoord;

void main() {
    // Scale quad vertex by instance size and offset by instance position
    vec2 pos = a_QuadVertex.xy * a_PosSize.zw + a_PosSize.xy;

    v_TexCoord = a_QuadVertex.zw;

    // Convert from screen coordinates to NDC
    vec2 ndc = (pos / u_resolution) * 2.0 - 1.0;
    ndc.y = -ndc.y;

    gl_Position = vec4(ndc, 0.0, 1.0);
}
