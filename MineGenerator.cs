using System;

public class MineGenerator
{
    private const double DefaultDensity = 0.2063;

    public uint Seed { get; set; } = 0;
    private int _startX;
    private int _startY;
    private double _density;

    public int StartX => _startX;
    public int StartY => _startY;

    public MineGenerator(uint seed, double density = DefaultDensity)
    {
        _startX = 0;
        _startY = 0;
        _density = density;
        Seed = seed;
    }

    public void FirstClickAt(int x, int y)
    {
        _startX = x;
        _startY = y;
    }

    public bool IsMine(int x, int y)
    {
        // First click guarantees no mine in the 3x3 area around the first click
        if (Math.Abs(x - _startX) <= 1 && Math.Abs(y - _startY) <= 1)
        {
            return false;
        }

        // "random" value between 0 and 1
        ulong randval = RandXYZ(x, y, Seed);
        double randomValue = (double)randval / ulong.MaxValue;

        return randomValue < _density;
    }

    public uint CountNeighborMines(int x, int y)
    {
        uint count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                if (IsMine(x + dx, y + dy))
                {
                    count++;
                }
            }
        }
        return count;
    }


    // SplitMix64 based
    private static ulong RandXYZ(int x, int y, uint seed)
    {
        unchecked
        {
            ulong z = seed;
            z += 0x9E3779B97F4A7C15UL + ((ulong)(uint)x << 32) + (uint)y;

            // SplitMix64 finalizer (Stafford Variant 13)
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z = z ^ (z >> 31);

            return z;
        }
    }
}