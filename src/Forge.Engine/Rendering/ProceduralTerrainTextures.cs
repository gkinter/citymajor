using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Generates procedural 128x64 isometric terrain textures at startup and uploads
/// them as OpenGL textures. Each terrain type has multiple variants (different noise
/// seeds) to avoid visual repetition. Pixels outside the isometric diamond are
/// transparent. Uses linear filtering for smooth blending between adjacent tiles.
/// </summary>
public sealed class ProceduralTerrainTextures : IDisposable
{
    private const int TerrainTypeCount = 6;
    private const int DefaultVariants = 4;

    // [terrainType][variant] -> GL texture handle
    private uint[][] _textures = Array.Empty<uint[]>();
    private GL? _gl;
    private bool _generated;

    /// <summary>
    /// Generate all terrain textures and upload to GPU.
    /// </summary>
    public void Generate(GL gl, int tileWidth = 128, int tileHeight = 64)
    {
        _gl = gl;
        _textures = new uint[TerrainTypeCount][];

        for (int terrain = 0; terrain < TerrainTypeCount; terrain++)
        {
            int variantCount = DefaultVariants;
            _textures[terrain] = new uint[variantCount];

            for (int v = 0; v < variantCount; v++)
            {
                byte[] pixels = GenerateTerrainPixels(terrain, v, tileWidth, tileHeight);
                _textures[terrain][v] = UploadTexture(gl, pixels, tileWidth, tileHeight);
            }
        }

        _generated = true;
        Console.WriteLine($"[ProceduralTerrainTextures] Generated {TerrainTypeCount} terrain types x {DefaultVariants} variants.");
    }

    /// <summary>Get the GL texture ID for a terrain type + variant.</summary>
    public uint GetTexture(int terrainType, int variant)
    {
        if (!_generated || terrainType < 0 || terrainType >= _textures.Length)
            return 0;

        var variants = _textures[terrainType];
        int idx = ((variant % variants.Length) + variants.Length) % variants.Length;
        return variants[idx];
    }

    /// <summary>Get total variants for a terrain type.</summary>
    public int GetVariantCount(int terrainType)
    {
        if (!_generated || terrainType < 0 || terrainType >= _textures.Length)
            return 0;
        return _textures[terrainType].Length;
    }

    /// <summary>Bind a specific terrain texture to a texture unit.</summary>
    public void Bind(GL gl, int terrainType, int variant, uint textureUnit = 0)
    {
        uint tex = GetTexture(terrainType, variant);
        if (tex == 0) return;

        gl.ActiveTexture(TextureUnit.Texture0 + (int)textureUnit);
        gl.BindTexture(TextureTarget.Texture2D, tex);
    }

    public void Dispose()
    {
        if (_gl == null || !_generated) return;

        for (int t = 0; t < _textures.Length; t++)
        {
            for (int v = 0; v < _textures[t].Length; v++)
            {
                if (_textures[t][v] != 0)
                {
                    _gl.DeleteTexture(_textures[t][v]);
                    _textures[t][v] = 0;
                }
            }
        }

        _generated = false;
    }

    // -----------------------------------------------------------------------
    // Texture generation per terrain type
    // -----------------------------------------------------------------------

