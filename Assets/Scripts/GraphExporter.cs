using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class GraphExporter
{
    public static void ExportToJSON(PuzzleDefinition def, PuzzleState startState, Dictionary<ulong, List<ulong>> edges, string filename)
    {
        string path = Path.Combine(Application.dataPath, "..", "Exports", filename);
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("{");
        
        // Export Definition for visualization context
        sb.AppendLine("  \"definition\": {");
        sb.AppendLine($"    \"width\": {def.Width},");
        sb.AppendLine($"    \"numCars\": {def.CarCount},");
        sb.AppendLine("    \"cars\": [");
        for (int i = 0; i < def.CarCount; i++)
        {
            sb.Append($"      {{\"isHorizontal\": {def.IsHorizontal[i].ToString().ToLower()}, \"length\": {def.Lengths[i]}, \"fixedAxis\": {def.FixedAxis[i]} }}");
            if (i < def.CarCount - 1) sb.Append(",");
            sb.AppendLine();
        }
        sb.AppendLine("    ]");
        sb.AppendLine("  },");
        
        sb.AppendLine($"  \"startState\": \"{startState.Data}\",");

        // Export Edges
        sb.AppendLine("  \"nodes\": [");
        
        int nodeCount = 0;
        foreach (var kvp in edges)
        {
            ulong state = kvp.Key;
            List<ulong> neighbors = kvp.Value;
            
            sb.Append($"    {{\"id\": \"{state}\", \"edges\": [");
            for (int i = 0; i < neighbors.Count; i++)
            {
                sb.Append($"\"{neighbors[i]}\"");
                if (i < neighbors.Count - 1) sb.Append(", ");
            }
            sb.Append("]}");
            
            if (nodeCount < edges.Count - 1) sb.Append(",");
            sb.AppendLine();
            
            nodeCount++;
        }
        
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        File.WriteAllText(path, sb.ToString());
        Debug.Log($"Exported graph with {edges.Count} nodes to {path}");
    }
}
