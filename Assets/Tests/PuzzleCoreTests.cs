using System.Collections.Generic;
using NUnit.Framework;

public class PuzzleCoreTests
{
    [Test]
    public void PuzzleState_Packing_Works()
    {
        PuzzleState state = new PuzzleState();
        state.SetCarPos(0, 3);
        Assert.AreEqual(3, state.GetCarPos(0));
        
        state.SetCarPos(1, 5);
        Assert.AreEqual(5, state.GetCarPos(1));
        
        Assert.AreEqual(3, state.GetCarPos(0));
        
        state.SetCarPos(0, 1);
        Assert.AreEqual(1, state.GetCarPos(0));
        Assert.AreEqual(5, state.GetCarPos(1));
    }

    [Test]
    public void PuzzleDefinition_Masks_AreCorrect()
    {
        PuzzleDefinition def = new PuzzleDefinition(1);
        def.Width = 6;
        def.IsHorizontal[0] = true;
        def.Lengths[0] = 2;
        def.FixedAxis[0] = 2; // Y = 2
        
        PuzzleState state = new PuzzleState();
        state.SetCarPos(0, 3); // X = 3
        
        ulong mask = def.ComputeOccupancyMask(state);
        ulong expectedMask = (1UL << (2 * 6 + 3)) | (1UL << (2 * 6 + 4));
        
        Assert.AreEqual(expectedMask, mask);
        
        ulong carMask = def.GetCarMask(0, 3);
        Assert.AreEqual(expectedMask, carMask);
    }
    
    [Test]
    public void BitboardSolver_Solves_TrivialPuzzle()
    {
        PuzzleDefinition def = new PuzzleDefinition(2);
        def.Width = 6;
        
        // Goal car
        def.IsHorizontal[0] = true;
        def.Lengths[0] = 2;
        def.FixedAxis[0] = 2;
        
        // Blocker
        def.IsHorizontal[1] = false;
        def.Lengths[1] = 2;
        def.FixedAxis[1] = 4;
        
        PuzzleState startState = new PuzzleState();
        startState.SetCarPos(0, 1);
        startState.SetCarPos(1, 1);
        
        BitboardSolver solver = new BitboardSolver();
        List<PuzzleState> path = solver.Solve(def, startState);
        
        Assert.IsNotNull(path, "Solver should find a path");
        Assert.IsTrue(path.Count > 1, "Path should have at least start and end state");
        
        PuzzleState finalState = path[path.Count - 1];
        Assert.AreEqual(BitboardSolver.GOAL_POSITION, finalState.GetCarPos(BitboardSolver.GOAL_CAR_INDEX));

        // Since we explicitly want the full graph, verify graph edges were created
        Assert.IsTrue(solver.GraphEdges.Count > 0, "Graph edges should be populated");
    }

    [Test]
    public void PuzzleGenerator_Finds_ValidPuzzle()
    {
        PuzzleGenerator generator = new PuzzleGenerator();
        
        // Try to generate a puzzle with 8 cars that takes at least 5 moves
        bool success = generator.TryGenerateDensityFirst(8, 5, out PuzzleDefinition def, out PuzzleState state, out PuzzleQualityMetrics metrics);
        
        // Brute force generation can theoretically fail if RNG is extremely unlucky, 
        // but 1000 attempts for a 5-move 8-car puzzle is virtually guaranteed to succeed.
        Assert.IsTrue(success, "Generator should find a valid puzzle within 1000 attempts");
        
        Assert.IsNotNull(def);
        Assert.IsNotNull(metrics);
        Assert.IsTrue(metrics.ShortestPathLength >= 5, "Generated puzzle should meet the minimum move requirement");
        Assert.IsTrue(metrics.TotalVisitedStates > 0, "Graph should have visited states");
    }

    [Test]
    public void PuzzleGenerator_ExportsGraph()
    {
        PuzzleGenerator generator = new PuzzleGenerator();
        BitboardSolver solver = new BitboardSolver();
        
        // Generate a decent puzzle
        generator.TryGenerateDensityFirst(8, 5, out PuzzleDefinition def, out PuzzleState state, out PuzzleQualityMetrics metrics);
        
        Assert.IsNotNull(def);
        
        // Ensure solver evaluates the whole graph and holds it in GraphEdges
        solver.Solve(def, state);
        
        // Export to JSON
        GraphExporter.ExportToJSON(def, state, solver.GraphEdges, "sample_graph.json");
        
        string path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Exports", "sample_graph.json");
        Assert.IsTrue(System.IO.File.Exists(path), "JSON export file should be created");
    }
}
