using System;
using System.Collections.Generic;
using System.Numerics;

namespace Gridlocked
{

/// <summary>
/// A piece is a straight line of <see cref="Length"/> cells that slides only
/// along its fixed <see cref="Axis"/> (0, 1, or 2). Its position is a single
/// scalar: the anchor (lowest bit index it occupies). The cell pattern is
///   anchor + k*Step[Axis]   for k in 0..Length-1
/// so the live bitmask is just (Pattern &lt;&lt; anchor).
/// </summary>
public sealed class Piece
{
    public readonly int Axis;
    public readonly int Length;
    public readonly ulong Pattern; // Length cells starting at index 0, stepping by Step[Axis]

    public Piece(Board board, int axis, int length)
    {
        Axis = axis;
        Length = length;
        int step = board.Step[axis];
        ulong p = 0;
        for (int k = 0; k < length; k++) p |= 1UL << (k * step);
        Pattern = p;
    }

    public ulong MaskAt(int anchor) => Pattern << anchor;
}

/// <summary>
/// Compact, identity-preserving state key: each piece's anchor (0..48, 6 bits)
/// packed in piece order across two ulongs (supports up to 20 pieces).
/// </summary>
public readonly struct StateKey : IEquatable<StateKey>
{
    public readonly ulong A, B;
    public StateKey(ulong a, ulong b) { A = a; B = b; }

    public static StateKey Pack(int[] anchors)
    {
        ulong a = 0, b = 0;
        for (int i = 0; i < anchors.Length; i++)
        {
            ulong v = (ulong)(anchors[i] & 0x3F);
            if (i < 10) a |= v << (i * 6);
            else        b |= v << ((i - 10) * 6);
        }
        return new StateKey(a, b);
    }

    /// <summary>Inverse of Pack — reads <paramref name="count"/> 6-bit anchors back out.</summary>
    public int[] Unpack(int count)
    {
        var anchors = new int[count];
        for (int i = 0; i < count; i++)
        {
            ulong src = i < 10 ? A : B;
            int shift = (i < 10 ? i : i - 10) * 6;
            anchors[i] = (int)((src >> shift) & 0x3F);
        }
        return anchors;
    }

    public bool Equals(StateKey o) => A == o.A && B == o.B;
    public override bool Equals(object? o) => o is StateKey k && Equals(k);
    public override int GetHashCode() => HashCode.Combine(A, B);
}

/// <summary>
/// A hex Rush Hour puzzle: a board, a set of pieces with start anchors, a
/// target piece, and an exit mask. Solved when the target covers an exit cell.
/// </summary>
public sealed class Puzzle
{
    public readonly Board Board;
    public readonly Piece[] Pieces;
    public readonly int[] StartAnchors;
    public readonly int TargetIndex;
    public readonly ulong ExitMask;

    public Puzzle(Board board, Piece[] pieces, int[] startAnchors, int targetIndex, ulong exitMask)
    {
        Board = board;
        Pieces = pieces;
        StartAnchors = startAnchors;
        TargetIndex = targetIndex;
        ExitMask = exitMask;
    }

    public bool IsGoal(int[] anchors)
    {
        ulong targetMask = Pieces[TargetIndex].MaskAt(anchors[TargetIndex]);
        return (targetMask & ExitMask) != 0;
    }

    /// <summary>Total occupancy bitmask for a given anchor configuration.</summary>
    public ulong Occupancy(int[] anchors)
    {
        ulong occ = 0;
        for (int i = 0; i < Pieces.Length; i++) occ |= Pieces[i].MaskAt(anchors[i]);
        return occ;
    }
}
}