    private static byte[] GenerateTerrainPixels(int terrainType, int variant, int w, int h)
    {
        byte[] pixels = new byte[w * h * 4]; // RGBA
        int seed = terrainType * 1000 + variant * 137;

        for (int y = 0; y < h; y++)
        {
            // Isometric diamond bounds for this row
            int center = w / 2;
            int halfWidth = (y < h / 2) ? y * 2 : (h - 1 - y) * 2;
            int xMin = center - halfWidth;
            int xMax = center + halfWidth;

            for (int x = 0; x < w; x++)
            {
                int idx = (y * w + x) * 4;

                if (x < xMin || x > xMax)
                {
                    // Outside diamond: fully transparent
                    pixels[idx] = 0;
                    pixels[idx + 1] = 0;
                    pixels[idx + 2] = 0;
                    pixels[idx + 3] = 0;
                    continue;
                }

                // Normalized coordinates within the diamond (0-1)
                float nx = (halfWidth > 0) ? (float)(x - xMin) / (halfWidth * 2) : 0.5f;
                float ny = (float)y / (h - 1);

                // Distance from center of diamond (0 = center, 1 = edge)
                float cx = nx - 0.5f;
                float cy = ny - 0.5f;
                float edgeDist = MathF.Sqrt(cx * cx * 4f + cy * cy * 4f);
                edgeDist = MathF.Min(edgeDist, 1f);

                byte r, g, b, a;

                switch (terrainType)
                {
                    case 0: (r, g, b, a) = GenerateGrass(x, y, nx, ny, edgeDist, seed); break;
                    case 1: (r, g, b, a) = GenerateDirt(x, y, nx, ny, edgeDist, seed); break;
                    case 2: (r, g, b, a) = GenerateSand(x, y, nx, ny, edgeDist, seed); break;
                    case 3: (r, g, b, a) = GenerateWater(x, y, nx, ny, edgeDist, seed); break;
                    case 4: (r, g, b, a) = GenerateRock(x, y, nx, ny, edgeDist, seed); break;
                    case 5: (r, g, b, a) = GenerateForest(x, y, nx, ny, edgeDist, seed); break;
                    default: (r, g, b, a) = (128, 128, 128, 255); break;
                }

                pixels[idx] = r;
                pixels[idx + 1] = g;
                pixels[idx + 2] = b;
                pixels[idx + 3] = a;
            }
        }

        return pixels;
    }

    // --- Grass: Rich green with clumps, tiny flowers, directional blade lines ---
    private static (byte r, byte g, byte b, byte a) GenerateGrass(
        int x, int y, float nx, float ny, float edgeDist, int seed)
    {
        float n1 = Noise(x * 0.06f, y * 0.09f, seed);         // Large-scale patches
        float n2 = Noise(x * 0.18f, y * 0.25f, seed + 100);   // Medium clumps
        float n3 = Noise(x * 0.5f, y * 0.7f, seed + 200);     // Fine grass blades
        float n4 = Noise(x * 1.2f, y * 1.6f, seed + 300);     // Per-pixel detail

        // Rich base green with warm/cool variation
        float baseR = 0.20f + n1 * 0.04f + n2 * 0.02f;
        float baseG = 0.52f + n1 * 0.10f + n2 * 0.05f;
        float baseB = 0.16f + n1 * 0.02f;

        // Directional grass blade lines (diagonal strokes)
        float bladeAngle = nx * 14f + ny * 8f + n1 * 3f;
        float blade = MathF.Sin(bladeAngle * MathF.PI) * 0.5f + 0.5f;
        float bladeStrength = 0.04f * (0.5f + n2 * 0.5f);
        baseG += blade * bladeStrength;
        baseR -= blade * bladeStrength * 0.3f;

        // Dark grass clumps (clusters of darker green)
        if (n3 > 0.55f)
        {
            float clumpIntensity = (n3 - 0.55f) * 2.2f;
            baseG -= 0.06f * clumpIntensity;
            baseR -= 0.03f * clumpIntensity;
            baseB -= 0.02f * clumpIntensity;
        }

        // Tiny yellow-green flower dots (sparse)
        float flowerNoise = Noise(x * 2.5f, y * 3.5f, seed + 400);
        if (flowerNoise > 0.88f)
        {
            float flowerT = (flowerNoise - 0.88f) * 8.3f;
            baseR += 0.18f * flowerT;
            baseG += 0.12f * flowerT;
            baseB -= 0.04f * flowerT;
        }

        // Lighter highlight patches (sun dappling)
        if (n2 > 0.4f && n3 > 0.2f)
        {
            float sunlight = (n2 - 0.4f) * 1.5f * MathF.Max(0f, n3);
            baseR += 0.03f * sunlight;
            baseG += 0.06f * sunlight;
        }

        // Fine per-pixel grain
        baseR += n4 * 0.015f;
        baseG += n4 * 0.02f;
        baseB += n4 * 0.01f;

        // Very subtle edge darkening (natural shadow at tile border, not grid lines)
        float edgeFade = 1f - edgeDist * edgeDist * 0.04f;
        baseR *= edgeFade;
        baseG *= edgeFade;
        baseB *= edgeFade;

        return (ToByte(baseR), ToByte(baseG), ToByte(baseB), 255);
    }

