using System;

namespace Gridlocked;

/// <summary>
/// Hexagonal board. A hexagon of radius R lives inside a W×W rhombus (W=2R+1)
/// in axial coords. Three movement axes (index deltas): ±1, ±W, ±(W-1).
/// The ±(W-1) diagonal is the wrap-prone one; the overridden CanSlide uses
/// column-edge masks to block wraps (validated in Gate 1).
/// </summary>
public sealed class HexBoard : Board
{
    public readonly int R;
    public readonly ulong LeftCol;   // q == 0
    public readonly ulong RightCol;  // q == W-1

    private static readonly (int dq, int dr)[] _deltas = { (1, 0), (0, 1), (-1, 1) };

    public override int AxisCount => 3;
    public override (int dq, int dr)[] Deltas => _deltas;

    public HexBoard(int radius)
    {
        if (radius < 1) throw new ArgumentOutOfRangeException(nameof(radius));
        R = radius;
        W = 2 * R + 1;
        Name = $"hex(r{radius})";
        if (W * W > 64)
            throw new ArgumentException($"Board needs {W * W} bits, exceeds 64 (max radius 3).");

        Step = new[] { 1, W, W - 1 };

        ulong mask = 0, left = 0, right = 0;
        for (int r = 0; r < W; r++)
        for (int q = 0; q < W; q++)
        {
            if (!InHex(q, r)) continue;
            ulong bit = 1UL << Idx(q, r);
            mask |= bit;
            if (q == 0) left |= bit;
            if (q == W - 1) right |= bit;
        }
        Mask = mask;
        LeftCol = left;
        RightCol = right;
    }

    protected override bool Shear => true;

    public override bool InCell(int q, int r) => InHex(q, r);

    public bool InHex(int q, int r)
    {
        if (q < 0 || q >= W || r < 0 || r >= W) return false;
        int qc = q - R, rc = r - R;
        return Math.Abs(qc) <= R && Math.Abs(rc) <= R && Math.Abs(qc + rc) <= R;
    }

    /// <summary>Fast bit-trick slide check with explicit wrap guards (Gate 1).</summary>
    public override bool CanSlide(ulong piece, ulong others, int axis, int dir)
    {
        if (axis == 0)
        {
            if (dir > 0 && (piece & RightCol) != 0) return false;
            if (dir < 0 && (piece & LeftCol) != 0) return false;
        }
        else if (axis == 2)
        {
            if (dir > 0 && (piece & LeftCol) != 0) return false;
            if (dir < 0 && (piece & RightCol) != 0) return false;
        }

        ulong moved = Shift(piece, axis, dir);

        if (System.Numerics.BitOperations.PopCount(moved) !=
            System.Numerics.BitOperations.PopCount(piece)) return false;
        if ((moved & Mask) != moved) return false;
        if ((moved & others) != 0) return false;
        return true;
    }
}
