using System;

namespace Gridlocked
{

/// <summary>
/// Coordinate-based piece placement. Builds a piece's cells by walking the
/// axis's true (q,r) neighbor deltas, so it can never produce a wrapped or
/// off-board piece (unlike raw bit-shift construction).
/// </summary>
public static class Placement
{
    /// <summary>
    /// Try to place a length-L piece on <paramref name="axis"/> starting at the
    /// min-index cell (q0,r0). On success returns the anchor and occupancy mask.
    /// Uses the board's own axis deltas, so it works for hex and square alike.
    /// </summary>
    public static bool TryPlace(Board board, int axis, int length, int q0, int r0,
                                out int anchor, out ulong mask)
    {
        anchor = 0; mask = 0;
        var (dq, dr) = board.Deltas[axis];
        ulong m = 0;
        int q = q0, r = r0;
        for (int k = 0; k < length; k++)
        {
            if (!board.InCell(q, r)) return false;
            m |= board.Bit(q, r);
            q += dq; r += dr;
        }
        anchor = board.Idx(q0, r0);
        mask = m;
        return true;
    }
}
}
