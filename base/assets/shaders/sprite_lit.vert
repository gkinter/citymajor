#version 330 core

// Per-vertex quad data
layout(location = 0) in vec4 a_QuadVertex; // xy = position, zw = uv

// Per-instance data
layout(location = 1) in vec4 a_PosSize;    // xy = screen position, zw = size
layout(location = 2) in vec4 a_UV;          // xy = uv min, zw = uv max
layout(location = 3) in vec4 a_Tint;        // rgba tint color

uniform vec2 u_resolution;

out vec2 v_TexCoord;
out vec4 v_Tint;
out vec2 v_ScreenPos;

void main() {
    // Scale quad vertex by instance size and offset by instance position
    vec2 pos = a_QuadVertex.xy * a_PosSize.zw + a_PosSize.xy;

    // Map UV from quad [0,1] to atlas region
    v_TexCoord = mix(a_UV.xy, a_UV.zw, a_QuadVertex.zw);
    v_Tint = a_Tint;
    v_ScreenPos = pos;

    // Convert from screen coordinates to NDC
    vec2 ndc = (pos / u_resolution) * 2.0 - 1.0;
    ndc.y = -ndc.y;

    gl_Position = vec4(ndc, 0.0, 1.0);
}
