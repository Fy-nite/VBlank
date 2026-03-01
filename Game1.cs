using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ObjectIR.Core.IR;
using ObjectIR.Core.Serialization;
using OCRuntime;
using SharpIR;
using System;
using System.Diagnostics;
using VBlank.AudioEngine;
using ObjectIR.MonoGame.SFX;
using Adamantite.GFX;
using Adamantite.GPU;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using static SharpIR.CSharpParser;
using VBlank;
using Adamantite.Util;
using VBlank.Abstractions;
using Color = Microsoft.Xna.Framework.Color;

namespace VBlank
{
    // Interface implemented by native games to receive an engine reference.
    public interface IEngineHost
    {
        void SetEngine(AsmoGameEngine engine);
    }


    public partial class AsmoGameEngine : Game
    {
        // Runtime-configurable backend selector. Set environment variable ASMO_BACKEND=SDL to use Silk.NET/SDL backend.
        enum BackendType { MonoGame, SilkSDL }
        static BackendType DetectBackend()
        {
            try
            {
                var v = Environment.GetEnvironmentVariable("ASMO_BACKEND");
                if (!string.IsNullOrEmpty(v) && v.Equals("SDL", StringComparison.OrdinalIgnoreCase))
                    return BackendType.SilkSDL;
            }
            catch { }
            return BackendType.MonoGame;
        }
        // UI manager responsible for retained UI and dirty-region rendering
        public Adamantite.GFX.UI.UIManager UI { get; } = new Adamantite.GFX.UI.UIManager();
        // Simple window sizing policy that callers can configure per-application.
        public struct WindowPolicy
        {
            // Allow the user to resize the window with the mouse/OS decorations
            public bool AllowUserResizing;
            // Prevent any resizing (locks the window to the initial size)
            public bool LockSize;
            // When true, start the app in true fullscreen mode
            public bool StartFullscreen;
            // When true, size the window to match the OS display size (windowed)
            public bool SizeToDisplay;
            // Allow toggling fullscreen with Alt+Enter
            public bool AltEnterToggle;
        }

        WindowPolicy _windowPolicy;
        public WindowPolicy Policy
        {
            get => _windowPolicy;
            set
            {
                _windowPolicy = value;
                ApplyWindowPolicy();
            }
        }
        public GraphicsDeviceManager? _graphics;
        private SpriteBatch _spriteBatch;
        private Texture2D _screenTexture;
        public Canvas _canvas;
        private SpriteFont _font;
        // virtual pixel buffer size (like a Pico-8 style canvas)
        private int _bufferWidth;
        private int _bufferHeight;
        private float _scale = 1; // how much to scale the virtual canvas to the window
        // out of 60 fps, how many frames have been drawn
        private int _frameCounter = 0;
        private readonly Adamantite.GFX.FpsCounter _fpsCounter = new();
        private double _fpsDisplay = 0.0;
        public IRRuntime _runtime;
        // Optionally host a native C# game implementing IConsoleGame
        private Adamantite.GFX.IConsoleGame? _nativeGame;
        // Console overlay text commands (printed via SpriteFont on top of the pixel buffer)
        private readonly List<(string text, int x, int y, Microsoft.Xna.Framework.Color color)> _consoleTexts = new();

        // State used for fullscreen toggling and keyboard detection
        KeyboardState _prevKeyboardState;
        int _prevWindowedWidth;
        int _prevWindowedHeight;
        float _prevWindowedScale;
        // Prevent reentrant resize handling when we programmatically change presentation
        bool _suspendResizeHandling = false;
        const float ScaleEpsilon = 0.001f;

        public float PresentationScale => _scale;
        public int BufferWidth => _bufferWidth;
        public int BufferHeight => _bufferHeight;

        // Expose the underlying GraphicsDevice for helpers or hosted games
        public GraphicsDevice EngineGraphicsDevice => GraphicsDevice;

        public static AsmoGameEngine Instance { get; set; }

        private Surface? _sdlSurface;
        private bool _useSdl = false;

        // Queue for batching texture uploads; processed on the main thread.
        private readonly ConcurrentQueue<Action> _renderQueue = new ConcurrentQueue<Action>();
        private readonly object _graphicsLock = new object();

        // Audio focus handling: target/master lerp state
        private float _audioTargetVolume = 1f;
        private float _audioLerpSpeed = 2f; // units per second

