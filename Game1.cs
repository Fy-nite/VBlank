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
using AsmoV2.AudioEngine;
using ObjectIR.MonoGame.SFX;
using Adamantite.GFX;
using System.Collections.Generic;
using System.IO;
using static SharpIR.CSharpParser;

namespace AsmoV2
{
    public class AsmoGameEngine : Game
    {
        public GraphicsDeviceManager _graphics;
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
        private readonly List<(string text, int x, int y, Color color)> _consoleTexts = new();

        public AsmoGameEngine(string name, int width, int height, float scale = 0.75f)
        {
            // set name, width and height based on parameters
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _bufferHeight = height;
            _bufferWidth = width;
            _scale = scale;
            // set window size based on pixel buffer and scale
            //_graphics.PreferredBackBufferWidth = _bufferWidth * _scale;
            //_graphics.PreferredBackBufferHeight = _bufferHeight * _scale;

            // set window size based on the requested width and height.
            _graphics.PreferredBackBufferWidth = (int)(width * _scale);
            _graphics.PreferredBackBufferHeight = (int)(height * _scale);

            // disable VSync so the GPU is not forced to sync to the display
            _graphics.SynchronizeWithVerticalRetrace = true;
            // run Update/Draw as fast as possible (uncap FPS)
            IsFixedTimeStep = false;
            _runtime = new IRRuntime(null);
            _runtime.EnableReflectionNativeMethods = true;

            _graphics.ApplyChanges();

            // register builtin runtime bindings (pixels, console, audio, etc.)
            RegisterRuntimeBindings(_runtime);
        }

        /// <summary>
        /// Host a native C# game (must implement IConsoleGame). Call before Run()/LoadContent.
        /// </summary>
        public void HostNativeGame(Adamantite.GFX.IConsoleGame native)
        {
            _nativeGame = native ?? throw new ArgumentNullException(nameof(native));
        }
        public void Init(string name, int width, int height)
        {
            
        }
        protected override void Initialize()
        {
            // TODO: Add initialization logic here

            // Do not run the IR until LoadContent has created the pixel buffer/texture
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // load a SpriteFont (Content/DefaultFont.spritefont)
            _font = Content.Load<SpriteFont>("DefaultFont");

            // create the Texture2D that will hold the pixel buffer
            _screenTexture = new Texture2D(GraphicsDevice, _bufferWidth, _bufferHeight, false, SurfaceFormat.Color);
            _canvas = new Canvas(_bufferWidth, _bufferHeight);
            _canvas.Clear(Color.Black);
            // Ensure the texture reflects any immediate pixel changes
            _screenTexture.SetData(_canvas.PixelData);

            // Initialize audio subsystem with the game's Content manager so audio assets can be loaded
            Subsystem.Initialize(Content);
            // If a native game is hosted, initialize it instead of running the IR runtime
            if (_nativeGame != null)
            {
                _nativeGame.Init(_canvas);
            }
            else
            {
                // Run the IR now that the pixel buffer and texture are initialized
                _runtime.Run();
            }
        }

        protected override void UnloadContent()
        {
            // Shutdown audio subsystem and release precomputed assets
            Subsystem.Shutdown();
            
            base.UnloadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Button.PlayClick(Subsystem.Sound);

            //ClearPixelBuffer(Color.Black);
            // If hosting a native game, update it (it will draw into the canvas). Otherwise the IR runtime updates the canvas.
            if (_nativeGame != null)
            {
                _nativeGame.Update(gameTime.ElapsedGameTime.TotalSeconds);
            }

            // Update fps counter
            _fpsCounter.Tick(gameTime.ElapsedGameTime.TotalSeconds);
            _fpsDisplay = _fpsCounter.Fps;

            if (_canvas != null) _screenTexture.SetData(_canvas.PixelData);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // update fps counter based on actual draws (value updated in Update via FpsCounter)

            // If hosting a native game, let it draw into the canvas before presenting
            if (_nativeGame != null)
            {
                _nativeGame.Draw(_canvas);
            }

            // Draw the pixel buffer scaled to the window using point sampling
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(
                _screenTexture,
                new Rectangle(0, 0, (int)(_bufferWidth * _scale), (int)(_bufferHeight * _scale)),
                Color.White
            );
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

            base.Draw(gameTime);
        }


        private static string CompileCSharp(string filePath)
        {
            Console.WriteLine($"Parsing file: {filePath}");
            // Read the file content
            string code = File.ReadAllText(filePath);
            // Parse the code into a SyntaxTree (the AST)
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            // Create compilation for semantic analysis
            var compilation = CSharpCompilation.Create("OIRTemp")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location))
                .AddSyntaxTrees(tree);
            var semanticModel = compilation.GetSemanticModel(tree);
            // Get the root of the AST
            CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();
            // Create ObjectIR module
            var module = new Module(Path.GetFileNameWithoutExtension(filePath));
            // Traverse the AST to extract information and build IR
            var walker = new CodeWalker(semanticModel, module, true);
            walker.Visit(root);
            // Output ObjectIR
            var serializer = new ModuleSerializer(module);
            var oir = serializer.DumpToIRCode();

            return oir;

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
                RegisterRuntimeBindings(_runtime);
                _runtime.OnException += (ex) => Console.WriteLine("Runtime Exception: " + ex.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("LoadEntry error: " + ex.Message);
            }
        }

        private void RegisterRuntimeBindings(IRRuntime runtime)
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
        }
    }
}
