using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SaperMultiplayer.Chunks;
using SaperMultiplayer.Enums;
using SaperMultiplayer.Multiplayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace SaperMultiplayer;

#nullable enable
public class MinesweeperGame : Game
{
    // ======================================================= CONST VARIABLES =======================================================
    private const float SCROLL_SPEED = 0.5f;
    private const int CellSize = 32; // Size of the one cell in pixels (without zoom)
    private const int CellsInDrawBuffer = 2048 / CellSize;
    private const int GridLineThickness = 1;

    private static readonly Color[] numberColors =
    {
        Color.Transparent,          // 0
        new Color(0, 0, 255),       // 1 - Blue
        new Color(0, 128, 0),       // 2 - Green
        new Color(255, 0, 0),       // 3 - Red
        new Color(0, 0, 128),       // 4 - Navy
        new Color(128, 0, 0),       // 5 - Maroon
        new Color(0, 128, 128),     // 6 - Teal
        new Color(0, 0, 0),         // 7 - Black
        new Color(128, 128, 128)    // 8 - Gray
    };


    private static readonly List<Color> _availableColors = [Color.Red, Color.Blue, Color.Green, Color.Yellow, Color.Orange, Color.Purple, Color.Cyan];

    private readonly ConcurrentQueue<Action> _mainThreadActions = new();


    // ============================================================ FIELDS ============================================================
    // Tools
    private Camera _camera;
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private MouseState _prevMouseState;
    private SpriteFont _minesweeperFont;

    // Textures
    private Texture2D _pixelTexture; // White pixel for drawing rectangles and borders
    private Texture2D _cellHidden; // base 256x256
    private Dictionary<int, (Texture2D, int)> _hiddenTiles = [];
    private Texture2D _revealedCell; // one cell revealed background
    private Texture2D _revealedChunk;
    private Texture2D[] _numberTextures = new Texture2D[9];
    private Texture2D[] _mineTexture  = new Texture2D[3]; // [0] - normal mine, [1] - exploded mine, [2] - crossed mine
    private Texture2D _flagTexture;
    private Texture2D _cursorTexture;

    // UI objects
    private UI.Button? _nextButton;
    private List<UI.Button> _menuButtons = [];

    // Fields
    private Board _board;
    private Scene _currentScene = Scene.MainMenu;
    private GameMode _currentGameMode;
    private NetworkManager? _networkManager;
    private ServerManager? _serverManager;
    private List<LobbyPlayer> _players = [];

    private int[] _chunkSizes;
    private string _playerName = "";
    private string _ipAddress = "127.0.0.1";
    private int _port = 31420;
    private string _statusMessage = "";
    private bool _wasInactive = false;
    private float _cursorSendTimer = 0;
    
    // Getters
    internal Board GetBoard() => _board;
    public List<LobbyPlayer> GetLobbyPlayers() => _players;