    // --- Dirt: Brown base with pebble clusters, wagon tracks, grain texture ---
    private static (byte r, byte g, byte b, byte a) GenerateDirt(
        int x, int y, float nx, float ny, float edgeDist, int seed)
    {
        float n1 = Noise(x * 0.08f, y * 0.12f, seed);
        float n2 = Noise(x * 0.3f, y * 0.4f, seed + 300);
        float n3 = Noise(x * 0.7f, y * 0.9f, seed + 400);
        float n4 = Noise(x * 1.5f, y * 2.0f, seed + 500);

        // Warm brown base
        float baseR = 0.40f + n1 * 0.08f + n2 * 0.03f;
        float baseG = 0.28f + n1 * 0.06f + n2 * 0.02f;
        float baseB = 0.20f + n1 * 0.04f;

        // Grain pattern (fine directional noise simulating compressed earth)
        float grain = Noise(x * 0.6f + n2 * 1.5f, y * 0.9f + n2 * 1.5f, seed + 600);
        baseR += grain * 0.025f;
        baseG += grain * 0.018f;
        baseB += grain * 0.012f;

        // Pebble clusters: small bright spots
        float pebble = Noise(x * 2.0f, y * 2.8f, seed + 700);
        if (pebble > 0.72f)
        {
            float pebbleT = (pebble - 0.72f) * 3.6f;
            float brightness = 0.08f * pebbleT;
            baseR += brightness;
            baseG += brightness * 0.85f;
            baseB += brightness * 0.7f;
        }
        // Dark pebbles too
        if (pebble < -0.70f)
        {
            float darkT = (-0.70f - pebble) * 3.3f;
            baseR -= 0.05f * darkT;
            baseG -= 0.04f * darkT;
            baseB -= 0.03f * darkT;
        }

        // Wagon wheel track lines (faint parallel lines running diagonally)
        float trackLine = MathF.Sin((nx * 18f + ny * 6f) * MathF.PI);
        float trackNoise = Noise(x * 0.05f, y * 0.05f, seed + 800);
        if (MathF.Abs(trackLine) < 0.15f && trackNoise > 0.2f)
        {
            float trackDark = 0.06f * (1f - MathF.Abs(trackLine) / 0.15f);
            baseR -= trackDark;
            baseG -= trackDark;
            baseB -= trackDark * 0.8f;
        }

        // Slight moisture pooling in low spots (darker, slightly bluer patches)
        if (n1 < -0.3f && n2 < 0f)
        {
            float moisture = (-0.3f - n1) * 1.5f;
            baseR -= 0.03f * moisture;
            baseG -= 0.01f * moisture;
            baseB += 0.02f * moisture;
        }

        // Per-pixel fine noise
        baseR += n4 * 0.012f;
        baseG += n4 * 0.009f;
        baseB += n4 * 0.007f;

        // Very subtle edge shadow
        float shadow = 1f - edgeDist * edgeDist * 0.05f;
        baseR *= shadow;
        baseG *= shadow;
        baseB *= shadow;

        return (ToByte(baseR), ToByte(baseG), ToByte(baseB), 255);
    }

