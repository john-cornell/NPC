namespace NPC.Library.Spatial.Grid;

using System;

public static class PerlinNoise
{
    private static readonly int[] permutation;
    private static readonly int[] p;

    static PerlinNoise()
    {
        permutation = new int[256];
        var random = new Random(42); // Fixed seed for stable tests
        for (int i = 0; i < 256; i++)
        {
            permutation[i] = i;
        }

        // Shuffle
        for (int i = 0; i < 256; i++)
        {
            int j = random.Next(256);
            (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
        }

        p = new int[512];
        for (int i = 0; i < 256; i++)
        {
            p[i] = p[i + 256] = permutation[i];
        }
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static double Lerp(double t, double a, double b) => a + t * (b - a);
    private static double Grad(int hash, double x, double y)
    {
        int h = hash & 15;
        double u = h < 8 ? x : y;
        double v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    public static double Noise(double x, double y)
    {
        int X = (int)Math.Floor(x) & 255;
        int Y = (int)Math.Floor(y) & 255;

        x -= Math.Floor(x);
        y -= Math.Floor(y);

        double u = Fade(x);
        double v = Fade(y);

        int A = p[X] + Y, AA = p[A], AB = p[A + 1];
        int B = p[X + 1] + Y, BA = p[B], BB = p[B + 1];

        return Lerp(v, Lerp(u, Grad(p[AA], x, y),
                               Grad(p[BA], x - 1, y)),
                       Lerp(u, Grad(p[AB], x, y - 1),
                               Grad(p[BB], x - 1, y - 1)));
    }
}