    // ========================================================= CONSTRUCTOR =========================================================
    public MinesweeperGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.ApplyChanges();
        Window.AllowUserResizing = true;
        Window.Title = "MINESWEEPER INFINITE MULTIPLAYER COOP ULTIMATE EDITION";
        InactiveSleepTime = TimeSpan.Zero;
    }


    // ======================================================= MONOGAME METHODS ======================================================
    protected override void Initialize()
    {
        _camera = new Camera(GraphicsDevice.Viewport);
        _board = new Board(0);

        // Determine tile sizes of chunks in cells
        var sizes = new List<int>();

        int chunkSize = ChunkState.CHUNK_SIZE;
        while (chunkSize >= LeafChunk.SIZE)
        {
            sizes.Add(chunkSize);
            chunkSize /= 4; // CHUNKS_PER_ROW assumed 4
        }
        // ensure we include single-cell tile for fallback (1 cell)
        if (!sizes.Contains(1))
        {
            sizes.Add(1);
        }
        _chunkSizes = sizes.ToArray();

        CreateMainMenu();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Generating 1x1 pixel to use for drawing rectangles and borders
        _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        _pixelTexture.SetData([Color.White]);

        // === Loading assets ===
        _minesweeperFont = Content.Load<SpriteFont>("assets/fonts/MainFont"); 
        _cellHidden = Content.Load<Texture2D>("assets/textures/cell_hidden");
        _cursorTexture = Content.Load<Texture2D>("assets/textures/cursor");

        // Prepare precomposed hidden textures for chunk sizes
        PrepareTextures();
    }

    protected override void Update(GameTime gameTime)
    {
        // Execute any actions that were queued to run on the main thread (e.g., from network events)
        while (_mainThreadActions.TryDequeue(out var action))
        {
            action();
        }

        if (!IsActive)
        {
            // If the game window is not active, we skip input handling
            _wasInactive = true;
            base.Update(gameTime);
            return;
        }

        MouseState mouseState = Mouse.GetState();
        KeyboardState keyState = Keyboard.GetState();

        if (_wasInactive)
        {
            _prevMouseState = mouseState;
            _wasInactive = false;
            base.Update(gameTime);
            return;
        }

        if (keyState.IsKeyDown(Keys.Escape))
        {
            if (_currentScene != Scene.MainMenu)
            {
                ReturnToMainMenu();
            }
        }

        bool isMouseInWindow = GraphicsDevice.Viewport.Bounds.Contains(mouseState.Position);

        if (_currentScene == Scene.MainMenu)
        {
            foreach (var btn in _menuButtons)
            {
                btn.Update(mouseState, _prevMouseState);
            }
        }
        else if (_currentScene == Scene.EnterNickname || _currentScene == Scene.JoinSetup)
        {
            _nextButton?.Update(mouseState, _prevMouseState);
        }
        else if (_currentScene == Scene.Lobby)
        {
            _nextButton?.Update(mouseState, _prevMouseState);
            foreach (var btn in _menuButtons)
            {
                btn.Update(mouseState, _prevMouseState);
            }
        }
        else if (_currentScene == Scene.Playing)
        {
            _cursorSendTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_cursorSendTimer > 0.033f)
            {
                Vector2 worldPos = _camera.ScreenToWorld(mouseState.Position.ToVector2());
                if (_currentGameMode == GameMode.Client)
                {
                    _networkManager!.SendCursorPosition(worldPos);
                }
                else if (_currentGameMode == GameMode.Host)
                {
                    _serverManager!.BroadcastHostCursor(worldPos);
                }
                _cursorSendTimer = 0;
            }

            // ===== SCROLLING & ZOOMING =====
            float rawScrollDelta = mouseState.ScrollWheelValue - _prevMouseState.ScrollWheelValue;

            if (rawScrollDelta != 0)
            {
                // Modifier keys
                bool isCtrlDown = keyState.IsKeyDown(Keys.LeftControl) || keyState.IsKeyDown(Keys.RightControl);
                bool isShiftDown = keyState.IsKeyDown(Keys.LeftShift) || keyState.IsKeyDown(Keys.RightShift);

                if (isCtrlDown)
                {
                    _camera.AdjustZoom(rawScrollDelta / 1200f, mouseState.Position.ToVector2());
                }
                else
                {
                    float moveAmount = -rawScrollDelta * SCROLL_SPEED / _camera.Zoom;

                    if (isShiftDown)
                    {
                        // --- HORIZONTAL (SHIFT + SCROLL) ---
                        _camera.Move(new Vector2(moveAmount, 0));
                    }
                    else
                    {
                        // --- VERTICAL (SCROLL) ---
                        _camera.Move(new Vector2(0, moveAmount));
                    }
                }
            }

            // ===== CAMERA MOVEMENT =====
            if (mouseState.MiddleButton == ButtonState.Pressed && _prevMouseState.MiddleButton == ButtonState.Pressed)
            {
                Vector2 delta = (mouseState.Position - _prevMouseState.Position).ToVector2();
                _camera.Move(-delta / _camera.Zoom);
            }

            // or WASD
            Vector2 camMove = Vector2.Zero;
            if (keyState.IsKeyDown(Keys.W)) camMove.Y -= 15 * SCROLL_SPEED / _camera.Zoom;
            if (keyState.IsKeyDown(Keys.S)) camMove.Y += 15 * SCROLL_SPEED / _camera.Zoom;
            if (keyState.IsKeyDown(Keys.A)) camMove.X -= 15 * SCROLL_SPEED / _camera.Zoom;
            if (keyState.IsKeyDown(Keys.D)) camMove.X += 15 * SCROLL_SPEED / _camera.Zoom;
            _camera.Move(camMove);


            int topBarHeight = 40;
            if (isMouseInWindow)
            {
                if (mouseState.Y <= topBarHeight)
                {
                    // RESET button
                    if (mouseState.LeftButton == ButtonState.Released && _prevMouseState.LeftButton == ButtonState.Pressed)
                    {
                        int screenWidth = GraphicsDevice.Viewport.Width;
                        var resetButtonRect = new Rectangle((screenWidth - 90) / 2, 4, 90, topBarHeight - 8);

                        if (resetButtonRect.Contains(mouseState.Position) &&
                           (_currentGameMode == GameMode.Host || _currentGameMode == GameMode.Singleplayer))
                        {
                            RequestReset();
                        }
                    }
                }
                else
                {
                    // board interaction
                    Vector2 worldMousePos = _camera.ScreenToWorld(mouseState.Position.ToVector2());
                    int gridX = (int)Math.Floor(worldMousePos.X / CellSize);
                    int gridY = (int)Math.Floor(worldMousePos.Y / CellSize);

                    if (mouseState.LeftButton == ButtonState.Released && _prevMouseState.LeftButton == ButtonState.Pressed)
                    {
                        ProcessClick(gridX, gridY, true);
                    }

                    if (mouseState.RightButton == ButtonState.Pressed && _prevMouseState.RightButton == ButtonState.Released)
                    {
                        ProcessClick(gridX, gridY, false);
                    }
                }
            }
        }

        _prevMouseState = mouseState;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue); // background color

        if (_currentScene == Scene.MainMenu)
        {
            int width = GraphicsDevice.Viewport.Width;
            _spriteBatch.Begin();
            DrawCenteredText(_minesweeperFont, "MINESWEEPER MP!", new Rectangle(0, 30, width, 50), Color.White, 40);
            foreach (var btn in _menuButtons)
            {
                btn.Draw(_spriteBatch, _minesweeperFont, _pixelTexture);
            }

            _spriteBatch.End();
        }

        else if (_currentScene == Scene.EnterNickname)
        {
            int width = GraphicsDevice.Viewport.Width;
            int height = GraphicsDevice.Viewport.Height;

            _spriteBatch.Begin();
            DrawCenteredText(_minesweeperFont, "ENTER YOUR NAME:", new Rectangle(0, (int)(0.2f * height), width, 50), Color.White, 32);
            DrawCenteredText(_minesweeperFont, _playerName + (DateTime.Now.Second % 2 == 0 ? "_" : ""), new Rectangle(0, (int)(0.2f * height) + 70, width, 50), Color.Gold, 28);     
            _nextButton?.Draw(_spriteBatch, _minesweeperFont, _pixelTexture);
            _spriteBatch.End();
        }

        else if (_currentScene == Scene.Lobby)
        {
            int width = GraphicsDevice.Viewport.Width;
            _spriteBatch.Begin();

            DrawCenteredText(_minesweeperFont, "LOBBY", new Rectangle(0, 50, width, 32), Color.White, 32);

            // List of players in the lobby
            for (int i = 0; i < _players.Count; i++)
            {
                Vector2 textPos = new Vector2(140, 150 + i * 40);
                
                foreach (var btn in _menuButtons)
                {
                    btn.Draw(_spriteBatch, _minesweeperFont, _pixelTexture);
                }

                string role = _players[i].IsHost ? "[HOST] " : "";
                _spriteBatch.DrawString(_minesweeperFont, $"{role}{_players[i].Name}", textPos, _players[i].CursorColor);
            }
            _nextButton?.Draw(_spriteBatch, _minesweeperFont, _pixelTexture);

            _spriteBatch.End();
        }
        else if (_currentScene == Scene.JoinSetup)
        {
            int width = GraphicsDevice.Viewport.Width;
            int height = GraphicsDevice.Viewport.Height;
            _spriteBatch.Begin();
            DrawCenteredText(_minesweeperFont, "JOIN GAME", new Rectangle(0, (int)(0.2f * height), width, 32), Color.White, 32);
            DrawCenteredText(_minesweeperFont, "<IP ADDRESS>:<PORT>:", new Rectangle(0, (int)(0.2f * height) + 50, width, 32), Color.White, 24);
            DrawCenteredText(_minesweeperFont, _ipAddress + (DateTime.Now.Second % 2 == 0 ? "_" : ""), new Rectangle(0, (int)(0.2f * height) + 90, width, 32), Color.Gold, 24);
            _nextButton?.Draw(_spriteBatch, _minesweeperFont, _pixelTexture);
            _spriteBatch.End();
        }
        else if (_currentScene == Scene.Playing)
        {
            DrawGameWorld(gameTime);
            _spriteBatch.End();
        }

        base.Draw(gameTime);
    }


    // ======================================================== HELPER METHODS ========================================================
    private void PrepareTextures()
    {
        int px = 0;
        var rect = new Rectangle(0, 0, CellSize, CellSize);

        // === Single revealed cell texture ===
        _revealedCell = new RenderTarget2D(GraphicsDevice, CellSize, CellSize);
        GraphicsDevice.SetRenderTarget((RenderTarget2D)_revealedCell);
        GraphicsDevice.Clear(Color.Transparent);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp); // start sprite batch
        _spriteBatch.Draw(_pixelTexture, rect, Color.LightGray);
        DrawBorder(rect, GridLineThickness, Color.DarkGray);
        _spriteBatch.End(); // end sprite batch
        GraphicsDevice.SetRenderTarget(null);

        // === Hidden chunk textures ===
        // Generating needed textures
        var tmpDict = new Dictionary<int, Texture2D>();

        for (int i = 0; i < _chunkSizes.Length; i++)
        {
            int tmp = Math.Min(_chunkSizes[i], CellsInDrawBuffer); // limit to CellsInDrawBuffer as XNA has max texture size of 2048x2048

            if (tmpDict.ContainsKey(tmp))
                continue; // already generated this size

            px = tmp * CellSize;

            var txtHidden = new RenderTarget2D(GraphicsDevice, px, px);
            GraphicsDevice.SetRenderTarget(txtHidden);
            GraphicsDevice.Clear(Color.Transparent);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp); // start sprite batch

            for (int y = 0; y < px; y += CellSize)
            {
                for (int x = 0; x < px; x += CellSize)
                {
                    _spriteBatch.Draw(_cellHidden, new Rectangle(x, y, CellSize, CellSize), Color.White);
                }
            }

            _spriteBatch.End(); // end sprite batch

            tmpDict[tmp] = txtHidden;
        }
        GraphicsDevice.SetRenderTarget(null);

        // Map chunk sizes to textures and multipliers (for drawing larger chunks with multiple smaller textures)
        foreach (var sizeInCells in _chunkSizes)
        {
            int txtSize = Math.Min(sizeInCells, CellsInDrawBuffer);
            int multiplier = sizeInCells / txtSize;
            _hiddenTiles[sizeInCells] = (tmpDict[txtSize], multiplier);
        }

        // === Revealed tile ===
        px = CellsInDrawBuffer * CellSize;

        var txtRevealed = new RenderTarget2D(GraphicsDevice, px, px);
        GraphicsDevice.SetRenderTarget(txtRevealed);
        GraphicsDevice.Clear(Color.Transparent);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp); // start sprite batch
        for (int y = 0; y < px; y += CellSize)
        {
            for (int x = 0; x < px; x += CellSize)
            {
                _spriteBatch.Draw(_revealedCell, new Rectangle(x, y, CellSize, CellSize), Color.White);
            }
        }
        _spriteBatch.End(); // end sprite batch

        GraphicsDevice.SetRenderTarget(null);
        _revealedChunk = txtRevealed;

        // === Numbers, mines, flags textures ===
        // mineTexture with white background
        _mineTexture[0] = new RenderTarget2D(GraphicsDevice, CellSize, CellSize);
        GraphicsDevice.SetRenderTarget((RenderTarget2D)_mineTexture[0]);
        GraphicsDevice.Clear(Color.Transparent);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp); // start sprite batch
        _spriteBatch.Draw(_revealedCell, rect, Color.White);
        _spriteBatch.Draw(_pixelTexture, new Rectangle((int)(0.35 * CellSize), (int)(0.35 * CellSize), CellSize / 8, CellSize / 8), Color.White);
        _spriteBatch.End(); // end sprite batch

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp); // start sprite batch
        DrawSymbol(Symbol.Mine, rect);
        _spriteBatch.End(); // end sprite batch

        GraphicsDevice.SetRenderTarget(null);

        // mineTexture with red background for exploded mine
        _mineTexture[1] = new RenderTarget2D(GraphicsDevice, CellSize, CellSize);
        GraphicsDevice.SetRenderTarget((RenderTarget2D)_mineTexture[1]);
        GraphicsDevice.Clear(Color.Transparent);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp); // start sprite batch
        _spriteBatch.Draw(_revealedCell, rect, Color.Red);
        _spriteBatch.Draw(_pixelTexture, new Rectangle((int)(0.35 * CellSize), (int)(0.35 * CellSize), CellSize / 8, CellSize / 8), Color.White);
        _spriteBatch.End(); // end sprite batch

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp); // start sprite batch
        DrawSymbol(Symbol.Mine, rect);
        _spriteBatch.End(); // end sprite batch

        GraphicsDevice.SetRenderTarget(null);

        // mineTexture with crossed mine
        _mineTexture[2] = new RenderTarget2D(GraphicsDevice, CellSize, CellSize);
        GraphicsDevice.SetRenderTarget((RenderTarget2D)_mineTexture[2]);
        GraphicsDevice.Clear(Color.Transparent);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp); // start sprite batch
        _spriteBatch.Draw(_mineTexture[0], rect, Color.White);
        _spriteBatch.End(); // end sprite batch

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp); // start sprite batch
        for (int i = 3; i < CellSize - 6; i++)
        {
            _spriteBatch.Draw(_pixelTexture, new Rectangle(i, i + 2, 3, 1), Color.Red);
            _spriteBatch.Draw(_pixelTexture, new Rectangle(CellSize - i - 3, i + 2, 3, 1), Color.Red);
        }
        _spriteBatch.End(); // end sprite batch

        GraphicsDevice.SetRenderTarget(null);

        // flagTexture
        _flagTexture = new RenderTarget2D(GraphicsDevice, CellSize, CellSize);
        GraphicsDevice.SetRenderTarget((RenderTarget2D)_flagTexture);
        GraphicsDevice.Clear(Color.Transparent);

        // background for flag -- hidden cell
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp); // start sprite batch
        _spriteBatch.Draw(_cellHidden, rect, Color.White);
        _spriteBatch.End();

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });
        DrawSymbol(Symbol.Flag, rect);
        _spriteBatch.End(); // end sprite batch

        GraphicsDevice.SetRenderTarget(null);

        // numberTextures
        for (int i = 1; i <= 8; i++)
        {
            _numberTextures[i] = new RenderTarget2D(GraphicsDevice, CellSize, CellSize);
            GraphicsDevice.SetRenderTarget((RenderTarget2D)_numberTextures[i]);
            GraphicsDevice.Clear(Color.Transparent);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp); // start sprite batch
            DrawSymbol((Symbol)i, rect);
            _spriteBatch.End(); // end sprite batch
        }
        GraphicsDevice.SetRenderTarget(null);
    }

    private void RequestReset()
    {
        if (_currentGameMode == GameMode.Host)
        {
            uint newSeed = (uint)new Random().Next();
            _serverManager?.BroadcastReset(newSeed);
            StartNetworkGame(newSeed, 0, 0);
        }
        else if (_currentGameMode == GameMode.Singleplayer)
        {
            StartSingleplayer();
        }
    }

    private void ReturnToMainMenu(string message = "")
    {
        // Disconnecting
        _networkManager?.Close();
        _networkManager = null;
        _serverManager?.Stop();
        _serverManager = null;

        // Reset data
        _players.Clear();
        _board = new Board(0);
        _statusMessage = message;
        _ipAddress = "127.0.0.1";
        _port = 31420;

        // Reset camera
        _camera.Position = Vector2.Zero;
        _camera.Zoom = 1.0f;

        // Reset scene
        _currentScene = Scene.MainMenu;
        CreateMainMenu();

        Window.TextInput -= OnTextInput;
    }

    private void StartSingleplayer()
    {
        uint randomSeed = (uint)new Random().Next();
        _board = new Board(randomSeed);

        _currentScene = Scene.Playing;
        _camera.Position = Vector2.Zero;
        _camera.Zoom = 1.0f;
    }

    private void StartNetworkGame(uint seed, int x, int y)
    {
        if (_board == null || _board.Seed != seed)
        {
            _board = new Board(seed);
        }

        _board.IsHost = (_currentGameMode == GameMode.Host);
        _camera.Position = Vector2.Zero;
        _camera.Zoom = 1.0f;
        _currentScene = Scene.Playing;
    }

    public void EnqueueAction(Action action)
    {
        _mainThreadActions.Enqueue(action);
    }

    public void CreateLobbyButtons()
    {
        _menuButtons.Clear();
        int centerX = GraphicsDevice.Viewport.Width / 2;

        if (_currentGameMode == GameMode.Host)
        {
            _nextButton = new UI.Button("START GAME", new Rectangle(0, 400, 350, 50), () => {
                if (_serverManager != null && _players.Count >= 2)
                {
                    uint masterSeed = (uint)new Random().Next();
                    _serverManager.BroadcastGameStart(masterSeed, 0, 0);
                    StartNetworkGame(masterSeed, 0, 0);
                }
            }, true, _graphics);
        }

        else if (_currentGameMode == GameMode.Client)
        {
            if (_networkManager!.ServerGameInProgress)
            {
                _nextButton = new UI.Button("JOIN GAME", new Rectangle(0, 400, 350, 50), () => {
                    StartNetworkGame(_networkManager.ServerCurrentSeed, _networkManager.ServerStartX, _networkManager.ServerStartY);
                }, true, _graphics);
            }
            else
            {
                _nextButton = null;
            }
        }

        // Change color button
        int myIndex = _players.FindIndex(p => p.Name == _playerName);
        var myPlayer = _players[myIndex];
        var btn = new UI.Button("", new Rectangle(100, 160 + myIndex * 40, 25, 25), () =>
        {
            if (_currentGameMode == GameMode.Host)
            {
                // Host change color directly and broadcast
                myPlayer.CursorColor = GetNextColorInCycle(myPlayer.CursorColor);
                _serverManager!.BroadcastLobbyState();
                EnqueueAction(() => CreateLobbyButtons());
            }
            else
            {
                // Client sends request to server to change color
                _networkManager!.SendColorChangeRequest();
            }
        });

        btn.ColorP = myPlayer.CursorColor;
        btn.DrawBoundry = true;
        _menuButtons.Add(btn);
    }

    private void CreateMainMenu()
    {
        _menuButtons.Clear();
        int w = 600;
        int h = 60;

        // ===== SINGLEPLAYER BUTTON =====
        _menuButtons.Add(new UI.Button("Singleplayer", new Rectangle(0, 150, w, h), () => {
            _currentGameMode = GameMode.Singleplayer;
            StartSingleplayer();
        }, true, _graphics));


        // ===== HOST GAME BUTTON =====
        _menuButtons.Add(new UI.Button("Host Game", new Rectangle(0, 220, w, h), () => {
            _statusMessage = "";
            _currentGameMode = GameMode.Host;
            _currentScene = Scene.EnterNickname;
            _board.IsHost = true;
            Window.TextInput += OnTextInput;

            _nextButton = new UI.Button("Next", new Rectangle(0, 400, 200, 50), () => {
                if (string.IsNullOrWhiteSpace(_playerName))
                {
                    _statusMessage = "Name cannot be empty!";
                    return;
                }

                // Creating new players list only with host
                _players =
                [
                    new LobbyPlayer
                    {
                        Id = 0,
                        Name = _playerName,
                        CursorColor = Color.Red,
                        IsHost = true
                    },
                ];

                try
                {
                    _serverManager = new ServerManager(this);
                    _serverManager.Start(_port);

                    CreateLobbyButtons();

                    Window.TextInput -= OnTextInput;
                    _currentScene = Scene.Lobby;
                }
                catch (Exception ex)
                {
                    _statusMessage = "Server error: " + ex.Message;
                }
            }, true, _graphics);
        }, true, _graphics));


        // ===== JOIN GAME BUTTON =====
        _menuButtons.Add(new UI.Button("Join Game", new Rectangle(0, 290, w, h), () => {
            _statusMessage = "";
            _currentGameMode = GameMode.Client;
            _currentScene = Scene.EnterNickname;
            Window.TextInput += OnTextInput;

            _nextButton = new UI.Button("Next", new Rectangle(0, 400, 200, 50), () => {
                if (string.IsNullOrWhiteSpace(_playerName))
                {
                    _statusMessage = "Name cannot be empty!";
                    return;
                }

                // Now player have to enter IP address
                _currentScene = Scene.JoinSetup;
                _nextButton = new UI.Button("Join", new Rectangle(0, 400, 200, 50), () => {
                    try
                    {
                        string input = _ipAddress.Trim();
                        string pattern = @"^((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}" +
                            @"(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(:(6553[0-5]|655[0-2]\d|" +
                            @"65[0-4]\d{2}|6[0-4]\d{3}|[1-5]?\d{1,4}))?$";

                        if (!Regex.IsMatch(input, pattern))
                        {
                            _statusMessage = "Invalid IP address or port!";
                            return;
                        }

                        _networkManager?.Close();
                        _networkManager = new NetworkManager();

                        string[] parts = input.Split(':');
                        _ipAddress = parts[0];
                        
                        if (parts.Length > 1)
                        {
                            _port = int.Parse(parts[1]);
                        }

                        // === Events ===
                        _networkManager!.OnDisconnected += () => {
                            EnqueueAction(() => ReturnToMainMenu("Lost connection to Host!"));
                        };

                        _networkManager!.OnLobbyUpdated += (updatedPlayers) => {
                            EnqueueAction(() => {
                                _players = updatedPlayers;
                                CreateLobbyButtons();
                            });
                        };

                        _networkManager!.OnLobbyUpdated += (updatedPlayers) => {
                            EnqueueAction(() => {
                                _players = updatedPlayers;
                                CreateLobbyButtons();
                            });
                        };

                        _networkManager!.OnBoardSyncReceived += (seed, startX, startY, points, chunkStates) => {
                            EnqueueAction(() => {
                                // sync board
                                if (_board == null || _board.Seed != seed)
                                {
                                    _board = new Board(seed);
                                }

                                _board.SyncFirstClick(startX, startY, _networkManager.ServerFirstClickDone);
                                _board.SyncPoints(points);

                                foreach (var state in chunkStates)
                                {
                                    _board.GetData().ApplyChunkState(state);
                                }

                                CreateLobbyButtons();
                            });
                        };

                        _networkManager!.OnGameStarted += (seed, x, y) => {
                            EnqueueAction(() => StartNetworkGame(seed, x, y));
                        };

                        _networkManager!.OnPlayerClickReceived += (x, y, isLeft) => {
                            EnqueueAction(() => HandleRemoteClick(x, y, isLeft));
                        };

                        _networkManager!.OnGameResetReceived += (newSeed) => {
                            EnqueueAction(() => StartNetworkGame(newSeed, 0, 0));
                        };

                        _networkManager!.OnCursorMoved += (id, pos) => {
                            var p = _players.Find(x => x.Id == id);
                            if (p != null)
                            {
                                p.CursorWorldPos = pos;
                            }
                        };


                        // Connecting
                        _networkManager!.Connect(_ipAddress, _port, _playerName);

                        _nextButton = null;
                        Window.TextInput -= OnTextInput;
                        _currentScene = Scene.Lobby;
                    }
                    catch
                    {
                        _statusMessage = "Connection failed!";
                        _networkManager = null;
                    }
                }, true, _graphics);
            }, true, _graphics);
        }, true, _graphics));
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (sender == null)
        {
            return;
        }

        if (_currentScene != Scene.EnterNickname && _currentScene != Scene.JoinSetup)
        {
            return;
        }


        // BACKSPACE
        if (e.Character == '\b')
        {
            if (_currentScene == Scene.EnterNickname && _playerName.Length > 0)
            {
                _playerName = _playerName.Substring(0, _playerName.Length - 1);
            }
            else if (_currentScene == Scene.JoinSetup && _ipAddress.Length > 0)
            {
                _ipAddress = _ipAddress.Substring(0, _ipAddress.Length - 1);
            }
            
            return;
        }

        // ENTER
        /*
        if (e.Character == '\r' || e.Character == '\n')
        {
            if ((_currentScene == Scene.EnterNickname && _board.IsHost) || _currentScene == Scene.JoinSetup)
            {
                _currentScene = Scene.Lobby;
            }
            if (_currentScene == Scene.EnterNickname && !_board.IsHost)
            {
                _currentScene = Scene.JoinSetup;
            }

            return;
        }
        */

        if (_playerName.Length > 16 || _ipAddress.Length > 21)
        {
            return;
        }

        // Chars must be printable by font
        if (e.Character >= 32 && e.Character <= 126)
        {
            if (_currentScene == Scene.EnterNickname)
            {
                _playerName += e.Character;
            }
            else if (_currentScene == Scene.JoinSetup)
            {
                if (char.IsDigit(e.Character) || e.Character == '.' || e.Character == ':')
                {
                    _ipAddress += e.Character;
                }
            }
        }
    }

    private void ProcessClick(int x, int y, bool isLeft)
    {
        HandleRemoteClick(x, y, isLeft);

        // for mp games:
        if (_currentGameMode == GameMode.Client)
        {
            _networkManager!.SendClick(x, y, isLeft);
        }
        else if (_currentGameMode == GameMode.Host)
        {
            _serverManager!.BroadcastClick(x, y, isLeft);
        }
    }

    public void HandleRemoteClick(int x, int y, bool isLeft)
    {
        if (isLeft)
        {
            _board.LeftClickAt(x, y);
        }
        else
        {
            _board.RightClickAt(x, y);
        }
    }

    public Color GetNextAvailableColor()
    {
        foreach (var color in _availableColors)
        {
            if (!_players.Any(p => p.CursorColor == color))
            {
                // Color must be unique
                return color;
            }
        }
        return Color.White;
    }

    public Color GetNextColorInCycle(Color currentColor)
    {
        int currentIndex = _availableColors.IndexOf(currentColor);

        for (int i = 1; i <= _availableColors.Count; i++)
        {
            int nextIndex = (currentIndex + i) % _availableColors.Count;
            Color candidate = _availableColors[nextIndex];

            if (!_players.Any(p => p.CursorColor == candidate))
            {
                // Color must be unique
                return candidate;
            }
        }
        return currentColor; // No free colors
    }


    // ====================================================== DRAWING FUNCTIONS ======================================================
    private void DrawGameWorld(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.White);

        if (_chunkSizes.Length == 0)
        {
            _spriteBatch.End();
            return;
        }

        int topChunkSize = _chunkSizes[0];

        // Determine visible world rectangle
        Vector2 topLeft = _camera.ScreenToWorld(Vector2.Zero);
        Vector2 bottomRight = _camera.ScreenToWorld(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));

        int startX = (int)Math.Floor(topLeft.X / CellSize) - 1;
        int endX = (int)Math.Ceiling(bottomRight.X / CellSize) + 1;

        int startY = (int)Math.Floor(topLeft.Y / CellSize) - 1;
        int endY = (int)Math.Ceiling(bottomRight.Y / CellSize) + 1;

        // =============================================================
        // BOARD
        // =============================================================
        _spriteBatch.Begin(SpriteSortMode.Deferred, transformMatrix: _camera.GetTransform(), samplerState: SamplerState.PointClamp);

        // Phase 1: Draw revealed backgrounds and numbers using revealedTiles when possible
        int startBgX = (int)Math.Floor((double)startX / CellsInDrawBuffer) * CellsInDrawBuffer - CellsInDrawBuffer;
        int startBgY = (int)Math.Floor((double)startY / CellsInDrawBuffer) * CellsInDrawBuffer - CellsInDrawBuffer;


        for (int cy = startBgY; cy <= endY; cy += CellsInDrawBuffer)
        {
            for (int cx = startBgX; cx <= endX; cx += CellsInDrawBuffer)
            {
                // Revealed background (cx/cy are cell-aligned coordinates)
                _spriteBatch.Draw(_revealedChunk, new Rectangle(cx * CellSize, cy * CellSize, CellsInDrawBuffer * CellSize, CellsInDrawBuffer * CellSize), Color.White);
            }
        }

        // Stacking
        Stack<ChunkState> stack = new();
        int chunkStartX = (int)Math.Floor((double)startX / topChunkSize) * topChunkSize;
        int chunkStartY = (int)Math.Floor((double)startY / topChunkSize) * topChunkSize;

        for (int cy = chunkStartY; cy <= endY; cy += topChunkSize)
        {
            for (int cx = chunkStartX; cx <= endX; cx += topChunkSize)
            {
                stack.Push(_board.GetChunkStateAt(cx, cy));
            }
        }


        // Unstacking
        while (stack.Count > 0)
        {
            var chunkState = stack.Pop();
            int cx = chunkState.X;
            int cy = chunkState.Y;

            if (chunkState.SubChunkStates == null)
            {
                if (chunkState.Size == LeafChunk.SIZE)
                {
                    // This is LeafChunk
                    for (int y = cy; y < cy + chunkState.Size; y++)
                    {
                        for (int x = cx; x < cx + chunkState.Size; x++)
                        {
                            var cellRect = new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize);
                            if (_board.IsFlagged(x, y))
                            {
                                if (_board.Lost && !_board.IsMine(x, y))
                                {
                                    _spriteBatch.Draw(_mineTexture[2], cellRect, Color.White);
                                }
                                else
                                {
                                    _spriteBatch.Draw(_flagTexture, cellRect, Color.White);
                                }
                            }
                            else
                            {
                                int localX = x - cx;
                                int localY = y - cy;
                                int blockIndex = (localY * LeafChunk.BLOCKS_PER_ROW) + (localX >> LeafChunk.BLOCK_BITS);

                                if (chunkState.Data[blockIndex][localX & LeafChunk.BLOCK_MASK])
                                {
                                    // revealed
                                    uint neighbor = _board.GetNumberOfNeighborMines(x, y);
                                    if (neighbor > 0)
                                        _spriteBatch.Draw(_numberTextures[neighbor], cellRect, Color.White);
                                }
                                else
                                {
                                    // hidden
                                    _spriteBatch.Draw(_cellHidden, cellRect, Color.White);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (chunkState.Data[0].IsAllTrue)
                    {
                        // All cells are reaveled
                        for (int y = cy; y < cy + chunkState.Size; y++)
                        {
                            for (int x = cx; x < cx + chunkState.Size; x++)
                            {
                                var cellRect = new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize);
                                if (_board.IsFlagged(x, y))
                                {
                                    if (_board.Lost && !_board.IsMine(x, y))
                                    {
                                        _spriteBatch.Draw(_mineTexture[2], cellRect, Color.White);
                                    }
                                    else
                                    {
                                        _spriteBatch.Draw(_flagTexture, cellRect, Color.White);
                                    }
                                }
                                else
                                {
                                    uint neighbor = _board.GetNumberOfNeighborMines(x, y);
                                    if (neighbor > 0)
                                        _spriteBatch.Draw(_numberTextures[neighbor], cellRect, Color.White);
                                }
                            }
                        }
                    }
                    else
                    {
                        // All cells are hidden
                        var (txt, mp) = _hiddenTiles[chunkState.Size];

                        for (int i = 0; i < mp; i++)
                        {
                            for (int j = 0; j < mp; j++)
                            {
                                int subcx = cx + i * CellsInDrawBuffer;
                                int subcy = cy + j * CellsInDrawBuffer;
                                int cellsToDraw = Math.Min(CellsInDrawBuffer, chunkState.Size - (i * CellsInDrawBuffer));
                                _spriteBatch.Draw(txt, new Rectangle(subcx * CellSize, subcy * CellSize, cellsToDraw * CellSize, cellsToDraw * CellSize), Color.White);
                            }
                        }
                    }
                }
            }
            else
            {
                int subSize = chunkState.Size / ChunkState.CHUNKS_PER_ROW;

                for (int i = 0; i < chunkState.SubChunkStates.Length; i++)
                {
                    var sub = chunkState.SubChunkStates[i];

                    if (sub != null)
                    {
                        stack.Push(sub);
                    }
                    else
                    {

                        int subIndexX = i % ChunkState.CHUNKS_PER_ROW;
                        int subIndexY = i / ChunkState.CHUNKS_PER_ROW;
                        int subcx = cx + subIndexX * subSize;
                        int subcy = cy + subIndexY * subSize;

                        bool isFilled = chunkState.Data[i >> 3][i % 8];

                        // subchunk is null
                        if (isFilled)
                        {
                            // subchunk is fully revealed
                            for (int y = subcy; y < subcy + subSize; y++)
                            {
                                for (int x = subcx; x < subcx + subSize; x++)
                                {
                                    var cellRect = new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize);
                                    if (_board.IsFlagged(x, y))
                                    {
                                        if (_board.Lost && !_board.IsMine(x, y))
                                        {
                                            _spriteBatch.Draw(_mineTexture[2], cellRect, Color.White);
                                        }
                                        else
                                        {
                                            _spriteBatch.Draw(_flagTexture, cellRect, Color.White);
                                        }
                                    }
                                    else
                                    {
                                        uint neighbor = _board.GetNumberOfNeighborMines(x, y);
                                        if (neighbor > 0)
                                            _spriteBatch.Draw(_numberTextures[neighbor], cellRect, Color.White);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // subchunk is fully hidden
                            var (txt, mp) = _hiddenTiles[subSize];

                            for (int a = 0; a < mp; a++)
                            {
                                for (int b = 0; b < mp; b++)
                                {
                                    int subcx2 = subcx + a * CellsInDrawBuffer;
                                    int subcy2 = subcy + b * CellsInDrawBuffer;

                                    // Safety check to not draw outside of chunk bounds
                                    int cellsToDraw = Math.Min(CellsInDrawBuffer, subSize - (a * CellsInDrawBuffer));

                                    _spriteBatch.Draw(txt, new Rectangle(
                                        subcx2 * CellSize,
                                        subcy2 * CellSize,
                                        cellsToDraw * CellSize,
                                        cellsToDraw * CellSize), Color.White);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Drawing mines if lost
        if (_board.Lost)
        {
            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    if (_board.IsMine(x, y) && !_board.IsRevealed(x, y))
                    {
                        _spriteBatch.Draw(_mineTexture[_board.LostXY == (x, y) ? 1 : 0], new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize), Color.White);
                    }
                }
            }
        }

        // Unreaveling tile when holding left mouse button
        if (!_board.Lost && _prevMouseState.LeftButton == ButtonState.Pressed)
        {
            Vector2 worldMousePos = _camera.ScreenToWorld(_prevMouseState.Position.ToVector2());
            int gridX = (int)Math.Floor(worldMousePos.X / CellSize);
            int gridY = (int)Math.Floor(worldMousePos.Y / CellSize);

            if (_board.IsRevealed(gridX, gridY))
            {
                if (!_board.IsFlagged(gridX, gridY))
                {
                    uint n = _board.GetNumberOfNeighborMines(gridX, gridY);
                    if (n > 0)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0)
                                    continue;

                                if (!_board.IsRevealed(gridX + dx, gridY + dy))
                                {
                                    _spriteBatch.Draw(_revealedCell, new Rectangle((gridX + dx) * CellSize, (gridY + dy) * CellSize, CellSize, CellSize), Color.White);
                                }
                            }
                        }
                    }
                }

            }
            else
            {
                // Showing one tile under cursor as revealed when holding LMB (for better targeting)
                _spriteBatch.Draw(_revealedCell, new Rectangle(gridX * CellSize, gridY * CellSize, CellSize, CellSize), Color.White);
            }
        }

        foreach (var player in _players)
        {
            bool isMe = (_currentGameMode == GameMode.Host && player.IsHost) ||
                        (_currentGameMode == GameMode.Client && player.Name == _playerName);

            if (!isMe)
            {
                // Drawing Cursor
                Rectangle cursorRect = new Rectangle((int)player.CursorWorldPos.X, (int)player.CursorWorldPos.Y, 16, 16);
                _spriteBatch.Draw(_cursorTexture, cursorRect, player.CursorColor);

                // Name above cursor
                _spriteBatch.DrawString(_minesweeperFont, player.Name,
                    new Vector2(player.CursorWorldPos.X, player.CursorWorldPos.Y - 15),
                    Color.White, 0f, Vector2.Zero, 0.3f, SpriteEffects.None, 0f);
            }
        }

        _spriteBatch.End();

        // =============================================================
        // UI
        // =============================================================
        _spriteBatch.Begin();

        // Parameters
        int topBarHeight = 40;
        int screenWidth = GraphicsDevice.Viewport.Width;

        // Background
        _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, topBarHeight), Color.FromNonPremultiplied(40, 40, 40, 255));
        _spriteBatch.Draw(_pixelTexture, new Rectangle(0, topBarHeight, screenWidth, 2), Color.Black);

        if (_minesweeperFont != null)
        {
            // points counter
            const float uiScale = 0.5f;
            uint points = _board.Points;
            string leftText = $"POINTS: {points}";

            Vector2 leftTextSize = _minesweeperFont.MeasureString(leftText);
            Vector2 leftPos = new Vector2(20f, (topBarHeight - leftTextSize.Y * uiScale) / 2f);
            _spriteBatch.DrawString(_minesweeperFont, leftText, leftPos, Color.Gold, 0f, Vector2.Zero, uiScale, SpriteEffects.None, 0f);

            // Coordinates
            int gridX = (int)Math.Floor(_camera.Position.X / CellSize);
            int gridY = (int)Math.Floor(_camera.Position.Y / CellSize);

            string rightText = $"[X: {gridX}, Y: {gridY}]";

            Vector2 rightTextSize = _minesweeperFont.MeasureString(rightText);
            Vector2 rightPos = new Vector2(screenWidth - (rightTextSize.X * uiScale) - 20f, (topBarHeight - rightTextSize.Y * uiScale) / 2f);

            _spriteBatch.DrawString(_minesweeperFont, rightText, rightPos, Color.LightGreen, 0f, Vector2.Zero, uiScale, SpriteEffects.None, 0f);
        }

        // Draw Reset button on the top-right
        if (_currentGameMode == GameMode.Host || _currentGameMode == GameMode.Singleplayer)
        {
            int buttonWidth = 90;
            int buttonHeight = topBarHeight - 8;
            Rectangle resetButton = new Rectangle((screenWidth - buttonWidth) / 2, 4, buttonWidth, buttonHeight);

            _spriteBatch.Draw(_pixelTexture, resetButton, Color.DarkRed);
            DrawCenteredText(_minesweeperFont!, "RESET", resetButton, Color.White, 10);
        }
    }

    private void DrawSymbol(Symbol symbol, Rectangle boundary, int size = 16)
    {
        switch (symbol)
        {
            case Symbol.Number1:
                boundary.Offset(2, 0);
                DrawCenteredText(_minesweeperFont, "1", boundary, numberColors[1], size);
                break;

            case Symbol.Number2:
            case Symbol.Number3:
            case Symbol.Number4:
            case Symbol.Number5:
            case Symbol.Number6:
            case Symbol.Number7:
            case Symbol.Number8:
                boundary.Offset(1, 0);
                DrawCenteredText(_minesweeperFont, ((int)symbol).ToString(), boundary, numberColors[(int)symbol], size);
                break;

            case Symbol.Mine:
                boundary.Offset(1, 1);
                DrawCenteredText(_minesweeperFont, "*", boundary, Color.Black, size);
                break;

            case Symbol.Flag:
                boundary.Offset(2, 0);
                DrawFlag(boundary);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(symbol), $"Unsupported symbol: {symbol}");
        }
    }

    private void DrawBorder(Rectangle rectangleToDraw, int thicknessOfBorder, Color borderColor)
    {
        // Up - Down - Left - Right
        _spriteBatch.Draw(_pixelTexture, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, rectangleToDraw.Width, thicknessOfBorder), borderColor);
        _spriteBatch.Draw(_pixelTexture, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y + rectangleToDraw.Height - thicknessOfBorder, rectangleToDraw.Width, thicknessOfBorder), borderColor);
        _spriteBatch.Draw(_pixelTexture, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, thicknessOfBorder, rectangleToDraw.Height), borderColor);
        _spriteBatch.Draw(_pixelTexture, new Rectangle(rectangleToDraw.X + rectangleToDraw.Width - thicknessOfBorder, rectangleToDraw.Y, thicknessOfBorder, rectangleToDraw.Height), borderColor);
    }

    private void DrawCenteredText(SpriteFont font, string text, Rectangle boundary, Color color, int size)
    {
        if (string.IsNullOrEmpty(text) || font == null)
        {
            return;
        }

        float scale = size / 24f;

        // Measure string at font scale (unscaled). Origin must be in font-space (unscaled)
        Vector2 textSize = font.MeasureString(text);

        Vector2 centerOfRect = new Vector2(boundary.X + boundary.Width / 2f, boundary.Y + boundary.Height / 2f);
        Vector2 origin = textSize / 2f;

        _spriteBatch.DrawString(font, text, centerOfRect, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    private void DrawFlag(Rectangle boundary, int size = 16)
    {
        var oldScissor = GraphicsDevice.ScissorRectangle;

        float scale = size / 24f;
        string text = "`";
        Color topColor = Color.Red;
        Color bottomColor = Color.Black;

        // Flag's text dims and position
        Vector2 textSize = _minesweeperFont.MeasureString(text);
        Vector2 origin = textSize / 2f;
        Vector2 position = new Vector2(boundary.X + boundary.Width / 2f, boundary.Y + boundary.Height / 2f);

        // Half flag is red, half is black
        int splitH = (int)(boundary.Height * 0.5f);

        // Red top
        GraphicsDevice.ScissorRectangle = Rectangle.Intersect(oldScissor,
            new Rectangle(boundary.X, boundary.Y, boundary.Width, splitH));

        _spriteBatch.DrawString(_minesweeperFont, text, position, topColor, 0f, origin, scale, SpriteEffects.None, 0f);

        // Black bottom
        GraphicsDevice.ScissorRectangle = Rectangle.Intersect(oldScissor,
            new Rectangle(boundary.X, boundary.Y + splitH, boundary.Width, boundary.Height - splitH));

        _spriteBatch.DrawString(_minesweeperFont, text, position, bottomColor, 0f, origin, scale, SpriteEffects.None, 0f);

        GraphicsDevice.ScissorRectangle = oldScissor;
    }
}