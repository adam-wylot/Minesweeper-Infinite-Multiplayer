using System;

#nullable enable
namespace SaperMultiplayer.Chunks;

internal class ControlledChunk : ChunkBase
{
    private ChunkBase?[] _chunks;
    private BoolArr8[] _isChunkFilled;
    private readonly int _subChunkCount;
    private readonly Action<int, int>? _filledCallback;

    public ControlledChunk(int x, int y, int size, Action<int, int>? filledCallback, bool alreadyFilled = false)
    {
        if (size % ChunkState.CHUNKS_PER_ROW != 0)
        {
            throw new ArgumentException($"Size must be divisible by {ChunkState.CHUNKS_PER_ROW}.");
        }

        WorldX = x;
        WorldY = y;
        Size = size;

        _subChunkCount = ChunkState.CHUNKS_PER_ROW * ChunkState.CHUNKS_PER_ROW;
        _chunks = new ChunkBase[_subChunkCount];
        _filledCallback = filledCallback;

        // Allocate enough BoolArr8 entries to hold one bit per sub-chunk.
        int boolArrLength = (_subChunkCount + 7) / 8; // ceil(_subChunkCount /  8)
        _isChunkFilled = new BoolArr8[boolArrLength];

        if (alreadyFilled)
        {
            new Span<BoolArr8>(_isChunkFilled).Fill(BoolArr8.AllTrue);
        }

    }

    public override int this[int x, int y]
    {
        get
        {
            if (!CheckBounds(x, y))
                throw new ArgumentOutOfRangeException("Coordinates are out of chunk bounds.");

            int localX = x - WorldX;
            int localY = y - WorldY;
            int half = Size / ChunkState.CHUNKS_PER_ROW;
            int chunkIndex = (localY / half) * ChunkState.CHUNKS_PER_ROW + (localX / half);

            var chunk = _chunks[chunkIndex];
            if (chunk == null)
                return _isChunkFilled[chunkIndex >> 3][chunkIndex % 8] ? 1 : 0;

            return chunk[x, y];
        }
        set
        {
            if (!CheckBounds(x, y))
                throw new ArgumentOutOfRangeException("Coordinates are out of chunk bounds.");

            int localX = x - WorldX;
            int localY = y - WorldY;
            int half = Size / ChunkState.CHUNKS_PER_ROW;
            int chunkIndex = (localY / half) * ChunkState.CHUNKS_PER_ROW + (localX / half);

            var chunk = _chunks[chunkIndex];
            if (chunk == null)
            {
                InitializeChunk(chunkIndex);
                chunk = _chunks[chunkIndex];
            }

            chunk![x, y] = value;
        }
    }

    public override ChunkState GetChunkState()
    {
        if (IsSubChunksFilled())
        {
            return new ChunkState(Size, WorldX, WorldY, [new BoolArr8(0xFF)]);
        }

        var subStates = new ChunkState?[_subChunkCount];
        for (int i = 0; i < _subChunkCount; i++)
        {
            if (_chunks[i] == null)
            {
                subStates[i] = null;
                continue;
            }

            subStates[i] = _chunks[i]!.GetChunkState();
        }

        return new ChunkState(Size, WorldX, WorldY, _isChunkFilled, subStates);
    }

    private void InitializeChunk(int chunkIndex)
    {
        int chunkX = WorldX + (chunkIndex % ChunkState.CHUNKS_PER_ROW) * (Size / ChunkState.CHUNKS_PER_ROW);
        int chunkY = WorldY + (chunkIndex / ChunkState.CHUNKS_PER_ROW) * (Size / ChunkState.CHUNKS_PER_ROW);

        bool filled = false;
        if (_isChunkFilled[chunkIndex >> 3][chunkIndex % 8])
        {
            filled = true;
        }

        if (Size / ChunkState.CHUNKS_PER_ROW == LeafChunk.SIZE)
        {
            // Next chunk is a leaf chunk
            _chunks[chunkIndex] = new LeafChunk(chunkX, chunkY, OnSubChunkFilled, filled);
        }
        else
        {
            // Next chunk is another controlled chunk
            _chunks[chunkIndex] = new ControlledChunk(chunkX, chunkY, Size / ChunkState.CHUNKS_PER_ROW, OnSubChunkFilled, filled);
        }

        _isChunkFilled[chunkIndex >> 3][chunkIndex % 8] = false;
    }

    private void OnSubChunkFilled(int chunkX, int chunkY)
    {
        int localX = chunkX - WorldX;
        int localY = chunkY - WorldY;

        int chunkIndex = (localY / (Size / ChunkState.CHUNKS_PER_ROW)) * ChunkState.CHUNKS_PER_ROW + (localX / (Size / ChunkState.CHUNKS_PER_ROW));

        _isChunkFilled[chunkIndex >> 3][chunkIndex % 8] = true;
        _chunks[chunkIndex] = null; // Free memory

        // Check if all sub-chunks are filled
        if (IsSubChunksFilled())
        {
            _filledCallback?.Invoke(WorldX, WorldY);
        }
    }

    private bool IsSubChunksFilled()
    {
        int fullBytes = _subChunkCount / 8;
        int remBits = _subChunkCount % 8;

        for (int i = 0; i < fullBytes; i++)
        {
            if (!_isChunkFilled[i].IsAllTrue)
                return false;
        }

        if (remBits > 0)
        {
            var last = _isChunkFilled[fullBytes];
            for (int b = 0; b < remBits; b++)
            {
                if (!last[b])
                    return false;
            }
        }

        return true;
    }


    // setters for a player joining mid-game for sync with the game
    public void SetSubChunk(int index, ChunkBase? chunk)
    {
        _chunks[index] = chunk;
    }

    public void SetFilledMask(int blockIndex, byte rawData)
    {
        if (blockIndex < _isChunkFilled.Length)
        {
            _isChunkFilled[blockIndex].RawData = rawData;
        }
    }
}