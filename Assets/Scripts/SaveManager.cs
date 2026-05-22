using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public struct LevelScore
{
    public string LevelId;
    public int BestMoves;
}

[System.Serializable]
public class UserSaveData
{
    public List<LevelScore> LevelBestMoves = new List<LevelScore>();
}

public static class SaveManager
{
    private static UserSaveData _currentData;
    
    private static string SavePath
    {
        get { return Path.Combine(Application.persistentDataPath, "savedata.json"); }
    }

    public static void Load()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            _currentData = JsonUtility.FromJson<UserSaveData>(json);
        }
        
        if (_currentData == null)
        {
            _currentData = new UserSaveData();
        }
    }

    public static void Save()
    {
        if (_currentData == null) return;
        string json = JsonUtility.ToJson(_currentData, true);
        File.WriteAllText(SavePath, json);
    }

    public static void RecordVictory(string levelId, int totalMoves)
    {
        if (_currentData == null) Load();

        int index = _currentData.LevelBestMoves.FindIndex(x => x.LevelId == levelId);
        if (index == -1)
        {
            _currentData.LevelBestMoves.Add(new LevelScore { LevelId = levelId, BestMoves = totalMoves });
        }
        else
        {
            int previousBest = _currentData.LevelBestMoves[index].BestMoves;
            if (totalMoves < previousBest)
            {
                var s = _currentData.LevelBestMoves[index];
                s.BestMoves = totalMoves;
                _currentData.LevelBestMoves[index] = s;
            }
        }
        Save();
    }

    public static bool HasCompleted(string levelId)
    {
        if (_currentData == null) Load();
        return _currentData.LevelBestMoves.Exists(x => x.LevelId == levelId);
    }

    public static int GetBestMoves(string levelId)
    {
        if (_currentData == null) Load();
        int index = _currentData.LevelBestMoves.FindIndex(x => x.LevelId == levelId);
        if (index != -1)
        {
            return _currentData.LevelBestMoves[index].BestMoves;
        }
        return -1;
    }
}