        public AsmoGameEngine(string name, int width, int height, float scale = 0.75f)
        {
            Instance = this;
            // set name, width and height based on parameters
            // Detect backend; we still construct MonoGame GraphicsDeviceManager by default but log init failures.
            var backend = DetectBackend();
            // Force MonoGame usage
            // if (backend == BackendType.SilkSDL)
            // {
            //         DebugUtil.Debug("ASMO: Using SilkSDL backend (ASMO_BACKEND=SDL)");
            //     _useSdl = true;
            // }
            // Only initialize MonoGame GraphicsDeviceManager when not using SDL backend
            if (!_useSdl)
            {
                try
                {
                    _graphics = new GraphicsDeviceManager(this);
                }
                catch (Exception ex)
                {
                    DebugUtil.Debug("ASMO: GraphicsDeviceManager init failed: " + ex.Message);
                    Console.Error.WriteLine(ex.ToString());
                    throw;
                }
            }
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _bufferHeight = height;
            _bufferWidth = width;
            _scale = scale;
            // set window size based on pixel buffer and scale
            //_graphics.PreferredBackBufferWidth = _bufferWidth * _scale;
            //_graphics.PreferredBackBufferHeight = _bufferHeight * _scale;

            // set window size based on the requested width and height.
            if (_graphics != null)
            {
                _graphics.PreferredBackBufferWidth = (int)(width * _scale);
                _graphics.PreferredBackBufferHeight = (int)(height * _scale);
            }

            // disable VSync so the GPU is not forced to sync to the display
            if (_graphics != null) _graphics.SynchronizeWithVerticalRetrace = true;
            // run Update/Draw as fast as possible (uncap FPS)
            IsFixedTimeStep = false;
            if (!string.IsNullOrEmpty(name))
            {
                Window.Title = name;
            }
            // _runtime = new IRRuntime(null);
            if (_runtime == null)
                _runtime = new IRRuntime("module test version 1.0.0");
            _runtime.EnableReflectionNativeMethods = true;

            if (_graphics != null) _graphics.ApplyChanges();

            // Default policy: allow resizing and Alt+Enter toggle
            _windowPolicy = new WindowPolicy
            {
                AllowUserResizing = true,
                LockSize = false,
                StartFullscreen = false,
                SizeToDisplay = false,
                AltEnterToggle = true
            };

            // Apply the initial policy and hook resize handling
            try
            {
                ApplyWindowPolicy();
                Window.ClientSizeChanged += OnClientSizeChanged;
                // Lerp master volume down when window is deactivated (tabbed out), restore when activated
                    try
                    {
                        Activated += (s, e) =>
                        {
                            _audioTargetVolume = 1f;
                            Console.WriteLine("Window.Activated: audio target -> 1.0");
                        };
                        Deactivated += (s, e) =>
                        {
                            _audioTargetVolume = 0.1f;
                            Console.WriteLine("Window.Deactivated: audio target -> 0.3");
                        };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Warning: failed to hook window focus events: " + ex.Message);
                    }
            }
            catch
            {
                // some platforms may not support resizing; ignore failures
            }

            // register builtin runtime bindings (pixels, console, audio, etc.)
            _runtime = RegisterRuntimeBindings(_runtime);

            // Log unhandled exceptions to stderr to aid debugging on Linux/macOS
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { Console.Error.WriteLine("ASMO Unhandled exception: " + e.ExceptionObject?.ToString()); } catch { }
            };

            // Setup a simple VFS: mount physical folder "root" next to exe as "/"
            var vfs = new Adamantite.VFS.VfsManager();
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var rootPath = System.IO.Path.Combine(exeDir, "root");
            vfs.Mount("/", new Adamantite.VFS.PhysicalFileSystem(rootPath));
            // Also mount an in-memory fs at /mem for testing and map /bin to it
            var memFs = new Adamantite.VFS.InMemoryFileSystem();
            vfs.Mount("/mem", memFs);
            vfs.Mount("/bin", memFs);

            // Ensure /mem/bin exists and provide a minimal /bin/sh stub
            try
            {
                vfs.CreateDirectory("/mem/bin");
                var shBytes = System.Text.Encoding.UTF8.GetBytes("#!/bin/sh\n# minimal sh stub\necho \"StarChart sh stub\"\n");
                vfs.WriteAllBytes("/mem/bin/sh", shBytes);
                // Create a symlink /bin/sh -> /mem/bin/sh
                memFs.CreateSymlink("sh", "/mem/bin/sh");
            }
            catch { }
            // expose VFS on runtime (optional) - keep a static instance for now
            Adamantite.VFS.VFSGlobal.Manager = vfs;

        }

        /// <summary>
        /// Host a native C# game (must implement IConsoleGame). Call before Run()/LoadContent.
        /// </summary>
        public void HostNativeGame(Adamantite.GFX.IConsoleGame native)
        {
            _nativeGame = native ?? throw new ArgumentNullException(nameof(native));
            // If the native game wants the engine reference, provide it.
            if (native is IEngineHost host) host.SetEngine(this);
        }
        public void Init(string name, int width, int height)
        {
            
        }

        // Set presentation scale for the virtual pixel buffer. This controls how the
        // internal pixel buffer is scaled to the window. Call at runtime to change size.
        public void SetScale(float scale)
        {
            if (scale <= 0) return;
            // avoid tiny fluctuations causing reentrant ApplyChanges
            if (Math.Abs(scale - _scale) <= ScaleEpsilon) return;
            _scale = scale;
            if (_graphics == null) return;
            _graphics.PreferredBackBufferWidth = (int)(_bufferWidth * _scale);
            _graphics.PreferredBackBufferHeight = (int)(_bufferHeight * _scale);
            try
            {
                _suspendResizeHandling = true;
                _graphics.ApplyChanges();
            }
            finally
            {
                _suspendResizeHandling = false;
            }
        }

