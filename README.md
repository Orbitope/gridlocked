# Gridlocked: Rush Hour Puzzle Engine & Visualizer

A high-performance, mathematically optimized recreation of the classic sliding car puzzle game "Rush Hour", built entirely in Unity 6000. 

Originally starting as a simple OOP-based Breadth-First Search (BFS) prototype in 2023, the project has since evolved into a heavily optimized, data-oriented puzzle engine capable of generating, solving, and rendering complex state-space graphs in real-time.

## Features & Architecture

### 1. The Bitboard Solver
The core of the engine has been refactored away from slow Object-Oriented deep-copies into a hyper-optimized **64-bit Bitboard**.
- The entire 6x6 puzzle state (up to 16 cars, their lengths, orientations, and positions) is compressed into a single `ulong`.
- State transitions and validation are handled via zero-allocation bitwise operations.
- The BFS algorithm uses a `HashSet<ulong>` Transposition Table to instantly prune redundant branches and infinite loops.
- **Performance**: The solver can completely enumerate and solve the entire reachable state space (~100,000 states for complex puzzles) in under 5 milliseconds on a single thread.

### 2. GPU-Accelerated Graph Visualization
Instead of relying on external web tools or locking up the Unity Editor with heavy UI elements, the game features a dedicated **3D Runtime Graph Scene**.
- While playing any puzzle, you can click "Graph" to instantly compute the entire mathematical tree of future moves.
- The engine dynamically spawns 3D physical nodes and connecting lines in world space.
- You can pan and zoom through the mathematical structure of the puzzle to visually identify "chokepoints" and critical paths, all rendered smoothly at 60 FPS by your GPU.

### 3. UI Toolkit Frontend
The entire game interface is built using Unity's modern UI Toolkit (UXML/USS).
- Features a sleek, responsive dark-mode menu system.
- Includes a dynamic Level Select screen that reads from a pre-generated ScriptableObject `LevelDatabase`.
- Tracks player progression, marking best move counts natively via `SaveManager`.
- Provides an active HUD during gameplay with **Hint**, **Undo**, and **Restart** functionality.

### 4. Custom Puzzle Generator
Don't want to play the curated levels? The engine includes a live Puzzle Generator.
- Specify the number of cars and the minimum required moves to solve.
- The engine uses a "Density-First Brute Force" approach, generating tightly packed random boards and using the lightning-fast Bitboard solver to throw away thousands of invalid boards per second until it finds one that matches your exact difficulty requirements.

## How to Play

1. Open the project in **Unity 6000.3.x**.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press **Play**. 
4. Select a pre-generated level from the Main Menu, or hop into the Custom Generator to spin up a new challenge.
5. Click and drag cars along their fixed axes to free the primary red car (Car 0) so it can exit the right side of the board!

## Future Concepts
- **Isomorphism Detection**: Implementing Canonical Hashing to detect mathematically identical boards that simply have swapped color skins.
- **Advanced Heuristics**: Utilizing the raw state-space data to algorithmically grade the "fun factor" of puzzles based on Deception Ratio and Aha! Bottlenecks.

---
## License

This project is licensed under the [Creative Commons Attribution-NonCommercial 4.0 International License (CC BY-NC 4.0)](https://creativecommons.org/licenses/by-nc/4.0/). 
You are free to share and adapt the material, provided you give appropriate credit and do not use the material for commercial purposes. See the `LICENSE` file for details.
