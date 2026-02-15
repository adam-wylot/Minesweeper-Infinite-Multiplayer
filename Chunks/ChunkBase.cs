namespace SaperMultiplayer.Chunks;

internal abstract class ChunkBase
{
    public int WorldX { get; protected set;  }
    public int WorldY { get; protected set; }

    public int Size { get; protected set; }

    public abstract int this[int x, int y] { get; set; }
    
    public abstract ChunkState GetChunkState();

    protected bool CheckBounds(int x, int y)
    {
        return x >= WorldX && x < WorldX + Size && y >= WorldY && y < WorldY + Size;
    }
}