        void ApplyWindowPolicy()
        {
            try
            {
                Window.AllowUserResizing = _windowPolicy.AllowUserResizing && !_windowPolicy.LockSize;

                if (_graphics != null)
                {
                    if (_windowPolicy.SizeToDisplay)
                    {
                        var d = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                        _graphics.PreferredBackBufferWidth = d.Width;
                        _graphics.PreferredBackBufferHeight = d.Height;
                        _graphics.ApplyChanges();
                        _scale = Math.Max(0.1f, Math.Min((float)d.Width / Math.Max(1, _bufferWidth), (float)d.Height / Math.Max(1, _bufferHeight)));
                    }

                    if (_windowPolicy.StartFullscreen)
                    {
                        EnterFullScreen();
                    }
                    else
                    {
                        ExitFullScreen();
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        void EnterFullScreen()
        {
            if (_graphics == null) return;
            if (!_graphics.IsFullScreen)
            {
                _prevWindowedWidth = _graphics.PreferredBackBufferWidth;
                _prevWindowedHeight = _graphics.PreferredBackBufferHeight;
                _prevWindowedScale = _scale;
                _graphics.IsFullScreen = true;
                _graphics.ApplyChanges();
            }
        }

        void ExitFullScreen()
        {
            if (_graphics == null) return;
            if (_graphics.IsFullScreen)
            {
                _graphics.IsFullScreen = false;
                _graphics.PreferredBackBufferWidth = Math.Max(1, _prevWindowedWidth == 0 ? _bufferWidth : _prevWindowedWidth);
                _graphics.PreferredBackBufferHeight = Math.Max(1, _prevWindowedHeight == 0 ? _bufferHeight : _prevWindowedHeight);
                _graphics.ApplyChanges();
                SetScale(_prevWindowedScale <= 0 ? 1f : _prevWindowedScale);
            }
        }

        // When the game window is resized by the user, update the presentation scale
        // so the virtual pixel buffer is scaled to fit the new client size while
        // preserving aspect ratio.
        private void OnClientSizeChanged(object? sender, EventArgs e)
        {
            if (_suspendResizeHandling) return;
            // If the policy locks size, ignore client size changes triggered by the OS/user
            if (_windowPolicy.LockSize) return;
            try
            {
                var bounds = Window.ClientBounds;
                if (bounds.Width <= 0 || bounds.Height <= 0) return;

                // Compute scale to fit the internal buffer into the client area
                float scaleX = (float)bounds.Width / Math.Max(1, _bufferWidth);
                float scaleY = (float)bounds.Height / Math.Max(1, _bufferHeight);
                // Use the smaller scale to preserve aspect ratio
                float newScale = Math.Max(0.1f, Math.Min(scaleX, scaleY));

                // Update presentation scale (this will update preferred backbuffer size)
                SetScale(newScale);
            }
            catch
            {
                // swallow any exceptions during resize handling
            }
        }
        protected override void Initialize()
        {
            // TODO: Add initialization logic here

            // Do not run the IR until LoadContent has created the pixel buffer/texture
            base.Initialize();
        }

        protected override void LoadContent()
        {
            if (!_useSdl)
            {
                _spriteBatch = new SpriteBatch(GraphicsDevice);

                // load a SpriteFont (Content/DefaultFont.spritefont)
                _font = Content.Load<SpriteFont>("DefaultFont");

                // create the Texture2D that will hold the pixel buffer
                _screenTexture = new Texture2D(GraphicsDevice, _bufferWidth, _bufferHeight, false, SurfaceFormat.Color);
                _canvas = new Canvas(_bufferWidth, _bufferHeight);
                _canvas.Clear(Color.Black);
                // Ensure the texture reflects any immediate pixel changes
                try
                {
                    var rect = new Rectangle(0, 0, _canvas.width, _canvas.height);
                    _screenTexture.SetData(0, rect, _canvas.PixelData, 0, _canvas.PixelData.Length);
                    DebugUtil.Debug("ASMO: Initial Texture2D.SetData succeeded");
                }
                catch
                {
                    // Fallback to naive SetData when explicit rectangle fails
                    DebugUtil.Debug("ASMO: Texture2D.SetData with rectangle failed; falling back to full SetData");
                    _screenTexture.SetData(_canvas.PixelData);
                    // Load image assets from VFS into AssetCache (PNG/JPG).
                    try
                    {
                        var vfs = Adamantite.VFS.VFSGlobal.Manager;
                        if (vfs != null)
                        {
                            foreach (var fi in vfs.Enumerate("/assets"))
                            {
                                var name = fi.Name ?? string.Empty;
                                if (string.IsNullOrEmpty(name)) continue;
                                string extension = (System.IO.Path.GetExtension(name) ?? string.Empty).ToLowerInvariant();
                                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".bmp") continue;
                                try
                                {
                                    var path = "/assets/" + name;
                                    var tex = Adamantite.GFX.TextureLoader.LoadTextureFromVfs(GraphicsDevice, path);
                                    if (tex != null)
                                    {
                                        var key = System.IO.Path.GetFileNameWithoutExtension(name);
                                        AssetCache.Register(key, tex);
                                        DebugUtil.Debug($"ASMO: Loaded asset via VFS: {name} as key={key}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    DebugUtil.Debug("ASMO: failed loading asset " + name + " -> " + ex.Message);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            else
            {
                // SDL mode: still create the canvas that the runtime and native games draw into
                _canvas = new Canvas(_bufferWidth, _bufferHeight);
                _canvas.Clear(Color.Black);
            }

            // Initialize audio subsystem with the game's Content manager so audio assets can be loaded
            // If a native game is hosted, initialize it instead of running the IR runtime
            if (_nativeGame != null)
            {
                _nativeGame.Init(_canvas);
            }
            else
            {
                // it's the games job to initialize audio if they want it, but we'll initialize the subsystem here so it's ready if IR want's it.
                Subsystem.Initialize(Content);
                // Run the IR now that the pixel buffer and texture are initialized
                _runtime.Run();
            }

            // If using SDL backend, create an adapter and surface for presenting. We postpone
            // creation until here so we have a valid Canvas instance. Otherwise create a
            // MonoGameRenderBackend that will upload the Canvas into a Texture2D.
           
            // RenderBackendAdapter removal: forcing native MonoGame SetData/Draw path.
            

            // No background render thread; uploads are processed on the main thread.
        }

        protected override void UnloadContent()
        {
            // Shutdown audio subsystem and release precomputed assets
            Subsystem.Shutdown();
            // Shutdown SDL adapter if present
            // try { _sdlAdapter?.Shutdown(); } catch { }
            // try { _sdlAdapter?.Dispose(); } catch { }
            // _sdlAdapter = null;
            _sdlSurface = null;
            // No background render thread to stop.
            base.UnloadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            if (Environment.GetEnvironmentVariable("ASMO_DEBUG_UPDATES") == "1") Console.WriteLine("AsmoGameEngine.Update");
            var keyboard = Keyboard.GetState();
            // Alt+Enter fullscreen toggle when enabled by policy
            if (_windowPolicy.AltEnterToggle)
            {
                bool altDown = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
                bool enterDown = keyboard.IsKeyDown(Keys.Enter);
                bool prevAltDown = _prevKeyboardState.IsKeyDown(Keys.LeftAlt) || _prevKeyboardState.IsKeyDown(Keys.RightAlt);
                bool prevEnterDown = _prevKeyboardState.IsKeyDown(Keys.Enter);
                // trigger on key press (not hold)
                if (altDown && enterDown && (!prevAltDown || !prevEnterDown))
                {
                    // toggle fullscreen
                    if (GraphicsDevice.PresentationParameters.IsFullScreen)
                        ExitFullScreen();
                    else
                        EnterFullScreen();
                }
            }

            // if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
            //     Button.PlayClick(Subsystem.Sound);

            //ClearPixelBuffer(Color.Black);
            // Process UI input early so clicks take effect before native game logic runs
            try { UI.ProcessInput(_scale); } catch { }

            // If hosting a native game, update it (it will draw into the canvas). Otherwise the IR runtime updates the canvas.
            if (_nativeGame != null)
            {
                // Update and immediately draw the native game into the canvas so
                // subsequent texture uploads in Update include the latest frame.
                _nativeGame.Update(gameTime.ElapsedGameTime.TotalSeconds);
                try
                {
                    // Always invoke Draw(Canvas) on the native game to update the canvas
                    // pixels and set _canvas.IsDirty so texture upload work is enqueued.
                    _nativeGame.Draw(_canvas);
                }
                catch
                {
                    // drawing should not crash the engine loop
                }
            }

            // Update fps counter
            _fpsCounter.Tick(gameTime.ElapsedGameTime.TotalSeconds);
            _fpsDisplay = _fpsCounter.Fps;

            // Smoothly lerp master bus volume toward the target when focus changes
            try
            {
                var sound = VBlank.AudioEngine.Subsystem.Sound;
                if (sound != null)
                {
                    // Ensure we respond to focus changes even if window events didn't fire on this platform
                    try
                    {
                        float focusTarget = this.IsActive ? 1f : 0.1f;
                        if (Math.Abs(_audioTargetVolume - focusTarget) > 0.0001f)
                        {
                            _audioTargetVolume = focusTarget;
                            Console.WriteLine("Audio focus fallback: set _audioTargetVolume -> " + focusTarget);
                        }
                    }
                    catch { }

                    float current = sound.Master.Volume;
                    float target = _audioTargetVolume;
                    float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                    const float Epsilon = 1e-6f;
                    if (Math.Abs(current - target) > Epsilon)
                    {
                        float t = Math.Clamp(dt * _audioLerpSpeed, 0f, 1f);
                        float next = Microsoft.Xna.Framework.MathHelper.Lerp(current, target, t);
                        // If the next value is extremely close to the target, snap to target to avoid tiny residuals
                        if (Math.Abs(next - target) <= Epsilon) next = target;
                        try { sound.SetBusVolume("Master", next); } catch { }
                        // Console.WriteLine("VBlank Audio Engine volume lerp: current=" + current + " target=" + target + " next=" + next);
                    }
                }
            }
            catch { }

            if (_canvas != null)
            {
                // Process input for UI (map window to canvas coordinates using current scale)
                try { UI.ProcessInput(_scale); } catch { }
                // Let UI manager render its dirty regions first (if any). This will mark canvas pixels directly.
                var uiDirty = UI.RenderDirty(_canvas);

                // Collect regions from UI and canvas and upload all of them
                var regions = new List<Rectangle>();
                if (uiDirty != null && uiDirty.Count > 0) regions.AddRange(uiDirty);
                if (_canvas.IsDirty)
                {
                    regions.Add(new Rectangle(_canvas.DirtyX, _canvas.DirtyY, _canvas.DirtyWidth, _canvas.DirtyHeight));
                }

                if (regions.Count > 0)
                {
                    var pool = System.Buffers.ArrayPool<Color>.Shared;
                    var swUpload = Stopwatch.StartNew();
                    long totalPixels = 0;
                    try
                    {
                        // Compute total pixels to upload
                        foreach (var r in regions)
                        {
                            int dx = Math.Max(0, r.X);
                            int dy = Math.Max(0, r.Y);
                            int dw = Math.Min(_canvas.width - dx, r.Width);
                            int dh = Math.Min(_canvas.height - dy, r.Height);
                            if (dw <= 0 || dh <= 0) continue;
                            totalPixels += (long)dw * dh;
                        }

                        bool forceFull = false;
                        try
                        {
                            var m = Environment.GetEnvironmentVariable("ASMO_UPLOAD_MODE");
                            if (!string.IsNullOrEmpty(m) && m.Equals("FULL", StringComparison.OrdinalIgnoreCase)) forceFull = true;
                        }
                        catch { }

                        long fullPixels = (long)_canvas.width * _canvas.height;
                        bool doFull = forceFull || (totalPixels > (fullPixels / 4));

                        // Enqueue texture SetData operations to the render worker so Update() stays fast.
                        {
                            if (doFull)
                            {
                                int len = _canvas.width * _canvas.height;
                                Color[] full = pool.Rent(len);
                                Array.Copy(_canvas.PixelData, 0, full, 0, len);
                                
                                // Enqueue a task that will call SetData on the render thread and return the array to pool
                                int localLenFull = len;
                                _renderQueue.Enqueue(() =>
                                {
                                    try
                                    {
                                        lock (_graphicsLock)
                                        {
                                            if (_screenTexture != null)
                                            {
                                                try
                                                {
                                                    var rect = new Rectangle(0, 0, _canvas.width, _canvas.height);
                                                    try
                                                    {
                                                        // log just before SetData
                                                        if (localLenFull > 0)
                                                        {
                                                            var fcol = full[0];
                                                            var mcol = full[localLenFull / 2];
                                                            var lcol = full[localLenFull - 1];
                                                            uint fv = ((uint)fcol.A << 24) | ((uint)fcol.R << 16) | ((uint)fcol.G << 8) | fcol.B;
                                                            uint mv = ((uint)mcol.A << 24) | ((uint)mcol.R << 16) | ((uint)mcol.G << 8) | mcol.B;
                                                            uint lv = ((uint)lcol.A << 24) | ((uint)lcol.R << 16) | ((uint)lcol.G << 8) | lcol.B;
                                                            DebugUtil.Debug($"ASMO: SetData FULL rect={rect} len={localLenFull} first=0x{fv:X8} mid=0x{mv:X8} last=0x{lv:X8}");
                                                        }
                                                    }
                                                    catch { }
                                                    _screenTexture.SetData(0, rect, full, 0, localLenFull);
                                                    try
                                                    {
                                                        var probesTex = new Color[3];
                                                        try
                                                        {
                                                            _screenTexture.GetData(0, new Rectangle(0, 0, 1, 1), probesTex, 0, 1);
                                                            _screenTexture.GetData(0, new Rectangle(_canvas.width / 2, _canvas.height / 2, 1, 1), probesTex, 1, 1);
                                                            _screenTexture.GetData(0, new Rectangle(Math.Max(0, _canvas.width - 1), Math.Max(0, _canvas.height - 1), 1, 1), probesTex, 2, 1);
                                                            uint pv0 = ((uint)probesTex[0].A << 24) | ((uint)probesTex[0].R << 16) | ((uint)probesTex[0].G << 8) | probesTex[0].B;
                                                            uint pv1 = ((uint)probesTex[1].A << 24) | ((uint)probesTex[1].R << 16) | ((uint)probesTex[1].G << 8) | probesTex[1].B;
                                                            uint pv2 = ((uint)probesTex[2].A << 24) | ((uint)probesTex[2].R << 16) | ((uint)probesTex[2].G << 8) | probesTex[2].B;
                                                            DebugUtil.Debug($"ASMO: post-SetData FULL texture-probes top-left=0x{pv0:X8} center=0x{pv1:X8} bottom-right=0x{pv2:X8}");
                                                        }
                                                        catch { }

                                                        try
                                                        {
                                                            uint srcChk = 0;
                                                            for (int i = 0; i < localLenFull; i++)
                                                            {
                                                                var c = full[i];
                                                                uint v = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                                                                srcChk ^= v;
                                                            }

                                                            var readback = new Color[localLenFull];
                                                            try { _screenTexture.GetData(0, rect, readback, 0, localLenFull); } catch { }

                                                            uint rbChk = 0;
                                                            for (int i = 0; i < localLenFull && i < readback.Length; i++)
                                                            {
                                                                var c = readback[i];
                                                                uint v = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                                                                rbChk ^= v;
                                                            }
                                                            DebugUtil.Debug($"ASMO: FULL upload checksum src=0x{srcChk:X8} tex=0x{rbChk:X8}");
                                                        }
                                                        catch { }
                                                    }
                                                    catch { }
                                                }
                                                catch
                                                {
                                                        try
                                                        {
                                                            // log fallback
                                                            if (localLenFull > 0)
                                                            {
                                                                var fcol = full[0];
                                                                uint fv = ((uint)fcol.A << 24) | ((uint)fcol.R << 16) | ((uint)fcol.G << 8) | fcol.B;
                                                                DebugUtil.Debug($"ASMO: SetData FULL fallback len={localLenFull} first=0x{fv:X8}");
                                                            }
                                                        }
                                                        catch { }
                                                        _screenTexture.SetData(full);
                                                        try
                                                        {
                                                            var probesTexFb = new Color[3];
                                                            try
                                                            {
                                                                _screenTexture.GetData(0, new Rectangle(0, 0, 1, 1), probesTexFb, 0, 1);
                                                                _screenTexture.GetData(0, new Rectangle(_canvas.width / 2, _canvas.height / 2, 1, 1), probesTexFb, 1, 1);
                                                                _screenTexture.GetData(0, new Rectangle(Math.Max(0, _canvas.width - 1), Math.Max(0, _canvas.height - 1), 1, 1), probesTexFb, 2, 1);
                                                                uint pv0fb = ((uint)probesTexFb[0].A << 24) | ((uint)probesTexFb[0].R << 16) | ((uint)probesTexFb[0].G << 8) | probesTexFb[0].B;
                                                                uint pv1fb = ((uint)probesTexFb[1].A << 24) | ((uint)probesTexFb[1].R << 16) | ((uint)probesTexFb[1].G << 8) | probesTexFb[1].B;
                                                                uint pv2fb = ((uint)probesTexFb[2].A << 24) | ((uint)probesTexFb[2].R << 16) | ((uint)probesTexFb[2].G << 8) | probesTexFb[2].B;
                                                                DebugUtil.Debug($"ASMO: post-SetData FULL fallback texture-probes top-left=0x{pv0fb:X8} center=0x{pv1fb:X8} bottom-right=0x{pv2fb:X8}");
                                                            }
                                                            catch { }

                                                            try
                                                            {
                                                                uint srcChkFb = 0;
                                                                for (int i = 0; i < localLenFull; i++)
                                                                {
                                                                    var c = full[i];
                                                                    uint v = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                                                                    srcChkFb ^= v;
                                                                }

                                                                var readbackFb = new Color[localLenFull];
                                                                try { _screenTexture.GetData(readbackFb); } catch { }
                                                                uint rbChkFb = 0;
                                                                for (int i = 0; i < readbackFb.Length; i++)
                                                                {
                                                                    var c = readbackFb[i];
                                                                    uint v = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                                                                    rbChkFb ^= v;
                                                                }
                                                                DebugUtil.Debug($"ASMO: FULL fallback checksum src=0x{srcChkFb:X8} tex=0x{rbChkFb:X8}");
                                                            }
                                                            catch { }
                                                        }
                                                        catch { }
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        try { pool.Return(full); } catch { }
                                    }
                                });
                            }
                            else
                            {
                                foreach (var r in regions)
                                {
                                    int dx = Math.Max(0, r.X);
                                    int dy = Math.Max(0, r.Y);
                                    int dw = Math.Min(_canvas.width - dx, r.Width);
                                    int dh = Math.Min(_canvas.height - dy, r.Height);
                                    if (dw <= 0 || dh <= 0) continue;
                                    int len = dw * dh;
                                    Color[] regionData = pool.Rent(len);
                                    for (int yy = 0; yy < dh; yy++)
                                    {
                                        Array.Copy(_canvas.PixelData, (dy + yy) * _canvas.width + dx, regionData, yy * dw, dw);
                                    }
                                    try
                                    {
                                        // sample region data for logging
                                        if (len > 0)
                                        {
                                            var fcol = regionData[0];
                                            var mcol = regionData[len / 2];
                                            var lcol = regionData[len - 1];
                                            uint fv = ((uint)fcol.A << 24) | ((uint)fcol.R << 16) | ((uint)fcol.G << 8) | fcol.B;
                                            uint mv = ((uint)mcol.A << 24) | ((uint)mcol.R << 16) | ((uint)mcol.G << 8) | mcol.B;
                                            uint lv = ((uint)lcol.A << 24) | ((uint)lcol.R << 16) | ((uint)lcol.G << 8) | lcol.B;
                                            DebugUtil.Debug($"ASMO: Enqueue REGION upload rect={dx},{dy},{dw},{dh} len={len} first=0x{fv:X8} mid=0x{mv:X8} last=0x{lv:X8}");
                                        }
                                    }
                                    catch { }

                                    // Capture local copies for closure
                                    var localRegion = new Rectangle(dx, dy, dw, dh);
                                    var localData = regionData;
                                    int localLen = len;
                                    _renderQueue.Enqueue(() =>
                                    {
                                        try
                                        {
                                            lock (_graphicsLock)
                                            {
                                                if (_screenTexture != null)
                                                {
                                                    try
                                                    {
                                                        try
                                                        {
                                                            if (localLen > 0)
                                                            {
                                                                var fcol = localData[0];
                                                                var mcol = localData[localLen / 2];
                                                                var lcol = localData[localLen - 1];
                                                                uint fv = ((uint)fcol.A << 24) | ((uint)fcol.R << 16) | ((uint)fcol.G << 8) | fcol.B;
                                                                uint mv = ((uint)mcol.A << 24) | ((uint)mcol.R << 16) | ((uint)mcol.G << 8) | mcol.B;
                                                                uint lv = ((uint)lcol.A << 24) | ((uint)lcol.R << 16) | ((uint)lcol.G << 8) | lcol.B;
                                                                DebugUtil.Debug($"ASMO: SetData REGION rect={localRegion} len={localLen} first=0x{fv:X8} mid=0x{mv:X8} last=0x{lv:X8}");
                                                            }
                                                        }
                                                        catch { }
                                                        _screenTexture.SetData(0, localRegion, localData, 0, localLen);
                                                        try
                                                        {
                                                            var probesTexReg = new Color[3];
                                                            try
                                                            {
                                                                _screenTexture.GetData(0, new Rectangle(localRegion.X, localRegion.Y, 1, 1), probesTexReg, 0, 1);
                                                                _screenTexture.GetData(0, new Rectangle(localRegion.X + Math.Max(0, localRegion.Width / 2), localRegion.Y + Math.Max(0, localRegion.Height / 2), 1, 1), probesTexReg, 1, 1);
                                                                _screenTexture.GetData(0, new Rectangle(Math.Min(_canvas.width - 1, localRegion.X + localRegion.Width - 1), Math.Min(_canvas.height - 1, localRegion.Y + localRegion.Height - 1), 1, 1), probesTexReg, 2, 1);
                                                                uint prv0 = ((uint)probesTexReg[0].A << 24) | ((uint)probesTexReg[0].R << 16) | ((uint)probesTexReg[0].G << 8) | probesTexReg[0].B;
                                                                uint prv1 = ((uint)probesTexReg[1].A << 24) | ((uint)probesTexReg[1].R << 16) | ((uint)probesTexReg[1].G << 8) | probesTexReg[1].B;
                                                                uint prv2 = ((uint)probesTexReg[2].A << 24) | ((uint)probesTexReg[2].R << 16) | ((uint)probesTexReg[2].G << 8) | probesTexReg[2].B;
                                                                DebugUtil.Debug($"ASMO: post-SetData REGION texture-probes rect={localRegion} vals=0x{prv0:X8},0x{prv1:X8},0x{prv2:X8}");
                                                            }
                                                            catch { }

                                                            try
                                                            {
                                                                uint srcChkR = 0;
                                                                for (int i = 0; i < localLen; i++)
                                                                {
                                                                    var c = localData[i];
                                                                    uint v = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                                                                    srcChkR ^= v;
                                                                }

                                                                var readbackR = new Color[localLen];
                                                                try { _screenTexture.GetData(0, localRegion, readbackR, 0, localLen); } catch { }
                                                                uint rbChkR = 0;
                                                                for (int i = 0; i < localLen && i < readbackR.Length; i++)
                                                                {
                                                                    var c = readbackR[i];
                                                                    uint v = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                                                                    rbChkR ^= v;
                                                                }
                                                                DebugUtil.Debug($"ASMO: REGION upload checksum rect={localRegion} src=0x{srcChkR:X8} tex=0x{rbChkR:X8}");
                                                            }
                                                            catch { }
                                                        }
                                                        catch { }
                                                    }
                                                    catch
                                                    {
                                                        try
                                                        {
                                                            if (localLen > 0)
                                                            {
                                                                var fcol = localData[0];
                                                                uint fv = ((uint)fcol.A << 24) | ((uint)fcol.R << 16) | ((uint)fcol.G << 8) | fcol.B;
                                                                DebugUtil.Debug($"ASMO: SetData REGION fallback len={localLen} first=0x{fv:X8}");
                                                            }
                                                        }
                                                        catch { }
                                                        try { _screenTexture.SetData(localData); } catch { }
                                                        try
                                                        {
                                                            var probesTexRegFb = new Color[1];
                                                            try
                                                            {
                                                                _screenTexture.GetData(probesTexRegFb);
                                                                uint pv = ((uint)probesTexRegFb[0].A << 24) | ((uint)probesTexRegFb[0].R << 16) | ((uint)probesTexRegFb[0].G << 8) | probesTexRegFb[0].B;
                                                                DebugUtil.Debug($"ASMO: post-SetData REGION fallback texture-probe first=0x{pv:X8}");
                                                            }
                                                            catch { }
                                                        }
                                                        catch { }
                                                    }
                                                }
                                            }
                                        }
                                        finally
                                        {
                                            try { pool.Return(localData); } catch { }
                                        }
                                    });
                                }
                            }
                        }
                    }
                    
                    finally
                    {
                        swUpload.Stop();
                        _canvas.ClearDirty();
                        if (swUpload.ElapsedMilliseconds > 10)
                        {
                            DebugUtil.Debug($"ASMO: texture upload took {swUpload.ElapsedMilliseconds}ms for {regions.Count} regions (totalPixels={totalPixels})");
                        }
                    }
                }
            }

            base.Update(gameTime);

            _prevKeyboardState = keyboard;
        }

        protected override void Draw(GameTime gameTime)
        {
            // Process all pending render queue actions on the main thread
            while (_renderQueue.TryDequeue(out var work))
            {
                try
                {
                    work?.Invoke();
                }
                catch (Exception ex)
                {
                    DebugUtil.Debug("ASMO: main thread render task exception: " + ex.Message);
                }
            }

            // Use the MonoGame drawing code which uses the engine's SpriteBatch/Texture.
            {
                GraphicsDevice.Clear(Color.Black);

                // Draw the pixel buffer scaled to the window using point sampling
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                lock (_graphicsLock)
                {
                    try
                    {
                        if (_screenTexture != null)
                        {
                            _spriteBatch.Draw(
                                _screenTexture,
                                new Rectangle(0, 0, (int)(_bufferWidth * _scale), (int)(_bufferHeight * _scale)),
                                Color.White
                            );
                        }
                    }
                    catch { }
                }
                _spriteBatch.End();

                // Draw FPS and console overlay using a regular sprite batch (no point sampling required)
                _spriteBatch.Begin();
                _spriteBatch.DrawString(_font, $"FPS: {_fpsDisplay:F1}", new Vector2(4, 4), Color.White);

                lock (_consoleTexts)
                {
                    foreach (var t in _consoleTexts)
                    {
                        _spriteBatch.DrawString(_font, t.text, new Vector2(t.x, t.y), t.color);
                    }
                }
                _spriteBatch.End();
                // Allow hosted native game to draw using engine SpriteBatch if it supports it.
                if (_nativeGame is Adamantite.GFX.IConsoleGameWithSpriteBatch sbGame)
                {
                    try { sbGame.Draw(_spriteBatch, _font, _scale); } catch { }
                }

            base.Draw(gameTime);
        }
        }

        // Background render worker: executes queued texture upload actions.
        // No background render worker; all render queue actions are processed on the main thread.


        private static string CompileCSharp(string filePath)
        {
            // Console.WriteLine($"Parsing file: {filePath}");
            // // Read the file content
            // string code = File.ReadAllText(filePath);
            // // Parse the code into a SyntaxTree (the AST)
            // SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            // // Create compilation for semantic analysis
            // var compilation = CSharpCompilation.Create("OIRTemp")
            //     .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            //     .AddReferences(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
            //     .AddReferences(MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location))
            //     .AddReferences(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location))
            //     .AddSyntaxTrees(tree);
            // var semanticModel = compilation.GetSemanticModel(tree);
            // // Get the root of the AST
            // CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();
            // // Create ObjectIR module
            // var module = new Module(Path.GetFileNameWithoutExtension(filePath));
            // // Traverse the AST to extract information and build IR
            // var walker = new CodeWalker(semanticModel, module, true);
            // walker.Visit(root);
            // // Output ObjectIR
            // var serializer = new ModuleSerializer(module);
            // var oir = serializer.DumpToIRCode();

            return "// C# to ObjectIR compilation is not yet implemented.";

        }

        // Pixel buffer manipulation has been moved to `Adamantite.GFX.Canvas`.
        /// <summary>
        /// loads the entrypoint for a ObjectIR file
        /// </summary>
        /// <param name="FileName">the file to load from the filesystem</param>
        public void LoadEntry(string FileName)
        {
            // Load the entry file text and create a new runtime from it.
            if (!System.IO.File.Exists(FileName))
            {
                Console.WriteLine($"LoadEntry: file not found: {FileName}");
                return;
            }

            string text;
            // If a C# source file is provided, compile it to TextIR/ObjectIR first
            if (string.Equals(Path.GetExtension(FileName), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    text = CompileCSharp(FileName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("CompileCSharp error: " + ex.Message);
                    return;
                }
            }
            else
            {
                text = System.IO.File.ReadAllText(FileName);
            }

            // Create runtime from the file contents (auto-detect format)
            try
            {
                _runtime = new IRRuntime(text);
                _runtime.EnableReflectionNativeMethods = true;
                _runtime = RegisterRuntimeBindings(_runtime);
                _runtime.OnException += (ex) => Console.WriteLine("Runtime Exception: " + ex.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("LoadEntry error: " + ex.Message);
            }
        }

        private IRRuntime RegisterRuntimeBindings(IRRuntime runtime)
        {
            // Pixel bindings
            runtime.RegisterNativeMethod("OCRuntime.PixelBindings.SetPixel(int64,int64,int64)", (i, args) =>
            {
                if (args.Length >= 3 && args[0] is long lx && args[1] is long ly && args[2] is long lc)
                {
                    _canvas?.SetPixel((int)lx, (int)ly, Canvas.ColorFromLong(lc));
                }
                return null;
            });

            runtime.RegisterNativeMethod("OCRuntime.PixelBindings.FillRect(int64,int64,int64,int64,int64)", (i, args) =>
            {
                if (args.Length >= 5 && args[0] is long sx && args[1] is long sy && args[2] is long w && args[3] is long h && args[4] is long c)
                {
                    _canvas?.DrawFilledRect((int)sx, (int)sy, (int)w, (int)h, Canvas.ColorFromLong(c));
                }
                return null;
            });

            runtime.RegisterNativeMethod("OCRuntime.PixelBindings.Clear(int64)", (i, args) =>
            {
                if (args.Length >= 1 && args[0] is long c)
                {
                    _canvas?.Clear(Canvas.ColorFromLong(c));
                }
                return null;
            });

            // Console
            runtime.RegisterNativeMethod("OCRuntime.Console.Cls(int64)", (i, args) =>
            {
                if (args.Length >= 1 && args[0] is long c)
                {
                    _canvas?.Clear(Canvas.ColorFromLong(c));
                    lock (_consoleTexts)
                    {
                        _consoleTexts.Clear();
                    }
                }
                return null;
            });

            runtime.RegisterNativeMethod("OCRuntime.Console.PSet(int64,int64,int64)", (i, args) =>
            {
                if (args.Length >= 3 && args[0] is long lx && args[1] is long ly && args[2] is long lc)
                {
                    _canvas?.SetPixel((int)lx, (int)ly, Canvas.ColorFromLong(lc));
                }
                return null;
            });

            runtime.RegisterNativeMethod("OCRuntime.Console.RectFill(int64,int64,int64,int64,int64)", (i, args) =>
            {
                if (args.Length >= 5 && args[0] is long sx && args[1] is long sy && args[2] is long w && args[3] is long h && args[4] is long c)
                {
                    _canvas?.DrawFilledRect((int)sx, (int)sy, (int)w, (int)h, Canvas.ColorFromLong(c));
                }
                return null;
            });

            runtime.RegisterNativeMethod("OCRuntime.Console.Prin(string,int64,int64,int64)", (i, args) =>
            {
                if (args.Length >= 4 && args[0] is string s && args[1] is long px && args[2] is long py && args[3] is long pc)
                {
                    lock (_consoleTexts)
                    {
                        _consoleTexts.Add((s, (int)px, (int)py, Canvas.ColorFromLong(pc)));
                    }
                }
                return null;
            });

            // Audio: play one-shot by asset name
            runtime.RegisterNativeMethod("OCRuntime.Audio.PlayOneShot(string)", (i, args) =>
            {
                if (args.Length >= 1 && args[0] is string asset)
                {
                    try
                    {
                        if (Subsystem.Sound != null) Subsystem.Sound.PlayOneShot(asset);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Audio.PlayOneShot error: " + ex.Message);
                    }
                }
                return null;
            });

            runtime.RegisterNativeMethod("OCRuntime.Audio.ButtonClick()", (i, args) =>
            {
                try
                {
                    if (Subsystem.Sound != null)
                    {
                        Button.PlayClick(Subsystem.Sound);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Audio.ButtonClick error: " + ex.Message);
                }
                return null;
            });

            // Synth: generate a sine note and play via SoundSystem precomputed cache.
            runtime.RegisterNativeMethod("OCRuntime.Audio.SynthPlay(int64,double)", (i, args) =>
            {
                // args: midiNote (int64), durationSeconds (double)
                try
                {
                    if (args.Length >= 2 && args[0] is long midi && (args[1] is double || args[1] is float || args[1] is long))
                    {
                        int midiNote = (int)midi;
                        double dur = Convert.ToDouble(args[1]);
                        var key = $"synth_{midiNote}_{dur}";
                        if (Subsystem.Sound != null)
                        {
                            var se = Subsystem.Sound.GetOrCreatePrecomputed(key, () =>
                            {
                                var pcm = SimpleSynth.GenerateSineWavePcm((float)SimpleSynth.MidiNoteToFrequency(midiNote), (float)dur);
                                return new Microsoft.Xna.Framework.Audio.SoundEffect(pcm, 44100, Microsoft.Xna.Framework.Audio.AudioChannels.Mono);
                            });
                            Subsystem.Sound.PlayOneShot(se);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SynthPlay error: " + ex.Message);
                }
                return null;
            });
            return runtime;
        }
    }

    // Minimal local AssetCache shim to ensure AssetCache.Register can be called from this file.
    // This stores Texture2D instances by key and avoids a compilation error when the real
    // AssetCache isn't available in the current build context.
    static class AssetCache
    {
        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private static readonly object _lock = new object();

        public static void Register(string key, Texture2D tex)
        {
            if (string.IsNullOrEmpty(key) || tex == null) return;
            lock (_lock)
            {
                _cache[key] = tex;
            }
        }

        public static bool TryGet(string key, out Texture2D tex)
        {
            tex = null;
            if (string.IsNullOrEmpty(key)) return false;
            lock (_lock)
            {
                return _cache.TryGetValue(key, out tex);
            }
        }
    }
}
