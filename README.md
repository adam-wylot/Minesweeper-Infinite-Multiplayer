# Minesweeper Infinite Multiplayer

A more ambitious version of the classic Minesweeper with a cooperative multiplayer mode, created in **C#** using the **MonoGame** framework. Unlike the traditional game, this project offers an infinite, procedurally generated board based on a chunk system.

## Features

*   **Infinite World**: Play on a map that expands with your exploration, managed by an efficient data structure.
*   **Multiplayer Co-op**: Host a server or join friends via IP to clear the minefield together.
*   **Real-time Synchronization**: See other players' cursors and their progress live.
*   **Dynamic Camera**: Smooth zooming and panning of the view, making it easier to navigate the gigantic grid.
*   **Seed-based Generator**: Utilizes the SplitMix64 algorithm, which ensures deterministic and fair mine placement.

## Controls

### Mouse
*   **LMB**: Reveal a tile.
*   **RMB**: Place/remove a flag.
*   **Scroll**: Pan up/down.
*   **Shift + Scroll**: Pan left/right.
*   **Ctrl + Scroll**: Zoom in and out.

### Keyboard
*   **WASD**: Move the camera.
*   **ESC**: Instant return to the main menu from any game screen.

## Game Mechanics
*   **Lobby**: Before starting, players can choose their unique cursor color.
*   **First Click Protection**: The first move in a new game will never hit a mine and always opens a safe area (min. 3x3).
*   **Chording**: Left-clicking on a revealed number (if the number of surrounding flags matches) automatically reveals adjacent, unflagged tiles.
*   **Reset**: The host can reset the game at any time.

## Requirements
*   .NET 6.0 SDK or newer
*   MonoGame 3.8.1+ libraries

## Project Structure
*   `Board` & `BoardData`: Logic of the infinite grid and tile state management.
*   `Chunks`: Memory optimization system. Only revealed tiles are stored in memory. The map is generated on the fly.
*   `Multiplayer`: `ServerManager` and `NetworkManager` modules handling TCP communication.
*   `Camera`: Handling view transformations and zoom.

---

### How to run
1. Clone the repository.
2. Open the project.
3. Ensure no assets are missing and the files in `Content.mgcb` are built (Pipeline Tool).
4. Run the project.
