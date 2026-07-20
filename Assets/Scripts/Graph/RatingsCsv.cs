using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Per-puzzle subjective ratings captured on solve: felt difficulty (mirrors the
/// Jarušek/Pelánek difficulty studies) and satisfaction (the video's angle),
/// both 1–5. Joined with graph_summary.csv by puzzle_id.
/// </summary>
public static class RatingsCsv
{
    private static string Path_ =>
        Path.Combine(Application.dataPath, "..", "Exports", "ratings.csv");

    public static void Append(string puzzleId, int difficulty, int satisfaction, int moves, int optimal)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return; // no filesystem in the browser sandbox
#else
        string path = Path_;
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        bool header = !File.Exists(path);
        using var w = new StreamWriter(path, append: true);
        if (header) w.WriteLine("timestamp,puzzle_id,difficulty,satisfaction,moves,optimal");
        w.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{puzzleId},{difficulty},{satisfaction},{moves},{optimal}");
#endif
    }

    /// <summary>Latest (difficulty, satisfaction) per puzzle_id.</summary>
    public static Dictionary<string, (int difficulty, int satisfaction)> Read()
    {
        var result = new Dictionary<string, (int, int)>();
#if UNITY_WEBGL && !UNITY_EDITOR
        return result;
#else
        string path = Path_;
        if (!File.Exists(path)) return result;

        foreach (var row in CsvTable.Load(path))
        {
            if (!row.TryGetValue("puzzle_id", out var id)) continue;
            int d = row.TryGetValue("difficulty", out var ds) && int.TryParse(ds, out int di) ? di : -1;
            int s = row.TryGetValue("satisfaction", out var ss) && int.TryParse(ss, out int si) ? si : -1;
            result[id] = (d, s); // later rows overwrite — keeps latest
        }
        return result;
#endif
    }
}
