using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System.Collections.Generic;

public class PuzzleGraphWindow : EditorWindow
{
    private PuzzleGraphView _graphView;

    [MenuItem("Tools/Puzzle Graph Visualizer")]
    public static void OpenWindow()
    {
        var window = GetWindow<PuzzleGraphWindow>("Puzzle Graph");
        window.Show();
    }

    private void OnEnable()
    {
        ConstructGraphView();
        GenerateToolbar();
    }

    private void ConstructGraphView()
    {
        _graphView = new PuzzleGraphView
        {
            name = "Puzzle Graph"
        };
        _graphView.StretchToParentSize();
        rootVisualElement.Add(_graphView);
    }

    private void GenerateToolbar()
    {
        var toolbar = new UnityEditor.UIElements.Toolbar();
        var btn = new Button(() => {
            _graphView.PopulateFromSolver();
        }) { text = "Load Current Puzzle Graph" };
        toolbar.Add(btn);
        rootVisualElement.Add(toolbar);
    }
}

public class PuzzleGraphView : GraphView
{
    public PuzzleGraphView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
    }

    public void PopulateFromSolver()
    {
        DeleteElements(graphElements);

        GameManager gm = GameObject.FindFirstObjectByType<GameManager>();
        if (gm == null || gm.Def == null)
        {
            Debug.LogWarning("No active GameManager or Puzzle found!");
            return;
        }

        BitboardSolver solver = new BitboardSolver();
        solver.Solve(gm.Def, gm.CurrentState);

        Dictionary<ulong, Node> nodes = new Dictionary<ulong, Node>();
        
        int x = 0;
        int y = 0;
        int rowCount = 0;
        int maxRenderNodes = 200;

        Debug.Log($"Solver mapped {solver.GraphEdges.Count} total states.");
        
        if (solver.GraphEdges.Count > maxRenderNodes)
        {
            Debug.LogWarning($"Graph is too large to render entirely ({solver.GraphEdges.Count} nodes). Rendering the first {maxRenderNodes} nodes to prevent Unity from freezing.");
        }

        // Create nodes
        int renderedCount = 0;
        foreach (var kvp in solver.GraphEdges)
        {
            if (renderedCount >= maxRenderNodes) break;

            var node = CreateNode(kvp.Key.ToString());
            node.SetPosition(new Rect(x, y, 150, 100));
            AddElement(node);
            nodes[kvp.Key] = node;

            x += 200;
            rowCount++;
            if (rowCount > 10)
            {
                rowCount = 0;
                x = 0;
                y += 150;
            }
            renderedCount++;
        }

        // Create edges
        foreach (var kvp in nodes)
        {
            var outputPort = kvp.Value.outputContainer[0] as Port;
            foreach (ulong childData in solver.GraphEdges[kvp.Key])
            {
                if (nodes.TryGetValue(childData, out Node childNode))
                {
                    var inputPort = childNode.inputContainer[0] as Port;
                    var edge = outputPort.ConnectTo(inputPort);
                    AddElement(edge);
                }
            }
        }
    }

    private Node CreateNode(string title)
    {
        var node = new Node
        {
            title = title,
        };

        var inputPort = node.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        inputPort.portName = "In";
        node.inputContainer.Add(inputPort);

        var outputPort = node.InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        outputPort.portName = "Out";
        node.outputContainer.Add(outputPort);

        node.RefreshExpandedState();
        node.RefreshPorts();

        return node;
    }
}
