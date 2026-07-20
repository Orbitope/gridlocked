using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Rush-Hour-specific difficulty metrics grounded in Jarušek &amp; Pelánek's
/// human-validated findings: counterintuitive moves (Spearman ~0.69) and the
/// dependency structure of the solution (~0.82 — the strongest predictor of
/// human difficulty). Needs car geometry, so it lives outside the
/// adjacency-generic GraphAnalysis.
/// </summary>
public static class RushHourMetrics
{
    public sealed class Result
    {
        public int   CounterintuitiveMoves;
        public float CounterintuitiveFrac;
        public int   DependencyDepth;      // longest "must move X before Y" chain
        public int   DependencyWidth;      // independent subproblems (components)
        public List<(int from, int to)> DepEdges = new();
        public Dictionary<int, int> DepRank = new(); // piece -> longest-chain depth
        public HashSet<int> MovedPieces = new();
        public bool[] StepCounterintuitive;  // per optimal-path step
    }

    public static Result Analyze(PuzzleDefinition def, List<PuzzleState> path)
    {
        var r = new Result();
        if (path == null || path.Count < 2) { r.StepCounterintuitive = new bool[0]; return r; }

        int steps = path.Count - 1;
        r.StepCounterintuitive = new bool[steps];

        // --- Counterintuitive moves: greedy heuristic must fail to improve. ---
        int prevH = Heuristic(def, path[0]);
        int counter = 0;
        for (int i = 1; i < path.Count; i++)
        {
            int h = Heuristic(def, path[i]);
            bool bad = h >= prevH;           // did NOT make greedy progress
            r.StepCounterintuitive[i - 1] = bad;
            if (bad) counter++;
            prevH = h;
        }
        r.CounterintuitiveMoves = counter;
        r.CounterintuitiveFrac  = steps > 0 ? (float)counter / steps : 0f;

        // --- Dependency DAG: A->B when B enters a cell A most-recently vacated. ---
        var lastVacatedBy = new Dictionary<int, int>(); // cell -> piece
        var edgeSet = new HashSet<(int, int)>();
        for (int i = 1; i < path.Count; i++)
        {
            int mover = MovedCar(def, path[i - 1], path[i]);
            if (mover < 0) continue;
            r.MovedPieces.Add(mover);

            var fromCells = CarCells(def, mover, path[i - 1].GetCarPos(mover));
            var toCells   = CarCells(def, mover, path[i].GetCarPos(mover));
            var fromSet   = new HashSet<int>(fromCells);
            var toSet     = new HashSet<int>(toCells);

            // Cells newly occupied by the mover.
            foreach (int c in toCells)
            {
                if (fromSet.Contains(c)) continue;
                if (lastVacatedBy.TryGetValue(c, out int a) && a != mover)
                    edgeSet.Add((a, mover));
            }
            // Cells the mover just vacated become "most recently vacated by mover".
            foreach (int c in fromCells)
                if (!toSet.Contains(c)) lastVacatedBy[c] = mover;
        }
        r.DepEdges = edgeSet.ToList();

        // Longest chain (DFS + on-stack guard breaks any rare cycles).
        var succ = new Dictionary<int, List<int>>();
        foreach (var (a, b) in edgeSet)
        {
            if (!succ.ContainsKey(a)) succ[a] = new List<int>();
            succ[a].Add(b);
            r.MovedPieces.Add(a); r.MovedPieces.Add(b);
        }
        var memo = new Dictionary<int, int>();
        var onStack = new HashSet<int>();
        int LongestFrom(int node)
        {
            if (memo.TryGetValue(node, out int cached)) return cached;
            if (onStack.Contains(node)) return 0;
            onStack.Add(node);
            int best = 1;
            if (succ.TryGetValue(node, out var ss))
                foreach (var s in ss) best = System.Math.Max(best, 1 + LongestFrom(s));
            onStack.Remove(node);
            memo[node] = best;
            return best;
        }
        int depth = 0;
        foreach (int p in r.MovedPieces)
        {
            int d = LongestFrom(p);
            r.DepRank[p] = d;
            depth = System.Math.Max(depth, d);
        }
        r.DependencyDepth = depth;

        // Width = weakly-connected components among moved pieces (union-find).
        var parent = new Dictionary<int, int>();
        int Find(int x) { if (!parent.ContainsKey(x)) parent[x] = x;
            return parent[x] == x ? x : (parent[x] = Find(parent[x])); }
        foreach (int p in r.MovedPieces) Find(p);
        foreach (var (a, b) in edgeSet) parent[Find(a)] = Find(b);
        r.DependencyWidth = r.MovedPieces.Select(Find).Distinct().Count();

        return r;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Greedy human heuristic: red car's remaining distance + blockers ahead.</summary>
    private static int Heuristic(PuzzleDefinition def, PuzzleState s)
    {
        int red = BitboardSolver.GOAL_CAR_INDEX;
        int redPos  = s.GetCarPos(red);
        int redDist = BitboardSolver.GOAL_POSITION - redPos;
        return redDist + Blockers(def, s);
    }

    /// <summary>Distinct cars occupying the red car's row to the right of its leading edge.</summary>
    private static int Blockers(PuzzleDefinition def, PuzzleState s)
    {
        int red = BitboardSolver.GOAL_CAR_INDEX;
        int redRow  = def.FixedAxis[red];
        int redLead = s.GetCarPos(red) + def.Lengths[red] - 1;

        var blocking = new HashSet<int>();
        for (int i = 0; i < def.CarCount; i++)
        {
            if (i == red) continue;
            foreach (int cell in CarCells(def, i, s.GetCarPos(i)))
            {
                int x = cell % def.Width;
                int y = cell / def.Width;
                if (y == redRow && x > redLead) { blocking.Add(i); break; }
            }
        }
        return blocking.Count;
    }

    private static int MovedCar(PuzzleDefinition def, PuzzleState a, PuzzleState b)
    {
        for (int i = 0; i < def.CarCount; i++)
            if (a.GetCarPos(i) != b.GetCarPos(i)) return i;
        return -1;
    }

    private static List<int> CarCells(PuzzleDefinition def, int car, int pos)
    {
        var cells = new List<int>(def.Lengths[car]);
        bool h = def.IsHorizontal[car];
        int fixedAx = def.FixedAxis[car];
        for (int o = 0; o < def.Lengths[car]; o++)
        {
            int x = h ? pos + o : fixedAx;
            int y = h ? fixedAx : pos + o;
            cells.Add(y * def.Width + x);
        }
        return cells;
    }
}
