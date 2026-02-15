using Microsoft.Xna.Framework;
using SaperMultiplayer.Chunks;
using SaperMultiplayer.Enums;
using SaperMultiplayer.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace SaperMultiplayer.Multiplayer;

internal class NetworkManager
{
    // =============================================== Variables ===============================================
    private readonly object _sendLock = new();

    // Fields
    private TcpClient _client;
    private NetworkStream _stream;
    private BinaryReader _reader;
    private BinaryWriter _writer;
    private Thread _receiveThread;

    // Properties
    public bool IsConnected => _client != null && _client.Connected;
    public bool ServerFirstClickDone { get; private set; }
    public bool ServerGameInProgress { get; private set; }
    public uint ServerCurrentSeed { get; private set; }
    public int ServerStartX { get; private set; }
    public int ServerStartY { get; private set; }

    // =============================================== Events ===============================================
    public event Action<int, int, bool> OnPlayerClickReceived; // clickX, clickY, isLeft (Mouse button)
    public event Action<uint> OnGameResetReceived; // resetSeed

    public event Action<uint, int, int> OnGameStarted; // Seed, StartX, StartY
    public event Action<List<LobbyPlayer>> OnLobbyUpdated; // list of players in the lobby
    public event Action<int, Vector2> OnCursorMoved; // playerId, new cursor position as Vector2

    public event Action OnDisconnected;
    public event Action<uint, int, int, uint, List<ChunkState>> OnBoardSyncReceived; // Seed, StartX, StartY, Points, List of chunk states for board sync



    // =============================================== Methods ===============================================
    public void Connect(string ip, int port, string playerName)
    {
        try
        {
            _client = new TcpClient(ip, port);
            _stream = _client.GetStream();
            _reader = new BinaryReader(_stream);
            _writer = new BinaryWriter(_stream);

            // Handshake
            _writer.Write((byte)PacketType.JoinRequest);
            _writer.Write(playerName);

            // Receiver thread
            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true
            };
            _receiveThread.Start();
        }
        catch (Exception ex)
        {
            throw new ConnectionErrorException("Could not connect to host: " + ex.Message);
        }
    }

    public void Close()
    {
        _client?.Close();
        _receiveThread?.Join(100);
    }

    private void ReceiveLoop()
    {
        try
        {
            while (IsConnected)
            {
                byte packetType = _reader.ReadByte();
                switch ((PacketType)packetType)
                {
                    case PacketType.LobbyState:
                        HandleLobbyState();
                        break;

                    case PacketType.GameStart:
                        HandleGameStart();
                        break;

                    case PacketType.PlayerClick:
                        int clickX = _reader.ReadInt32();
                        int clickY = _reader.ReadInt32();
                        bool isLeft = _reader.ReadBoolean();
                        OnPlayerClickReceived?.Invoke(clickX, clickY, isLeft);
                        break;

                    case PacketType.GameReset:
                        uint resetSeed = _reader.ReadUInt32();
                        OnGameResetReceived?.Invoke(resetSeed);
                        break;

                    case PacketType.PlayerCursor:
                        int pId = _reader.ReadInt32();
                        float px = _reader.ReadSingle();
                        float py = _reader.ReadSingle();
                        OnCursorMoved?.Invoke(pId, new Vector2(px, py));
                        break;

                    case PacketType.FullBoardSync:
                        HandleFullBoardSync();
                        break;
                }
            }
        }
        catch
        {
            OnDisconnected?.Invoke();
        }
    }


    // Packet handlers
    private void HandleLobbyState()
    {
        int count = _reader.ReadInt32();
        var players = new List<LobbyPlayer>();

        for (int i = 0; i < count; i++)
        {
            players.Add(new LobbyPlayer
            {
                Id = _reader.ReadInt32(),
                Name = _reader.ReadString(),
                CursorColor = new Color(_reader.ReadUInt32()),
                IsHost = _reader.ReadBoolean()
            });
        }

        ServerGameInProgress = _reader.ReadBoolean();
        ServerCurrentSeed = _reader.ReadUInt32();

        OnLobbyUpdated?.Invoke(players);
    }

    private void HandleGameStart()
    {
        uint seed = _reader.ReadUInt32();
        int startX = _reader.ReadInt32();
        int startY = _reader.ReadInt32();
        OnGameStarted?.Invoke(seed, startX, startY);
    }

    private void HandleFullBoardSync()
    {
        uint seed = _reader.ReadUInt32();
        int startX = _reader.ReadInt32();
        int startY = _reader.ReadInt32();
        bool isFirstClickDone = _reader.ReadBoolean();
        uint points = _reader.ReadUInt32();
        int count = _reader.ReadInt32();

        this.ServerCurrentSeed = seed;
        this.ServerStartX = startX;
        this.ServerStartY = startY;
        this.ServerFirstClickDone = isFirstClickDone;

        List<ChunkState> states = new List<ChunkState>();
        for (int i = 0; i < count; i++)
        {
            states.Add(ReadChunkState());
        }

        OnBoardSyncReceived?.Invoke(seed, startX, startY, points, states);
    }


    // Packet senders
    private void SendPacketSafe(Action action)
    {
        if (!IsConnected)
        {
            return;
        }

        lock (_sendLock)
        {
            try
            {
                action();
                _writer.Flush(); // force sending
            }
            catch { }
        }
    }

    public void SendClick(int x, int y, bool isLeftClick)
    {
        SendPacketSafe(() => {
            _writer.Write((byte)PacketType.PlayerClick);
            _writer.Write(x);
            _writer.Write(y);
            _writer.Write(isLeftClick);
        });
    }

    public void SendCursorPosition(Vector2 worldPos)
    {
        SendPacketSafe(() => {
            _writer.Write((byte)PacketType.PlayerCursor);
            _writer.Write(worldPos.X);
            _writer.Write(worldPos.Y);
        });
    }

    public void SendColorChangeRequest()
    {
        SendPacketSafe(() => {
            _writer.Write((byte)PacketType.ColorChangeRequest);
        });
    }


    // Helpers
    private ChunkState ReadChunkState()
    {
        int x = _reader.ReadInt32();
        int y = _reader.ReadInt32();
        int size = _reader.ReadInt32();
        bool hasSubChunks = _reader.ReadBoolean();

        BoolArr8[] data;
        ChunkState[] subChunks = null;

        if (hasSubChunks)
        {
            // ControlledChunk -- partial revealed
            data = new BoolArr8[2];
            data[0] = new BoolArr8(_reader.ReadByte());
            data[1] = new BoolArr8(_reader.ReadByte());

            subChunks = new ChunkState[16];
            int activeCount = _reader.ReadInt32();
            for (int i = 0; i < activeCount; i++)
            {
                byte index = _reader.ReadByte();
                subChunks[index] = ReadChunkState();
            }
        }
        else if (size == 32)
        {
            // LeafChunk
            int len = _reader.ReadInt32();
            data = new BoolArr8[len];
            for (int i = 0; i < len; i++)
            {
                data[i] = new BoolArr8(_reader.ReadByte());
            }
        }
        else
        {
            // ControlledChunk -- fully revealed or fully hidden
            data = new BoolArr8[1];
            data[0] = new BoolArr8(_reader.ReadByte());
        }

        return new ChunkState(size, x, y, data, subChunks);
    }
}