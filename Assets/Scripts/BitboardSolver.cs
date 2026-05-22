using System;
using System.Collections.Generic;

public class BitboardSolver
{
    public const int GOAL_CAR_INDEX = 0;
    // For a 6x6 grid, a length-2 car exits at X=4 (occupies 4 and 5)
    public const int GOAL_POSITION = 4; 

    // For visualization and heuristic grading
    public Dictionary<ulong, List<ulong>> GraphEdges = new Dictionary<ulong, List<ulong>>();

    public List<PuzzleState> Solve(PuzzleDefinition def, PuzzleState startState)
    {
        GraphEdges.Clear();

        Queue<PuzzleState> queue = new Queue<PuzzleState>();
        Dictionary<ulong, PuzzleState> parentMap = new Dictionary<ulong, PuzzleState>();
        HashSet<ulong> visited = new HashSet<ulong>();

        queue.Enqueue(startState);
        visited.Add(startState.Data);

        PuzzleState? winState = null;

        while (queue.Count > 0)
        {
            PuzzleState current = queue.Dequeue();

            if (current.GetCarPos(GOAL_CAR_INDEX) == GOAL_POSITION)
            {
                if (winState == null) 
                {
                    winState = current;
                }
                // We do not break early! We want to map the ENTIRE reachable graph for our metrics.
            }

            // Ensure graph entry
            if (!GraphEdges.ContainsKey(current.Data))
            {
                GraphEdges[current.Data] = new List<ulong>();
            }

            ulong currentMask = def.ComputeOccupancyMask(current);

            for (int c = 0; c < def.CarCount; c++)
            {
                int currentPos = current.GetCarPos(c);
                int length = def.Lengths[c];
                
                ulong maskWithoutCar = currentMask & ~def.GetCarMask(c, currentPos);

                // Try moving forward
                for (int pos = currentPos + 1; pos <= def.Width - length; pos++)
                {
                    ulong newCarMask = def.GetCarMask(c, pos);
                    if ((maskWithoutCar & newCarMask) != 0) break; // Collision

                    PuzzleState nextState = new PuzzleState(current.Data);
                    nextState.SetCarPos(c, pos);
                    
                    GraphEdges[current.Data].Add(nextState.Data);

                    if (!visited.Contains(nextState.Data))
                    {
                        visited.Add(nextState.Data);
                        parentMap[nextState.Data] = current;
                        queue.Enqueue(nextState);
                    }
                }

                // Try moving backward
                for (int pos = currentPos - 1; pos >= 0; pos--)
                {
                    ulong newCarMask = def.GetCarMask(c, pos);
                    if ((maskWithoutCar & newCarMask) != 0) break; // Collision

                    PuzzleState nextState = new PuzzleState(current.Data);
                    nextState.SetCarPos(c, pos);
                    
                    GraphEdges[current.Data].Add(nextState.Data);

                    if (!visited.Contains(nextState.Data))
                    {
                        visited.Add(nextState.Data);
                        parentMap[nextState.Data] = current;
                        queue.Enqueue(nextState);
                    }
                }
            }
        }

        if (winState == null) return null; // No solution

        // Reconstruct shortest path
        List<PuzzleState> path = new List<PuzzleState>();
        ulong curr = winState.Value.Data;
        
        while (parentMap.ContainsKey(curr))
        {
            path.Add(new PuzzleState(curr));
            curr = parentMap[curr].Data;
        }
        
        path.Add(startState);
        path.Reverse();
        return path;
    }
}
