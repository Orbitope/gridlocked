using System.Collections.Generic;

/// <summary>
/// Converts between the standard 36-character Rush Hour board encoding (used by
/// Michael Fogleman's complete 6x6 database) and this project's
/// PuzzleDefinition + PuzzleState.
///
/// Format: 36 chars, row-major, index 0 = top-left.
///   'o' (or '.') = empty, 'x' = wall, 'A' = primary/red car, 'B'-'Z' = others.
///
/// Two hard constraints of the current engine, enforced here:
///   1. PuzzleDefinition has no wall concept  -> boards containing 'x' are rejected.
///   2. BitboardSolver.GOAL_POSITION == 4 assumes the primary car is length 2
///      (it must end on x=4,5) -> boards whose 'A' is not length 2 are rejected.
///
/// Row orientation: the source format has row 0 at the top, while the game
/// renders y=0 at the bottom, so rows are flipped on import for visual parity.
/// This does not affect solvability or any metric — the goal is "primary car at
/// the right edge of its own row" either way.
/// </summary>
public static class RushBoardParser
{
    public const int Size = 6;
    public const int Cells = Size * Size;

    /// <summary>
    /// Parse a 36-char board. Returns false with a human-readable
    /// <paramref name="rejectReason"/> when the board can't be represented.
    /// </summary>
    public static bool TryParse(string board, out PuzzleDefinition def,
                                out PuzzleState state, out string rejectReason)
    {
        def = null;
        state = default;
        rejectReason = null;

        if (string.IsNullOrEmpty(board) || board.Length != Cells)
        {
            rejectReason = $"length != {Cells}";
            return false;
        }
        if (board.IndexOf('x') >= 0 || board.IndexOf('X') >= 0)
        {
            rejectReason = "contains wall";
            return false;
        }

        // Collect the cells of each piece letter, converting to game coords
        // (flip rows: y_game = 5 - srcRow).
        var cells = new Dictionary<char, List<(int x, int y)>>();
        for (int i = 0; i < Cells; i++)
        {
            char c = board[i];
            if (c == 'o' || c == '.' || c == ' ') continue;
            if (c < 'A' || c > 'Z') { rejectReason = $"bad char '{c}'"; return false; }

            int x = i % Size;
            int y = (Size - 1) - (i / Size);
            if (!cells.TryGetValue(c, out var list))
                cells[c] = list = new List<(int, int)>();
            list.Add((x, y));
        }

        if (!cells.ContainsKey('A')) { rejectReason = "no primary 'A'"; return false; }

        // 'A' must be car 0 (BitboardSolver.GOAL_CAR_INDEX); rest in letter order.
        var letters = new List<char> { 'A' };
        foreach (var key in cells.Keys)
            if (key != 'A') letters.Add(key);
        letters.Sort((a, b) => a == 'A' ? -1 : b == 'A' ? 1 : a.CompareTo(b));

        var result = new PuzzleDefinition(letters.Count);
        var positions = new int[letters.Count];

        for (int i = 0; i < letters.Count; i++)
        {
            var list = cells[letters[i]];
            if (list.Count < 2) { rejectReason = $"piece '{letters[i]}' size {list.Count}"; return false; }

            bool sameRow = true, sameCol = true;
            for (int k = 1; k < list.Count; k++)
            {
                if (list[k].y != list[0].y) sameRow = false;
                if (list[k].x != list[0].x) sameCol = false;
            }
            if (sameRow == sameCol) { rejectReason = $"piece '{letters[i]}' not a line"; return false; }

            bool horizontal = sameRow;
            int min = int.MaxValue, max = int.MinValue;
            foreach (var (cx, cy) in list)
            {
                int along = horizontal ? cx : cy;
                if (along < min) min = along;
                if (along > max) max = along;
            }
            // Contiguity: a straight run of exactly list.Count cells.
            if (max - min + 1 != list.Count)
            { rejectReason = $"piece '{letters[i]}' not contiguous"; return false; }

            result.IsHorizontal[i] = horizontal;
            result.Lengths[i]      = list.Count;
            result.FixedAxis[i]    = horizontal ? list[0].y : list[0].x;
            positions[i]           = min;
        }

        // Engine goal check hardcodes a length-2 primary car reaching pos 4.
        if (result.Lengths[0] != 2) { rejectReason = "primary 'A' not length 2"; return false; }
        if (!result.IsHorizontal[0]) { rejectReason = "primary 'A' not horizontal"; return false; }

        var s = new PuzzleState();
        for (int i = 0; i < letters.Count; i++) s.SetCarPos(i, positions[i]);

        def = result;
        state = s;
        return true;
    }

    /// <summary>Render a definition+state back to the 36-char encoding (round-trip test).</summary>
    public static string Format(PuzzleDefinition def, PuzzleState state)
    {
        var grid = new char[Cells];
        for (int i = 0; i < Cells; i++) grid[i] = 'o';

        for (int car = 0; car < def.CarCount; car++)
        {
            char letter = (char)('A' + car);
            int pos = state.GetCarPos(car);
            for (int off = 0; off < def.Lengths[car]; off++)
            {
                int x = def.IsHorizontal[car] ? pos + off : def.FixedAxis[car];
                int y = def.IsHorizontal[car] ? def.FixedAxis[car] : pos + off;
                int srcRow = (Size - 1) - y;            // flip back
                grid[srcRow * Size + x] = letter;
            }
        }
        return new string(grid);
    }

    /// <summary>
    /// Parse one corpus line. Accepts Fogleman's "moves board cluster" form and a
    /// plain "board" form (curated files). moves = -1 when absent.
    /// </summary>
    public static bool TryParseLine(string line, out string board, out int moves)
    {
        board = null; moves = -1;
        if (string.IsNullOrWhiteSpace(line)) return false;
        line = line.Trim();
        if (line.StartsWith("#")) return false;

        var parts = line.Split(new[] { ' ', '\t', ',' },
            System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in parts)
        {
            if (p.Length == Cells) { board = p; break; }
        }
        if (board == null) return false;

        if (parts.Length > 0 && int.TryParse(parts[0], out int m)) moves = m;
        return true;
    }
}