    // --- Sand: Warm tan with wind ripple lines, scattered rocks, dune shadows ---
    private static (byte r, byte g, byte b, byte a) GenerateSand(
        int x, int y, float nx, float ny, float edgeDist, int seed)
    {
        float n1 = Noise(x * 0.05f, y * 0.07f, seed);
        float n2 = Noise(x * 0.3f, y * 0.4f, seed + 600);
        float n3 = Noise(x * 0.8f, y * 1.1f, seed + 700);

        // Warm tan base
        float baseR = 0.80f + n1 * 0.06f;
        float baseG = 0.71f + n1 * 0.05f;
        float baseB = 0.52f + n1 * 0.04f;

        // Wind ripple lines (curved sinusoidal pattern)
        float ripplePhase = nx * 16f + ny * 5f + n1 * 2.5f + Noise(x * 0.02f, y * 0.03f, seed + 800) * 3f;
        float ripple = MathF.Sin(ripplePhase * MathF.PI);
        float rippleStrength = 0.035f * (1f - edgeDist * 0.3f);
        baseR += ripple * rippleStrength;
        baseG += ripple * rippleStrength * 0.85f;
        baseB += ripple * rippleStrength * 0.65f;

        // Ripple crest highlights (brighter at peaks)
        if (ripple > 0.7f)
        {
            float crest = (ripple - 0.7f) * 3.3f;
            baseR += 0.025f * crest;
            baseG += 0.02f * crest;
            baseB += 0.015f * crest;
        }

        // Small scattered rocks (sparse dark spots)
        float rockNoise = Noise(x * 2.2f, y * 3.0f, seed + 900);
        if (rockNoise > 0.9f)
        {
            float rockT = (rockNoise - 0.9f) * 10f;
            baseR -= 0.12f * rockT;
            baseG -= 0.10f * rockT;
            baseB -= 0.08f * rockT;
        }

        // Dune shadow (darker in low-noise areas simulating valleys between dunes)
        if (n1 < -0.25f)
        {
            float duneShadow = (-0.25f - n1) * 0.8f;
            baseR -= 0.04f * duneShadow;
            baseG -= 0.035f * duneShadow;
            baseB -= 0.025f * duneShadow;
        }

        // Fine grain noise
        baseR += n3 * 0.015f;
        baseG += n3 * 0.012f;
        baseB += n3 * 0.009f;

        // Very subtle edge fade
        float edgeFade = 1f - edgeDist * edgeDist * 0.03f;
        baseR *= edgeFade;
        baseG *= edgeFade;
        baseB *= edgeFade;

        return (ToByte(baseR), ToByte(baseG), ToByte(baseB), 255);
    }

