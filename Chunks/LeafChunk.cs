using System;
using System.Collections.Generic;

namespace SaperMultiplayer.Chunks;

internal class LeafChunk : ChunkBase
{
    // Constants for chunk configuration
    public const int SIZE = 32;
    public const int BLOCKS_PER_ROW = SIZE >> 3; // 32 / 8
    public const int BLOCK_BITS = 3; // 2^3 = 8 (BoolArr8 size)
    public const int BLOCK_MASK = (1 << BLOCK_BITS) - 1; // 0..7 (BollArr8's mask)

    // Variables
    private readonly BoolArr8[] _blocks;
    private readonly HashSet<(int x, int y)> _wrongFlags = new();
    private readonly Action<int, int> _filledCallback;

    // Consturctor
    public LeafChunk(int worldX, int worldY, Action<int, int> filledCallback, bool alreadyFilled = false)
    {
        Size = SIZE;
        WorldX = worldX;
        WorldY = worldY;

        _blocks = new BoolArr8[Size * BLOCKS_PER_ROW];
        _filledCallback = filledCallback;

        if (alreadyFilled)
        {
            new Span<BoolArr8>(_blocks).Fill(BoolArr8.AllTrue);
        }

    }

    public override int this[int x, int y]
    {
        get
        {
            if (!CheckBounds(x, y))
            {
                throw new ArgumentOutOfRangeException("Coordinates are out of chunk bounds.");
            }

            int localX = x - WorldX;
            int localY = y - WorldY;

            if (_wrongFlags.Contains((localX, localY)))
            {
                return 2; // Return 2 for wrong flags
            }

            // Calculating index of the BoolArr8 block:
            // Y * 4 + (X / 8)
            int blockIndex = (localY * BLOCKS_PER_ROW) + (localX >> BLOCK_BITS);

            return _blocks[blockIndex][localX & BLOCK_MASK] ? 1 : 0;
        }
        set
        {
            if (!CheckBounds(x, y))
            {
                throw new ArgumentOutOfRangeException("Coordinates are out of chunk bounds.");
            }


            int localX = x - WorldX;
            int localY = y - WorldY;

            if (value == 2)
            {
                _wrongFlags.Add((localX, localY));
                return;
            }
            else
            {
                _wrongFlags.Remove((localX, localY));
            }

            // Calculating index of the BoolArr8 block:
            // Y * 4 + (X / 8)
            int blockIndex = (localY * BLOCKS_PER_ROW) + (localX >> BLOCK_BITS);

            _blocks[blockIndex][localX & BLOCK_MASK] = value == 1;

            // Check if the block is now fully filled
            if (value == 1 && _blocks[blockIndex].IsAllTrue)
            {
                foreach (var block in _blocks)
                {
                    if (!block.IsAllTrue)
                        return; // Not all blocks are filled
                }

                // All blocks are filled, trigger the callback
                _filledCallback.Invoke(WorldX, WorldY);
            }
        }
    }

    public override ChunkState GetChunkState()
    {
        return new ChunkState(Size, WorldX, WorldY, _blocks);
    }

    // setter for a player joining mid-game for sync with the game
    public void SetBlocks(BoolArr8[] data)
    {
        for (int i = 0; i < data.Length && i < _blocks.Length; i++)
        {
            _blocks[i] = data[i];
        }
    }
}