using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class LevelGeneratorTool : EditorWindow
{
    private int numLevelsToGenerate = 50;
    private int carsPerLevel = 8;
    private int minMoves = 8;

    [MenuItem("Tools/Generate Offline Levels")]
    public static void ShowWindow()
    {
        GetWindow<LevelGeneratorTool>("Level Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Offline Level Generation", EditorStyles.boldLabel);
        
        numLevelsToGenerate = EditorGUILayout.IntField("Count", numLevelsToGenerate);
        carsPerLevel = EditorGUILayout.IntField("Cars per Level", carsPerLevel);
        minMoves = EditorGUILayout.IntField("Min Moves", minMoves);

        if (GUILayout.Button("Generate & Save"))
        {
            GenerateLevels();
        }
    }

    private void GenerateLevels()
    {
        // Find or create database
        LevelDatabase db = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Resources/LevelDatabase.asset");
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<LevelDatabase>();
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            AssetDatabase.CreateAsset(db, "Assets/Resources/LevelDatabase.asset");
        }

        PuzzleGenerator generator = new PuzzleGenerator();
        int generated = 0;

        EditorUtility.DisplayProgressBar("Generating Puzzles", "Brute forcing...", 0);

        while (generated < numLevelsToGenerate)
        {
            EditorUtility.DisplayProgressBar("Generating Puzzles", $"Found {generated}/{numLevelsToGenerate}...", (float)generated/numLevelsToGenerate);

            if (generator.TryGenerateDensityFirst(carsPerLevel, minMoves, out PuzzleDefinition def, out PuzzleState state, out PuzzleQualityMetrics metrics))
            {
                LevelData data = new LevelData();
                data.ID = System.Guid.NewGuid().ToString();
                data.SetDefinition(def);
                data.InitialStateData = state.Data;
                data.OptimalMoves = metrics.ShortestPathLength;
                data.ComplexityScore = metrics.DeceptionRatio;

                db.Levels.Add(data);
                generated++;
            }
        }

        // Sort by moves then complexity
        db.Levels.Sort((a, b) => {
            int moveCmp = a.OptimalMoves.CompareTo(b.OptimalMoves);
            if (moveCmp != 0) return moveCmp;
            return a.ComplexityScore.CompareTo(b.ComplexityScore);
        });

        EditorUtility.ClearProgressBar();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"Successfully generated and saved {numLevelsToGenerate} levels to LevelDatabase.asset!");
    }
}