    // --- Water: Depth-based color, wave crests with white highlights, foam at edges ---
    private static (byte r, byte g, byte b, byte a) GenerateWater(
        int x, int y, float nx, float ny, float edgeDist, int seed)
    {
        float n1 = Noise(x * 0.04f, y * 0.06f, seed);
        float n2 = Noise(x * 0.15f, y * 0.22f, seed + 100);

        // Depth-based color: darker center, lighter shore
        float depth = 1f - edgeDist;
        float baseR = 0.06f + edgeDist * 0.14f + depth * 0.02f;
        float baseG = 0.28f + edgeDist * 0.22f + depth * 0.04f;
        float baseB = 0.65f + edgeDist * 0.18f - depth * 0.08f;

        // Primary wave pattern
        float wavePhase = nx * 10f + ny * 4f + n1 * 2f;
        float wave = MathF.Sin(wavePhase * MathF.PI * 2f);
        float waveStrength = 0.035f * (0.6f + depth * 0.4f);
        baseR += wave * waveStrength * 0.25f;
        baseG += wave * waveStrength * 0.55f;
        baseB += wave * waveStrength;

        // Wave crest highlights (white foam at wave peaks)
        if (wave > 0.65f)
        {
            float crest = (wave - 0.65f) * 2.85f;
            float highlight = crest * crest * 0.18f;
            baseR += highlight;
            baseG += highlight;
            baseB += highlight * 0.7f;
        }

        // Secondary cross-wave (subtle)
        float wave2Phase = nx * 6f - ny * 9f + n2 * 1.5f;
        float wave2 = MathF.Sin(wave2Phase * MathF.PI * 2f) * 0.015f;
        baseG += wave2;
        baseB += wave2 * 1.5f;

        // Caustic-like light patterns on the water floor (visible in shallow areas)
        float caustic = Noise(x * 0.25f + n1 * 2f, y * 0.35f + n2 * 2f, seed + 200);
        float causticStrength = 0.03f * edgeDist; // Stronger near edges (shallow)
        baseR += MathF.Max(0f, caustic) * causticStrength * 0.6f;
        baseG += MathF.Max(0f, caustic) * causticStrength;
        baseB += MathF.Max(0f, caustic) * causticStrength * 0.4f;

        // Shore foam: bright frothy band near edges
        if (edgeDist > 0.75f)
        {
            float foamT = (edgeDist - 0.75f) * 4f;
            float foamNoise = Noise(x * 0.6f, y * 0.8f, seed + 300);
            if (foamNoise > -0.2f)
            {
                float foam = foamT * (0.5f + foamNoise * 0.5f);
                baseR += foam * 0.15f;
                baseG += foam * 0.18f;
                baseB += foam * 0.12f;
            }
        }

        // Slight alpha reduction at edges for softer shoreline
        byte alpha = (byte)(255 - (int)(edgeDist * edgeDist * 25f));
        if (alpha < 210) alpha = 210;

        return (ToByte(baseR), ToByte(baseG), ToByte(baseB), alpha);
    }

    // --- Rock: Gray with pronounced cracks, lichen patches, rough surface ---
    private static (byte r, byte g, byte b, byte a) GenerateRock(
        int x, int y, float nx, float ny, float edgeDist, int seed)
    {
        float n1 = Noise(x * 0.06f, y * 0.09f, seed);
        float n2 = Noise(x * 0.22f, y * 0.3f, seed + 700);
        float n3 = Noise(x * 0.6f, y * 0.8f, seed + 800);
        float n4 = Noise(x * 1.2f, y * 1.6f, seed + 900);

        // Gray base with slight warm/cool variation per patch
        float base_ = 0.45f + n1 * 0.10f;
        float baseR = base_ + 0.025f + n2 * 0.015f;
        float baseG = base_ + n2 * 0.01f;
        float baseB = base_ - 0.01f + n2 * 0.02f;

        // Rough surface texture (high-frequency noise)
        baseR += n4 * 0.035f;
        baseG += n4 * 0.035f;
        baseB += n4 * 0.035f;

        // Medium-scale surface variation (blotchy patches)
        baseR += n3 * 0.025f;
        baseG += n3 * 0.025f;
        baseB += n3 * 0.03f;

        // Crack network: dark lines where noise crosses zero
        float crackNoise1 = Noise(x * 0.12f + n2 * 2.5f, y * 0.18f + n2 * 2.5f, seed + 1000);
        float crackNoise2 = Noise(x * 0.2f + n1 * 1.5f, y * 0.15f + n1 * 1.5f, seed + 1100);
        float crackWidth1 = MathF.Abs(crackNoise1);
        float crackWidth2 = MathF.Abs(crackNoise2);

        if (crackWidth1 < 0.06f)
        {
            float crackIntensity = 1f - crackWidth1 / 0.06f;
            float crackDark = 1f - 0.35f * crackIntensity;
            baseR *= crackDark;
            baseG *= crackDark;
            baseB *= crackDark;
        }
        if (crackWidth2 < 0.05f)
        {
            float crackIntensity = 1f - crackWidth2 / 0.05f;
            float crackDark = 1f - 0.25f * crackIntensity;
            baseR *= crackDark;
            baseG *= crackDark;
            baseB *= crackDark;
        }

        // Lichen patches: slightly green/yellow spots on rock surface
        float lichenNoise = Noise(x * 0.15f, y * 0.2f, seed + 1200);
        float lichenDetail = Noise(x * 0.5f, y * 0.7f, seed + 1300);
        if (lichenNoise > 0.4f && lichenDetail > 0f)
        {
            float lichenT = (lichenNoise - 0.4f) * 1.7f * lichenDetail;
            baseR += 0.02f * lichenT;
            baseG += 0.06f * lichenT;
            baseB -= 0.01f * lichenT;
        }

        // Elevation-dependent brightness (lighter at top of tile = "sunlit face")
        float elevBright = 0.92f + (1f - ny) * 0.12f;
        baseR *= elevBright;
        baseG *= elevBright;
        baseB *= elevBright;

        // Edge shadow for depth
        float shadow = 1f - edgeDist * edgeDist * 0.06f;
        baseR *= shadow;
        baseG *= shadow;
        baseB *= shadow;

        return (ToByte(baseR), ToByte(baseG), ToByte(baseB), 255);
    }

