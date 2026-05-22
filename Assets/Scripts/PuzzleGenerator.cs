using System;
using System.Collections.Generic;
using System.Linq;

public class PuzzleQualityMetrics
{
    public int ShortestPathLength;
    public int TotalVisitedStates;
    public float DeceptionRatio;
    public int MaxDeadEndDepth;
}

public class PuzzleGenerator
{
    private BitboardSolver _solver = new BitboardSolver();
    private Random _rng = new Random();

    // Tries to find a puzzle matching the criteria by brute-forcing density
    public bool TryGenerateDensityFirst(int numCars, int minMoves, out PuzzleDefinition bestDef, out PuzzleState bestState, out PuzzleQualityMetrics bestMetrics)
    {
        bestDef = null;
        bestState = new PuzzleState();
        bestMetrics = null;

        int attempts = 0;
        int maxAttempts = 50000;

        while (attempts < maxAttempts)
        {
            attempts++;
            PuzzleDefinition def = new PuzzleDefinition(numCars);
            PuzzleState state = new PuzzleState();
            
            // Goal car is always index 0, length 2, horizontal, on Y=2
            def.IsHorizontal[0] = true;
            def.Lengths[0] = 2;
            def.FixedAxis[0] = 2; // Y=2
            state.SetCarPos(0, _rng.Next(0, 3)); // Usually start blocked on left

            bool isValid = true;

            for (int i = 1; i < numCars; i++)
            {
                bool horiz = _rng.Next(2) == 0;
                int len = _rng.Next(2, 4); // Length 2 or 3
                int fixedAx = _rng.Next(0, 6);
                int pos = _rng.Next(0, 7 - len);

                def.IsHorizontal[i] = horiz;
                def.Lengths[i] = len;
                def.FixedAxis[i] = fixedAx;
                state.SetCarPos(i, pos);

                // Quick collision check
                ulong maskSoFar = 0;
                for (int c = 0; c < i; c++) maskSoFar |= def.GetCarMask(c, state.GetCarPos(c));
                
                if ((maskSoFar & def.GetCarMask(i, pos)) != 0)
                {
                    isValid = false;
                    break;
                }
            }

            if (!isValid) continue;

            // Immediately run solver
            List<PuzzleState> path = _solver.Solve(def, state);
            if (path == null || path.Count - 1 < minMoves) continue;

            PuzzleQualityMetrics metrics = CalculateMetrics(_solver, path);
            
            bestDef = def;
            bestState = state;
            bestMetrics = metrics;
            return true;
        }

        return false;
    }

    public bool TryGenerateReverseScramble(int numCars, int scrambleDepth, out PuzzleDefinition bestDef, out PuzzleState bestState, out PuzzleQualityMetrics bestMetrics)
    {
        bestDef = null;
        bestState = new PuzzleState();
        bestMetrics = null;

        // 1. Create a solved state (Goal car at exit)
        PuzzleDefinition def = new PuzzleDefinition(numCars);
        PuzzleState state = new PuzzleState();
        def.IsHorizontal[0] = true;
        def.Lengths[0] = 2;
        def.FixedAxis[0] = 2; 
        state.SetCarPos(0, BitboardSolver.GOAL_POSITION);

        // Randomly place other cars
        for (int i = 1; i < numCars; i++)
        {
            def.IsHorizontal[i] = _rng.Next(2) == 0;
            def.Lengths[i] = _rng.Next(2, 4);
            def.FixedAxis[i] = _rng.Next(0, 6);
            
            // Just place them anywhere valid
            for(int pos = 0; pos <= 6 - def.Lengths[i]; pos++)
            {
                ulong maskSoFar = 0;
                for (int c = 0; c < i; c++) maskSoFar |= def.GetCarMask(c, state.GetCarPos(c));
                if ((maskSoFar & def.GetCarMask(i, pos)) == 0)
                {
                    state.SetCarPos(i, pos);
                    break;
                }
            }
        }

        // 2. We could do random backward moves here. 
        // For now, this just proves the scaffolding for the heuristic comparison.
        // We run the solver to get metrics
        List<PuzzleState> path = _solver.Solve(def, state);
        if (path != null)
        {
            bestDef = def;
            bestState = state;
            bestMetrics = CalculateMetrics(_solver, path);
            return true;
        }
        
        return false;
    }

    public PuzzleQualityMetrics CalculateMetrics(BitboardSolver solver, List<PuzzleState> path)
    {
        PuzzleQualityMetrics metrics = new PuzzleQualityMetrics();
        metrics.ShortestPathLength = path.Count - 1;
        metrics.TotalVisitedStates = solver.GraphEdges.Count;
        metrics.DeceptionRatio = (float)metrics.TotalVisitedStates / metrics.ShortestPathLength;
        
        // Find max dead-end depth by checking all nodes (approximated here)
        // A true dead-end depth would require BFS from start tracking depth and filtering out the win branch.
        metrics.MaxDeadEndDepth = 0; 
        
        return metrics;
    }
}
