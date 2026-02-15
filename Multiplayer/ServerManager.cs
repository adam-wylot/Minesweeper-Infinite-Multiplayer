using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SaperMultiplayer.Multiplayer;

internal class ServerManager
{
    // =============================================== Variables ===============================================
    // Fields
    private TcpListener _listener;
    private List<RemoteClient> _clients = new();
    private MinesweeperGame _game;
    private bool _isRunning;

    // Properties
    public bool IsGameRunning { get; private set; }
    public uint CurrentSeed { get; private set; }


    // =============================================== Constructor ===============================================
    public ServerManager(MinesweeperGame game)
    {
        _game = game;
    }


    // =============================================== Methods ===============================================
    public void Start(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _isRunning = true;

        Thread acceptThread = new(AcceptLoop)
        {
            IsBackground = true
        };
        acceptThread.Start();
    }

    public void Stop()
    {
        _isRunning = false;
        IsGameRunning = false;
        _listener?.Stop();

        lock (_clients)
        {
            foreach (var c in _clients)
            {
                try
                {
                    c.Close();
                }
                catch { }
            }
            _clients.Clear();
        }
    }

    private void AcceptLoop()
    {
        try
        {
            while (_isRunning)
            {
                TcpClient tcpClient = _listener.AcceptTcpClient();
                var client = new RemoteClient(tcpClient, this);
                // will add to _clients in OnPlayerJoined after receiving JoinRequest
            }
        }
        catch { }
    }

    public bool FirstClickPerformed()
    {
        return _game.GetBoard().PerformedFirstClick();
    }


    // =============================================== Client Handlers ===============================================
    public void OnPlayerJoined(RemoteClient client, string name)
    {
        // Create a new LobbyPlayer for this client
        var newPlayer = new LobbyPlayer
        {
            Id = _clients.Count + 1,
            Name = name,
            CursorColor = _game.GetNextAvailableColor(),
            IsHost = false
        };

        client.Player = newPlayer;

        lock (_clients)
        {
            _clients.Add(client);
        }

        // Adding player to the game's lobby list on the main thread
        _game.GetLobbyPlayers().Add(newPlayer);

        if (IsGameRunning)
        {
            // the game is already running, so we need to sync this player
            _game.EnqueueAction(() => {
                var board = _game.GetBoard();
                var chunksStates = _game.GetBoard().GetData().GetTouchedChunks();
                var (sX, sY) = _game.GetBoard().GetFirstClickPos();
                client.SafeSend(() => client.SendFullBoardSync(CurrentSeed, sX, sY, board.Points, chunksStates));
            });
        }

        // Send updated lobby state to all clients
        BroadcastLobbyState();
    }

    public void OnClientDisconnected(RemoteClient client)
    {
        lock (_clients)
        {
            _clients.Remove(client);
        }

        if (client.Player != null)
        {
            _game.EnqueueAction(() => {
                var allPlayers = _game.GetLobbyPlayers();
                allPlayers.RemoveAll(p => p.Id == client.Player.Id);
                BroadcastLobbyState();
            });
        }
    }

    public void OnClientClick(RemoteClient client, int x, int y, bool isLeft)
    {
        // Host make the move on the main thread
        _game.EnqueueAction(() => {
            _game.HandleRemoteClick(x, y, isLeft);
        });

        // Broadcast to others
        BroadcastClick(x, y, isLeft, client);
    }

    public void OnClientCursorMoved(RemoteClient client, Vector2 pos)
    {
        if (client.Player == null)
        {
            return;
        }
        client.Player.CursorWorldPos = pos;

        // Broadcast to others
        lock (_clients)
        {
            foreach (var cl in _clients)
            {
                if (cl == client) continue;
                cl.SafeSend(() => cl.SendCursorUpdate(client.Player.Id, pos));
            }
        }
    }

    public void OnColorChangeRequest(RemoteClient client)
    {
        _game.EnqueueAction(() => {
            Color nextColor = _game.GetNextColorInCycle(client.Player.CursorColor);
            client.Player.CursorColor = nextColor;

            // Update the color in the lobby
            var pInLobby = _game.GetLobbyPlayers().Find(p => p.Id == client.Player.Id);
            if (pInLobby != null)
            {
                pInLobby.CursorColor = nextColor;
            }

            BroadcastLobbyState();
            _game.EnqueueAction(() => _game.CreateLobbyButtons());
        });
    }


    // =============================================== Broadcast Methods ===============================================
    public void BroadcastLobbyState()
    {
        var allPlayers = _game.GetLobbyPlayers();

        lock (_clients)
        {
            foreach (var client in _clients)
            {
                client.SafeSend(() => client.SendLobbyState(allPlayers, IsGameRunning, CurrentSeed));
            }
        }
    }

    public void BroadcastGameStart(uint seed, int startX, int startY)
    {
        IsGameRunning = true;
        CurrentSeed = seed;

        lock (_clients)
        {
            foreach (var client in _clients)
            {
                client.SafeSend(() => client.SendGameStart(seed, startX, startY));
            }
        }

        BroadcastLobbyState();
    }

    public void BroadcastClick(int x, int y, bool isLeft, RemoteClient excludeClient = null)
    {
        lock (_clients)
        {
            foreach (var client in _clients)
            {
                if (client == excludeClient) continue;
                client.SafeSend(() => client.SendClick(x, y, isLeft));
            }
        }
    }

    public void BroadcastReset(uint seed)
    {
        CurrentSeed = seed;
        IsGameRunning = true;

        lock (_clients)
        {
            foreach (var client in _clients)
            {
                client.SafeSend(() => client.SendReset(seed));
            }
        }
    }

    public void BroadcastHostCursor(Vector2 pos)
    {
        lock (_clients)
        {
            foreach (var client in _clients)
            {
                client.SafeSend(() => client.SendCursorUpdate(0, pos));
            }
        }
    }
}