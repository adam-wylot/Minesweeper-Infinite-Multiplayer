namespace SaperMultiplayer.Chunks;

#nullable enable
internal class ChunkState
{
    public const int CHUNK_SIZE = 512;
    public const int CHUNKS_PER_ROW = 4;

    public int Size { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public BoolArr8[] Data { get; private set; }
    public ChunkState?[]? SubChunkStates { get; private set; }


    public ChunkState(int size, int x, int y, BoolArr8[] data, ChunkState?[]? subChunkStates = null)
    {
        Size = size;
        X = x;
        Y = y;
        Data = data;
        SubChunkStates = subChunkStates;
    }
}
