#version 330 core

in vec2 v_uv;

uniform sampler2D u_overlayData;  // R32F texture with values 0.0-1.0
uniform int u_overlayType;        // 0=none,1=traffic,2=landvalue,3=happiness,4=pollution,5=crime,6=fire,7=power,8=water
uniform float u_opacity;          // Fade opacity (0.0-1.0) for smooth transitions

out vec4 FragColor;

// Traffic: green(0) -> yellow(0.5) -> red(1.0)
vec3 trafficColor(float t) {
    if (t < 0.5) {
        return mix(vec3(0.0, 0.8, 0.0), vec3(1.0, 1.0, 0.0), t * 2.0);
    } else {
        return mix(vec3(1.0, 1.0, 0.0), vec3(1.0, 0.0, 0.0), (t - 0.5) * 2.0);
    }
}

// Land value: light purple(low) -> deep purple(high)
// #CE93D8 = (0.808, 0.576, 0.847), #6A1B9A = (0.416, 0.106, 0.604)
vec3 landValueColor(float t) {
    vec3 low  = vec3(0.808, 0.576, 0.847);
    vec3 high = vec3(0.416, 0.106, 0.604);
    return mix(low, high, t);
}

// Happiness: red(low) -> green(high)
vec3 happinessColor(float t) {
    return mix(vec3(1.0, 0.0, 0.0), vec3(0.0, 0.8, 0.0), t);
}

// Pollution: transparent(clean) -> brown(heavy) #4E342E = (0.306, 0.204, 0.180)
vec3 pollutionColor(float t) {
    return vec3(0.306, 0.204, 0.180);
}

// Crime: transparent(safe) -> red(dangerous) #C62828 = (0.776, 0.157, 0.157)
vec3 crimeColor(float t) {
    return vec3(0.776, 0.157, 0.157);
}

// Fire risk: transparent(safe) -> orange(high) #E65100 = (0.902, 0.318, 0.0)
vec3 fireColor(float t) {
    return vec3(0.902, 0.318, 0.0);
}

// Power: yellow(connected) -> gray(disconnected)
vec3 powerColor(float t) {
    // t=1 means connected (yellow), t=0 means disconnected (gray)
    return mix(vec3(0.5, 0.5, 0.5), vec3(1.0, 0.9, 0.0), t);
}

// Water: blue(connected) -> gray(dry)
vec3 waterColor(float t) {
    // t=1 means connected (blue), t=0 means dry (gray)
    return mix(vec3(0.5, 0.5, 0.5), vec3(0.2, 0.4, 0.9), t);
}

void main() {
    float value = texture(u_overlayData, v_uv).r;

    // Skip zero values (no overlay data)
    if (value < 0.001) {
        discard;
    }

    vec3 color;
    float alpha;

    switch (u_overlayType) {
        case 1: // Traffic
            color = trafficColor(value);
            alpha = 0.5;
            break;
        case 2: // Land Value
            color = landValueColor(value);
            alpha = 0.5;
            break;
        case 3: // Happiness
            color = happinessColor(value);
            alpha = 0.5;
            break;
        case 4: // Pollution
            color = pollutionColor(value);
            alpha = value * 0.6; // Transparent when clean, opaque when heavy
            break;
        case 5: // Crime
            color = crimeColor(value);
            alpha = value * 0.6;
            break;
        case 6: // Fire Risk
            color = fireColor(value);
            alpha = value * 0.6;
            break;
        case 7: // Power
            color = powerColor(value);
            alpha = 0.5;
            break;
        case 8: // Water
            color = waterColor(value);
            alpha = 0.5;
            break;
        default:
            discard;
    }

    FragColor = vec4(color, alpha * u_opacity);
}
