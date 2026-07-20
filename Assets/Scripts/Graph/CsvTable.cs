using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Minimal CSV reader + join helper for the aggregate scatter. Both source CSVs
/// are append-only (duplicate puzzle_ids accumulate), so dedup keeps the latest.
/// </summary>
public static class CsvTable
{
    private static string ExportsDir =>
        Path.Combine(Application.dataPath, "..", "Exports");

    /// <summary>Load a CSV into header-keyed rows. Empty list if the file is missing.</summary>
    public static List<Dictionary<string, string>> Load(string path)
    {
        var rows = new List<Dictionary<string, string>>();
#if UNITY_WEBGL && !UNITY_EDITOR
        return rows;
#else
        if (!File.Exists(path)) return rows;

        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) return rows;

        var headers = lines[0].Split(',');
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = lines[i].Split(',');
            var row = new Dictionary<string, string>();
            for (int c = 0; c < headers.Length && c < cells.Length; c++)
                row[headers[c].Trim()] = cells[c].Trim();
            rows.Add(row);
        }
        return rows;
#endif
    }

    /// <summary>
    /// graph_summary rows (deduped to latest per puzzle_id), each augmented with a
    /// "rating" column from ratings.csv when a match exists.
    /// </summary>
    public static List<Dictionary<string, string>> JoinedRows()
    {
        var summary = Load(Path.Combine(ExportsDir, "graph_summary.csv"));

        // Dedup graph rows: keep last occurrence per puzzle_id.
        var byId = new Dictionary<string, Dictionary<string, string>>();
        foreach (var row in summary)
            if (row.TryGetValue("puzzle_id", out var id))
                byId[id] = row;

        var ratings = RatingsCsv.Read();
        foreach (var kvp in byId)
        {
            if (ratings.TryGetValue(kvp.Key, out var r))
            {
                kvp.Value["difficulty"]   = r.difficulty   >= 0 ? r.difficulty.ToString()   : "";
                kvp.Value["satisfaction"] = r.satisfaction >= 0 ? r.satisfaction.ToString() : "";
            }
            else
            {
                kvp.Value["difficulty"] = "";
                kvp.Value["satisfaction"] = "";
            }
        }

        return byId.Values.ToList();
    }
}
