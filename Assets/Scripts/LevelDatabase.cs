using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Gridlocked/Level Database")]
public class LevelDatabase : ScriptableObject
{
    public List<LevelData> Levels = new List<LevelData>();
}
