using SaperMultiplayer.Chunks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SaperMultiplayer;

internal class BoardData
{
    // Variables
    private Dictionary<(int x, int y), ControlledChunk> _touchedChunks = new();


    public int this[int x, int y]
    {
        get
        {
            var (chunkX, chunkY) = GetChunkCoordinates(x, y);
            if (_touchedChunks.TryGetValue((chunkX, chunkY), out var chunk))
            {
                return chunk[x, y];
            }

            return 0;
        }
        set
        {
            var (chunkIndexX, chunkIndexY) = GetChunkCoordinates(x, y);
            if (!_touchedChunks.TryGetValue((chunkIndexX, chunkIndexY), out var chunk))
            {
                int worldX = chunkIndexX * ChunkState.CHUNK_SIZE;
                int worldY = chunkIndexY * ChunkState.CHUNK_SIZE;

                chunk = new ControlledChunk(
                    worldX,
                    worldY,
                    ChunkState.CHUNK_SIZE,
                    null
                );

                _touchedChunks[(chunkIndexX, chunkIndexY)] = chunk;
            }
            chunk[x, y] = value;
        }
    }


    // Methods
    private static (int chunkX, int chunkY) GetChunkCoordinates(int x, int y)
    {
        int chunkIndexX = Math.DivRem(x, ChunkState.CHUNK_SIZE, out int remX);
        if (remX < 0) chunkIndexX--;

        int chunkIndexY = Math.DivRem(y, ChunkState.CHUNK_SIZE, out int remY);
        if (remY < 0) chunkIndexY--;

        return (chunkIndexX, chunkIndexY);
    }
    public ChunkState GetChunkState(int x, int y)
    {
        var (chunkIndexX, chunkIndexY) = GetChunkCoordinates(x, y);
        if (_touchedChunks.TryGetValue((chunkIndexX, chunkIndexY), out var chunk))
        {
            return chunk.GetChunkState();
        }

        int worldX = chunkIndexX * ChunkState.CHUNK_SIZE;
        int worldY = chunkIndexY * ChunkState.CHUNK_SIZE;
        return new ChunkState(ChunkState.CHUNK_SIZE, worldX, worldY, new BoolArr8[] { new BoolArr8() });
    }
    public List<ChunkState> GetTouchedChunks()
    {
        return _touchedChunks.Values.Select(c => c.GetChunkState()).ToList();
    }


    // Setters methods to sync with mp game
    public void ApplyChunkState(ChunkState state)
    {
        var (chunkIndexX, chunkIndexY) = GetChunkCoordinates(state.X, state.Y);

        ChunkBase reconstructed = ReconstructFromState(state);
        if (reconstructed is ControlledChunk cc)
        {
            _touchedChunks[(chunkIndexX, chunkIndexY)] = cc;
        }
    }
    private ChunkBase ReconstructFromState(ChunkState state)
    {
        if (state.SubChunkStates == null)
        {
            if (state.Size == 32)
            {
                // LeafChunk
                var leaf = new LeafChunk(state.X, state.Y, null);
                leaf.SetBlocks(state.Data);
                return leaf;
            }
            else
            {
                // ControlledChunk -- full revealed or full hidden
                return new ControlledChunk(state.X, state.Y, state.Size, null, state.Data[0].IsAllTrue);
            }
        }
        else
        {
            // ControlledChunk -- partial revealed
            var cc = new ControlledChunk(state.X, state.Y, state.Size, null);

            for (int i = 0; i < state.Data.Length; i++)
            {
                cc.SetFilledMask(i, state.Data[i].RawData);
            }

            for (int i = 0; i < 16; i++)
            {
                if (state.SubChunkStates[i] != null)
                {
                    cc.SetSubChunk(i, ReconstructFromState(state.SubChunkStates[i]));
                }
            }
            return cc;
        }
    }
}
