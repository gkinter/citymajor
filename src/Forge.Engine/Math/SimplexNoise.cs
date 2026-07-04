namespace Forge.Engine.Math;

/// <summary>
/// Simplex noise implementation for terrain generation. Produces smooth,
/// natural-looking height maps, moisture maps, and resource distribution.
/// </summary>
public static class SimplexNoise
{
    // Permutation table (shuffled 0-255, doubled to avoid wrapping)
    private static readonly byte[] Perm = new byte[512];
    private static readonly byte[] Perm12 = new byte[512];

    private static readonly int[][] Grad3 =
    [
        [1, 1, 0], [-1, 1, 0], [1, -1, 0], [-1, -1, 0],
        [1, 0, 1], [-1, 0, 1], [1, 0, -1], [-1, 0, -1],
        [0, 1, 1], [0, -1, 1], [0, 1, -1], [0, -1, -1],
    ];

    static SimplexNoise()
    {
        Seed(42);
    }

    /// <summary>Initialize the permutation table with a seed.</summary>
    public static void Seed(int seed)
    {
        var rng = new Random(seed);
        var p = new byte[256];
        for (int i = 0; i < 256; i++) p[i] = (byte)i;

        // Fisher-Yates shuffle
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        for (int i = 0; i < 512; i++)
        {
            Perm[i] = p[i & 255];
            Perm12[i] = (byte)(Perm[i] % 12);
        }
    }

    /// <summary>
    /// 2D simplex noise. Returns value in approximately [-1, 1].
    /// </summary>
    public static float Noise2D(float x, float y)
    {
        const float F2 = 0.3660254037844386f;  // (sqrt(3) - 1) / 2
        const float G2 = 0.21132486540518713f; // (3 - sqrt(3)) / 6

        float s = (x + y) * F2;
        int i = FastFloor(x + s);
        int j = FastFloor(y + s);

        float t = (i + j) * G2;
        float X0 = i - t;
        float Y0 = j - t;
        float x0 = x - X0;
        float y0 = y - Y0;

        int i1, j1;
        if (x0 > y0) { i1 = 1; j1 = 0; }
        else { i1 = 0; j1 = 1; }

        float x1 = x0 - i1 + G2;
        float y1 = y0 - j1 + G2;
        float x2 = x0 - 1f + 2f * G2;
        float y2 = y0 - 1f + 2f * G2;

        int ii = i & 255;
        int jj = j & 255;
        int gi0 = Perm12[ii + Perm[jj]];
        int gi1 = Perm12[ii + i1 + Perm[jj + j1]];
        int gi2 = Perm12[ii + 1 + Perm[jj + 1]];

        float n0, n1, n2;

        float t0 = 0.5f - x0 * x0 - y0 * y0;
        if (t0 < 0) n0 = 0;
        else { t0 *= t0; n0 = t0 * t0 * Dot(Grad3[gi0], x0, y0); }

        float t1 = 0.5f - x1 * x1 - y1 * y1;
        if (t1 < 0) n1 = 0;
        else { t1 *= t1; n1 = t1 * t1 * Dot(Grad3[gi1], x1, y1); }

        float t2 = 0.5f - x2 * x2 - y2 * y2;
        if (t2 < 0) n2 = 0;
        else { t2 *= t2; n2 = t2 * t2 * Dot(Grad3[gi2], x2, y2); }

        return 70f * (n0 + n1 + n2);
    }

    /// <summary>
    /// Fractal Brownian Motion (FBM) using simplex noise.
    /// Combines multiple octaves for natural-looking terrain.
    /// </summary>
    public static float Fbm(float x, float y, int octaves = 6, float lacunarity = 2f, float persistence = 0.5f)
    {
        float value = 0;
        float amplitude = 1;
        float frequency = 1;
        float maxAmplitude = 0;

        for (int i = 0; i < octaves; i++)
        {
            value += Noise2D(x * frequency, y * frequency) * amplitude;
            maxAmplitude += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return value / maxAmplitude;
    }

    /// <summary>
    /// Generate a heightmap for the given world size. Returns values in [0, 1].
    /// </summary>
    public static float[] GenerateHeightmap(int size, int seed, float scale = 0.005f, int octaves = 6)
    {
        Seed(seed);
        var map = new float[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = x * scale;
                float ny = y * scale;
                float value = Fbm(nx, ny, octaves);
                map[y * size + x] = (value + 1f) * 0.5f; // Normalize to [0, 1]
            }
        }

        return map;
    }

    private static int FastFloor(float x)
    {
        int xi = (int)x;
        return x < xi ? xi - 1 : xi;
    }

    private static float Dot(int[] g, float x, float y) =>
        g[0] * x + g[1] * y;
}