    // --- Forest: Overlapping oval canopies with sunlit highlights, dark gaps ---
    private static (byte r, byte g, byte b, byte a) GenerateForest(
        int x, int y, float nx, float ny, float edgeDist, int seed)
    {
        // Ground layer (dark humus)
        float groundR = 0.15f;
        float groundG = 0.22f;
        float groundB = 0.10f;

        // Add ground noise
        float gn = Noise(x * 0.3f, y * 0.4f, seed + 1500);
        groundR += gn * 0.025f;
        groundG += gn * 0.03f;
        groundB += gn * 0.015f;

        // Scatter overlapping oval tree canopies
        float maxTreeInfluence = 0f;
        float bestTreeHash = 0f;
        float bestTreeDist = 1f;
        int cellSize = 16;
        int cellX = x / cellSize;
        int cellY = y / cellSize;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int ncx = cellX + dx;
                int ncy = cellY + dy;

                // Pseudo-random center offset within cell
                float hash1 = HashFloat(ncx, ncy, seed + 1100);
                float hash2 = HashFloat(ncx, ncy, seed + 1200);
                float hash3 = HashFloat(ncx, ncy, seed + 1300);
                float hash4 = HashFloat(ncx, ncy, seed + 1400);

                float treeCenterX = (ncx + 0.25f + hash1 * 0.5f) * cellSize;
                float treeCenterY = (ncy + 0.25f + hash2 * 0.5f) * cellSize;
                float treeRadiusX = 5.5f + hash3 * 7f;
                float treeRadiusY = treeRadiusX * (0.5f + hash4 * 0.3f); // Oval shape

                float ddx = x - treeCenterX;
                float ddy = (y - treeCenterY) * (treeRadiusX / treeRadiusY);
                float distSq = ddx * ddx + ddy * ddy;
                float radiusSq = treeRadiusX * treeRadiusX;

                if (distSq < radiusSq)
                {
                    float t = 1f - distSq / radiusSq;
                    if (t > maxTreeInfluence)
                    {
                        maxTreeInfluence = t;
                        bestTreeHash = hash3;
                        bestTreeDist = MathF.Sqrt(distSq / radiusSq);
                    }
                }
            }
        }

        float r, g, b;
        if (maxTreeInfluence > 0.05f)
        {
            // Tree canopy: rich dark green with per-tree variation
            float n = Noise(x * 0.1f, y * 0.15f, seed + 1600);
            float treeVariation = bestTreeHash * 0.08f - 0.04f;

            float canopyR = 0.08f + n * 0.025f + treeVariation * 0.5f;
            float canopyG = 0.32f + n * 0.07f + treeVariation;
            float canopyB = 0.08f + n * 0.015f;

            // Sunlit highlight at canopy top-right (light from upper-right)
            float highlightX = bestTreeDist * 0.7f;
            float sunAngle = (nx - 0.6f) * 2f + (ny - 0.3f) * 2f;
            float sunlight = MathF.Max(0f, 1f - MathF.Abs(sunAngle) * 0.8f) * maxTreeInfluence;
            if (bestTreeDist < 0.6f)
            {
                canopyR += sunlight * 0.04f;
                canopyG += sunlight * 0.08f;
                canopyB += sunlight * 0.02f;
            }

            // Dark shadow at canopy center/bottom
            float shadowFactor = maxTreeInfluence * maxTreeInfluence;
            canopyR -= shadowFactor * 0.04f;
            canopyG -= shadowFactor * 0.05f;
            canopyB -= shadowFactor * 0.02f;

            // Inter-tree shadow (where canopies overlap, gets darker)
            float overlapNoise = Noise(x * 0.08f, y * 0.12f, seed + 1700);
            if (overlapNoise > 0.3f && maxTreeInfluence > 0.6f)
            {
                float overlap = (overlapNoise - 0.3f) * (maxTreeInfluence - 0.6f) * 3f;
                canopyR -= 0.03f * overlap;
                canopyG -= 0.04f * overlap;
                canopyB -= 0.02f * overlap;
            }

            // Blend canopy over ground
            float blend = MathF.Min(maxTreeInfluence * 2.5f, 1f);
            r = Lerp(groundR, canopyR, blend);
            g = Lerp(groundG, canopyG, blend);
            b = Lerp(groundB, canopyB, blend);
        }
        else
        {
            // Forest floor: visible between trees
            float floorNoise = Noise(x * 0.5f, y * 0.7f, seed + 1800);
            r = groundR + floorNoise * 0.02f;
            g = groundG + floorNoise * 0.025f;
            b = groundB + floorNoise * 0.01f;

            // Fallen leaf spots
            float leafNoise = Noise(x * 1.5f, y * 2.0f, seed + 1900);
            if (leafNoise > 0.75f)
            {
                float leafT = (leafNoise - 0.75f) * 4f;
                r += 0.06f * leafT;
                g += 0.02f * leafT;
                b -= 0.01f * leafT;
            }
        }

        // Subtle edge fade
        float edgeFade = 1f - edgeDist * edgeDist * 0.04f;
        r *= edgeFade;
        g *= edgeFade;
        b *= edgeFade;

        return (ToByte(r), ToByte(g), ToByte(b), 255);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Simple value noise based on integer hash. Returns -1 to 1.</summary>
    private static float Noise(float x, float y, int seed)
    {
        int ix = (int)MathF.Floor(x);
        int iy = (int)MathF.Floor(y);
        float fx = x - ix;
        float fy = y - iy;

        // Smooth interpolation
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        float n00 = HashFloat(ix, iy, seed);
        float n10 = HashFloat(ix + 1, iy, seed);
        float n01 = HashFloat(ix, iy + 1, seed);
        float n11 = HashFloat(ix + 1, iy + 1, seed);

        float nx0 = Lerp(n00, n10, fx);
        float nx1 = Lerp(n01, n11, fx);
        float result = Lerp(nx0, nx1, fy);

        return result * 2f - 1f; // Remap 0..1 to -1..1
    }

    /// <summary>Hash two ints + seed to a float in 0..1.</summary>
    private static float HashFloat(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static byte ToByte(float v) => (byte)System.Math.Clamp((int)(v * 255f), 0, 255);

    private unsafe uint UploadTexture(GL gl, byte[] pixels, int width, int height)
    {
        uint tex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, tex);

        fixed (byte* ptr = pixels)
        {
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        // Linear filtering for smooth blending between terrain tiles
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        gl.BindTexture(TextureTarget.Texture2D, 0);
        return tex;
    }
}
