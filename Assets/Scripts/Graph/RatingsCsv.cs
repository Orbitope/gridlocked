using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Per-puzzle subjective satisfaction ratings (1–5), captured on solve.
/// Joined with graph_summary.csv by puzzle_id for the cross-puzzle scatter.
/// </summary>
public static class RatingsCsv
{
    private static string Path_ =>
        Path.Combine(Application.dataPath, "..", "Exports", "ratings.csv");

    public static void Append(string puzzleId, int rating, int moves, int optimal)
    {
        string path = Path_;
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        bool header = !File.Exists(path);
        using var w = new StreamWriter(path, append: true);
        if (header) w.WriteLine("timestamp,puzzle_id,rating,moves,optimal");
        w.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{puzzleId},{rating},{moves},{optimal}");
    }

    /// <summary>Latest rating per puzzle_id.</summary>
    public static Dictionary<string, int> Read()
    {
        var result = new Dictionary<string, int>();
        string path = Path_;
        if (!File.Exists(path)) return result;

        foreach (var row in CsvTable.Load(path))
        {
            if (row.TryGetValue("puzzle_id", out var id) &&
                row.TryGetValue("rating", out var r) &&
                int.TryParse(r, out int rating))
            {
                result[id] = rating; // later rows overwrite — keeps latest
            }
        }
        return result;
    }
}
