using System;

public struct PuzzleState : IEquatable<PuzzleState>
{
    public ulong Data;

    public PuzzleState(ulong data)
    {
        Data = data;
    }

    public void SetCarPos(int carIndex, int pos)
    {
        ulong mask = ~(7UL << (carIndex * 3));
        Data = (Data & mask) | ((ulong)pos << (carIndex * 3));
    }

    public int GetCarPos(int carIndex)
    {
        return (int)((Data >> (carIndex * 3)) & 7UL);
    }

    public override bool Equals(object obj)
    {
        return obj is PuzzleState state && Equals(state);
    }

    public bool Equals(PuzzleState other)
    {
        return Data == other.Data;
    }

    public override int GetHashCode()
    {
        return Data.GetHashCode();
    }

    public static bool operator ==(PuzzleState left, PuzzleState right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PuzzleState left, PuzzleState right)
    {
        return !(left == right);
    }
}
