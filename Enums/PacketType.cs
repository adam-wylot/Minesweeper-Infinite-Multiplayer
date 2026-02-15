namespace SaperMultiplayer.Enums;

public enum PacketType : byte
{
    JoinRequest = 1,
    LobbyState,
    GameStart,
    PlayerClick,
    GameReset,
    PlayerCursor,
    ColorChangeRequest,
    FullBoardSync
}
