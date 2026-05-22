using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GraphManager : MonoBehaviour
{
    public GameObject nodePrefab;
    public Material lineMaterial;
    
    private void Start()
    {
        if (CrossSceneData.TargetDef == null)
        {
            Debug.LogError("No puzzle definition found in CrossSceneData!");
            return;
        }

        BuildGraph(CrossSceneData.TargetDef, CrossSceneData.TargetState);
    }

    private void BuildGraph(PuzzleDefinition def, PuzzleState startState)
    {
        BitboardSolver solver = new BitboardSolver();
        solver.Solve(def, startState);

        Dictionary<ulong, GameObject> nodeObjects = new Dictionary<ulong, GameObject>();

        // We want to layout by BFS depth.
        // We need to re-traverse the graph to find depths since BitboardSolver doesn't store them directly.
        Dictionary<ulong, int> nodeDepths = new Dictionary<ulong, int>();
        Dictionary<int, List<ulong>> layers = new Dictionary<int, List<ulong>>();
        
        Queue<ulong> queue = new Queue<ulong>();
        HashSet<ulong> visited = new HashSet<ulong>();
        
        queue.Enqueue(startState.Data);
        visited.Add(startState.Data);
        nodeDepths[startState.Data] = 0;
        layers[0] = new List<ulong> { startState.Data };

        while (queue.Count > 0)
        {
            ulong current = queue.Dequeue();
            int currentDepth = nodeDepths[current];

            if (solver.GraphEdges.TryGetValue(current, out List<ulong> children))
            {
                foreach (ulong child in children)
                {
                    if (!visited.Contains(child))
                    {
                        visited.Add(child);
                        int childDepth = currentDepth + 1;
                        nodeDepths[child] = childDepth;
                        
                        if (!layers.ContainsKey(childDepth))
                        {
                            layers[childDepth] = new List<ulong>();
                        }
                        layers[childDepth].Add(child);
                        
                        queue.Enqueue(child);
                    }
                }
            }
        }

        // Spawn Nodes using Depth Layout
        float xSpacing = 10f;
        float ySpacing = 3f;

        foreach (var layer in layers)
        {
            int depth = layer.Key;
            List<ulong> nodesInLayer = layer.Value;

            float startY = (nodesInLayer.Count - 1) * ySpacing / 2f;

            for (int i = 0; i < nodesInLayer.Count; i++)
            {
                ulong nodeData = nodesInLayer[i];
                float x = depth * xSpacing;
                float y = startY - (i * ySpacing);

                GameObject nodeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                nodeObj.name = $"Node_{nodeData}";
                nodeObj.transform.position = new Vector3(x, y, 0);
                
                // Color nodes
                PuzzleState state = new PuzzleState(nodeData);
                if (state.GetCarPos(BitboardSolver.GOAL_CAR_INDEX) == BitboardSolver.GOAL_POSITION)
                {
                    nodeObj.GetComponent<Renderer>().material.color = Color.red;
                }
                else if (nodeData == startState.Data)
                {
                    nodeObj.GetComponent<Renderer>().material.color = Color.green;
                    nodeObj.transform.localScale = Vector3.one * 1.5f;
                }
                
                nodeObjects[nodeData] = nodeObj;
            }
        }

        // Draw Edges
        foreach (var kvp in solver.GraphEdges)
        {
            if (nodeObjects.TryGetValue(kvp.Key, out GameObject parentObj))
            {
                foreach (ulong childData in kvp.Value)
                {
                    if (nodeObjects.TryGetValue(childData, out GameObject childObj))
                    {
                        GameObject lineObj = new GameObject("Edge");
                        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                        if (lineMaterial != null) lr.material = lineMaterial;
                        
                        lr.startWidth = 0.1f;
                        lr.endWidth = 0.05f;
                        lr.SetPosition(0, parentObj.transform.position);
                        lr.SetPosition(1, childObj.transform.position);
                        
                        // Gradient from white to gray
                        lr.startColor = Color.white;
                        lr.endColor = new Color(0.5f, 0.5f, 0.5f);
                    }
                }
            }
        }

        Debug.Log($"Rendered {nodeObjects.Count} nodes and {solver.GraphEdges.Count} branching groups via LineRenderers.");
    }
}
