using System;
using System.Numerics;
using System.Text;

namespace Gridlocked;

/// <summary>
/// Abstract board shared by the hex and square variants so the solver,
/// generator, and instrumentation are identical across both — the only
/// difference is geometry (axis count, cell mask, neighbor deltas).
/// Cell (q,r) maps to bit index r*W + q.
/// </summary>
public abstract class Board
{
    public int W { get; protected set; }      // rhombus / grid width
    public int[] Step { get; protected set; } = Array.Empty<int>(); // bit-index step per axis
    public ulong Mask { get; protected set; }  // valid cells

    public abstract int AxisCount { get; }
    public abstract (int dq, int dr)[] Deltas { get; } // +index direction per axis
    public abstract bool InCell(int q, int r);
    protected virtual bool Shear => false;     // hex shears rows for the ASCII render

    public string Name { get; protected set; } = "board";

    public int Idx(int q, int r) => r * W + q;
    public ulong Bit(int q, int r) => 1UL << Idx(q, r);
    public int CellCount => BitOperations.PopCount(Mask);

    public ulong Shift(ulong piece, int axis, int dir)
        => dir > 0 ? piece << Step[axis] : piece >> Step[axis];

    /// <summary>
    /// Coordinate-validated slide check: wrap-proof by construction. Each cell
    /// of the piece is moved by the axis delta and verified to land on a real
    /// cell; then checked for collision. Subclasses may override with a faster
    /// bit-trick version (see HexBoard).
    /// </summary>
    public virtual bool CanSlide(ulong piece, ulong others, int axis, int dir)
    {
        var (dq, dr) = Deltas[axis];
        dq *= dir; dr *= dir;

        ulong moved = 0;
        ulong bits = piece;
        while (bits != 0)
        {
            int idx = BitOperations.TrailingZeroCount(bits);
            bits &= bits - 1;
            int q = idx % W, r = idx / W;
            int nq = q + dq, nr = r + dr;
            if (!InCell(nq, nr)) return false; // off board or wrapped
            moved |= Bit(nq, nr);
        }
        return (moved & others) == 0;
    }

    public virtual string Render(ulong occ = 0)
    {
        var sb = new StringBuilder();
        for (int r = 0; r < W; r++)
        {
            if (Shear) sb.Append(new string(' ', r));
            for (int q = 0; q < W; q++)
            {
                if (!InCell(q, r)) { sb.Append("  "); continue; }
                sb.Append((occ & Bit(q, r)) != 0 ? "X " : ". ");
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
