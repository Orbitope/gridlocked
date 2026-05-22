using System;
using System.Collections.Generic;

public class PuzzleDefinition
{
    public int Width = 6;
    public int CarCount;
    public bool[] IsHorizontal;
    public int[] Lengths;
    public int[] FixedAxis;

    public PuzzleDefinition(int carCount)
    {
        CarCount = carCount;
        IsHorizontal = new bool[carCount];
        Lengths = new int[carCount];
        FixedAxis = new int[carCount];
    }

    public ulong ComputeOccupancyMask(PuzzleState state)
    {
        ulong mask = 0;
        for (int i = 0; i < CarCount; i++)
        {
            mask |= GetCarMask(i, state.GetCarPos(i));
        }
        return mask;
    }

    public ulong GetCarMask(int carIndex, int pos)
    {
        ulong mask = 0;
        bool isHoriz = IsHorizontal[carIndex];
        int fixedAx = FixedAxis[carIndex];
        int len = Lengths[carIndex];

        for (int offset = 0; offset < len; offset++)
        {
            int x = isHoriz ? (pos + offset) : fixedAx;
            int y = isHoriz ? fixedAx : (pos + offset);
            int bitIndex = y * Width + x;
            mask |= (1UL << bitIndex);
        }
        return mask;
    }
}
