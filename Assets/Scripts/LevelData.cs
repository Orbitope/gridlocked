using UnityEngine;

[System.Serializable]
public struct LevelData
{
    public string ID;
    public int CarCount;
    public int OptimalMoves;
    public float ComplexityScore;
    
    // Serialized state
    public ulong InitialStateData;
    
    // Serialized definition arrays
    public bool[] IsHorizontal;
    public int[] Lengths;
    public int[] FixedAxis;

    public PuzzleDefinition GetDefinition()
    {
        PuzzleDefinition def = new PuzzleDefinition(CarCount);
        for (int i = 0; i < CarCount; i++)
        {
            def.IsHorizontal[i] = IsHorizontal[i];
            def.Lengths[i] = Lengths[i];
            def.FixedAxis[i] = FixedAxis[i];
        }
        return def;
    }

    public void SetDefinition(PuzzleDefinition def)
    {
        CarCount = def.CarCount;
        IsHorizontal = new bool[CarCount];
        Lengths = new int[CarCount];
        FixedAxis = new int[CarCount];
        
        for (int i = 0; i < CarCount; i++)
        {
            IsHorizontal[i] = def.IsHorizontal[i];
            Lengths[i] = def.Lengths[i];
            FixedAxis[i] = def.FixedAxis[i];
        }
    }
}
