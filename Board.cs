using SaperMultiplayer.Chunks;
using System.Collections.Generic;

namespace SaperMultiplayer;

internal class Board
{
    //  ========================================== Variables ==========================================
    private BoardData _data;
    private MineGenerator _mineGenerator;
    private bool _firstClick;

    // Properties
    public uint Points { get; private set; }
    public bool Lost { get; private set; }
    public (int x, int y) LostXY { get; private set; }
    public bool IsHost { get; set; }
    public uint Seed { get; private set; }

    // ========================================== Getters ==========================================
    public BoardData GetData() => _data;
    public (int x, int y) GetFirstClickPos() => (_mineGenerator.StartX, _mineGenerator.StartY);
    public bool PerformedFirstClick() => !_firstClick;

    // ========================================== Constructor ==========================================
    public Board(uint seed)
    {
        Seed = seed;
        _data = new BoardData();
        _mineGenerator = new MineGenerator(seed);
        _firstClick = true;
        Points = 0;
        Lost = false;
        LostXY = (0, 0);
        IsHost = false;
    }

    // ========================================== Methods ==========================================
    // Click actions
    public void LeftClickAt(int x, int y)
    {
        if (Lost)
        {
            return;
        }

        if (_firstClick)
        {
            // Ensure first click is never a mine
            _mineGenerator.FirstClickAt(x, y);
            _firstClick = false;
        }

        if (_data[x, y] > 0)
        {
            // revealed cell
            uint n = _mineGenerator.CountNeighborMines(x, y);

            if (!IsFlagged(x, y) && n > 0)
            {
                // Reveal neighbors if flagged count matches the number on the cell
                uint count = 0;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;

                        if (IsFlagged(x + dx, y + dy))
                        {
                            count++;
                        }
                    }
                }

                if (count == n)
                {
                    // Reveal neighbors
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            if (!IsFlagged(x + dx, y + dy))
                            {
                                if (_mineGenerator.IsMine(x + dx, y + dy))
                                {
                                    // Clicked on a mine -- lose condition
                                    Lost = true;
                                    LostXY = (x + dx, y + dy);
                                    continue;
                                }

                                RevealCellAndNeighbors(x + dx, y + dy);
                            }
                        }
                    }
                }
            }

            // Already revealed or flagged, do nothing
            return;
        }

        else if (_mineGenerator.IsMine(x, y))
        {
            // Clicked on a mine
            Lost = true;
            LostXY = (x, y);
            return;
        }

        // Reveal the cell and neighbors if safe
        RevealCellAndNeighbors(x, y);
    }

    public void RightClickAt(int x, int y)
    {
        if (_firstClick || Lost)
        {
            return;
        }
        
        int tmp = _data[x, y];
        if (tmp == 1 && !_mineGenerator.IsMine(x, y))
        {
            // Already revealed, do nothing
            return;
        }

        switch (tmp)
        {
            case 0:
                if (_mineGenerator.IsMine(x, y))
                {
                    // Correct flag, treat as revealed
                    _data[x, y] = 1;
                }
                else
                {
                    // Incorrect flag -- set 2 to indicate wrong flag
                    _data[x, y] = 2;
                }

                break;

            case 1:
            case 2:
                // Remove flag
                _data[x, y] = 0;
                break;
        }
    }


    // State queries
    public bool IsRevealed(int x, int y)
    {
        return _data[x, y] > 0;
    }

    public bool IsFlagged(int x, int y)
    {
        int tmp = _data[x, y];
        return tmp == 2 || (tmp == 1 && _mineGenerator.IsMine(x, y));
    }

    public bool IsMine(int x, int y)
    {
        return _mineGenerator.IsMine(x, y);
    }

    public ChunkState GetChunkStateAt(int x, int y)
    {
        return _data.GetChunkState(x, y);
    }

    public uint GetNumberOfNeighborMines(int x, int y)
    {
        return (uint)_mineGenerator.CountNeighborMines(x, y);
    }


    // Board actions
    private void RevealCellAndNeighbors(int x, int y)
    {
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((x, y));

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            if (_data[cx, cy] > 0)
            {
                continue; // Already revealed
            }

            _data[cx, cy] = 1; // Reveal cell
            Points++;

            // If no neighboring mines, reveal neighbors
            if (_mineGenerator.CountNeighborMines(cx, cy) == 0)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;
                        queue.Enqueue((cx + dx, cy + dy));
                    }
                }
            }
        }

    }

    // Sync methods for mp
    public void SyncFirstClick(int x, int y, bool wasFirstClickDone)
    {
        _mineGenerator.FirstClickAt(x, y);
        _firstClick = !wasFirstClickDone;
    }

    public void SyncPoints(uint points)
    {
        Points = points;
    }
}
