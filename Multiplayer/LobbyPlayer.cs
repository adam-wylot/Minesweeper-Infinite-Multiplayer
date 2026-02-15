using Microsoft.Xna.Framework;

namespace SaperMultiplayer.Multiplayer;

public class LobbyPlayer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsHost { get; set; }
    public Vector2 CursorWorldPos { get; set; }
    public Color CursorColor { get; set; }
}