using System.Collections.Generic;

namespace Gridlocked
{
    /// <summary>
    /// Bridges the lab Puzzle to the rest of gridlocked-game.
    /// GraphEdges is populated by HexGameManager after Analysis runs,
    /// using the same Dictionary<ulong, List<ulong>> shape that GraphManager
    /// already consumes from BitboardSolver.
    /// </summary>
    public class HexPuzzleDefinition
    {
        public HexBoard Board;
        public Puzzle Puzzle;
        public Dictionary<ulong, List<ulong>> GraphEdges = new();
    }
}
