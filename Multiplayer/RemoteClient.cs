using Microsoft.Xna.Framework;
using SaperMultiplayer.Chunks;
using SaperMultiplayer.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace SaperMultiplayer.Multiplayer;

internal class RemoteClient
{
    // =============================================== Variables ===============================================
    private readonly object _sendLock = new();

    // Fields
    private BinaryReader _reader;
    private BinaryWriter _writer;
    private ServerManager _server;

    // Properties
    public TcpClient Tcp { get; }
    public LobbyPlayer Player { get; set; }


    // =============================================== Constructor ===============================================
    public RemoteClient(TcpClient tcp, ServerManager server)
    {
        Tcp = tcp;
        _server = server;
        var stream = tcp.GetStream();
        _reader = new BinaryReader(stream);
        _writer = new BinaryWriter(stream);

        new Thread(ReadLoop)
        {
            IsBackground = true
        }.Start();
    }


    // =============================================== Methods ===============================================
    private void ReadLoop()
    {
        try
        {
            while (Tcp.Connected)
            {
                byte type = _reader.ReadByte();
                switch ((PacketType)type)
                {
                    case PacketType.JoinRequest:
                        string name = _reader.ReadString();
                        _server.OnPlayerJoined(this, name);
                        break;

                    case PacketType.PlayerClick:
                        int pX = _reader.ReadInt32();
                        int pY = _reader.ReadInt32();
                        bool isLeft = _reader.ReadBoolean();
                        _server.OnClientClick(this, pX, pY, isLeft);
                        break;

                    case PacketType.PlayerCursor:
                        float cX = _reader.ReadSingle();
                        float cY = _reader.ReadSingle();
                        _server.OnClientCursorMoved(this, new Vector2(cX, cY));
                        break;

                    case PacketType.ColorChangeRequest:
                        if (this.Player != null)
                        {
                            _server.OnColorChangeRequest(this);
                        }
                        break;
                }
            }
        }
        catch
        {
            _server.OnClientDisconnected(this);
        }
    }

    public void Close() => Tcp.Close();

    private void WriteChunkState(ChunkState state)
    {
        _writer.Write(state.X);
        _writer.Write(state.Y);
        _writer.Write(state.Size);

        bool isLeaf = state.Size == 32;
        bool hasSubChunks = state.SubChunkStates != null;

        _writer.Write(hasSubChunks);

        if (hasSubChunks)
        {
            // ControlledChunk -- partial revealed
            _writer.Write(state.Data[0].RawData);
            _writer.Write(state.Data[1].RawData);

            int activeCount = 0;
            for (int i = 0; i < 16; i++)
            {
                if (state.SubChunkStates[i] != null)
                {
                    activeCount++;
                }
            }
            _writer.Write(activeCount);
            
            for (int i = 0; i < 16; i++)
            {
                if (state.SubChunkStates[i] != null)
                {
                    _writer.Write((byte)i);
                    WriteChunkState(state.SubChunkStates[i]);
                }
            }
        }
        else if (isLeaf)
        {
            // LeafChunk
            _writer.Write(state.Data.Length);
            foreach (var block in state.Data)
            {
                _writer.Write(block.RawData);
            }
        }
        else
        {
            // ControlledChunk -- fully revealed or fully hidden
            _writer.Write(state.Data[0].RawData);
        }
    }


    // Senders
    public void SafeSend(Action action)
    {
        lock (_sendLock)
        {
            try
            {
                if (Tcp.Connected)
                {
                    action();
                    _writer.Flush(); // force sending
                }
            }
            catch
            {
                _server.OnClientDisconnected(this);
            }
        }
    }

    public void SendLobbyState(List<LobbyPlayer> players, bool isRunning, uint seed)
    {
        _writer.Write((byte)PacketType.LobbyState);
        _writer.Write(players.Count);
        foreach (var p in players)
        {
            _writer.Write(p.Id);
            _writer.Write(p.Name);
            _writer.Write(p.CursorColor.PackedValue);
            _writer.Write(p.IsHost);
        }
        _writer.Write(isRunning);
        _writer.Write(seed);
    }

    public void SendGameStart(uint seed, int startX, int startY)
    {
        _writer.Write((byte)PacketType.GameStart);
        _writer.Write(seed);
        _writer.Write(startX);
        _writer.Write(startY);
    }

    public void SendClick(int x, int y, bool isLeft)
    {
        _writer.Write((byte)PacketType.PlayerClick);
        _writer.Write(x);
        _writer.Write(y);
        _writer.Write(isLeft);
    }

    public void SendReset(uint seed)
    {
        _writer.Write((byte)PacketType.GameReset);
        _writer.Write(seed);
    }

    public void SendCursorUpdate(int playerId, Vector2 pos)
    {
        _writer.Write((byte)PacketType.PlayerCursor);
        _writer.Write(playerId);
        _writer.Write(pos.X);
        _writer.Write(pos.Y);
    }

    public void SendFullBoardSync(uint seed, int startX, int startY, uint points, List<ChunkState> states)
    {
        bool isFirstClickDone = _server.FirstClickPerformed();

        SafeSend(() => {
            _writer.Write((byte)PacketType.FullBoardSync);
            _writer.Write(seed);
            _writer.Write(startX);
            _writer.Write(startY);
            _writer.Write(isFirstClickDone);
            _writer.Write(points);
            _writer.Write(states.Count);
            foreach (var s in states)
            {
                WriteChunkState(s);
            }
        });
    }
}