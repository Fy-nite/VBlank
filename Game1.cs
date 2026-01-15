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
using System;
using System.Collections.Generic;
using System.IO;
using static SharpIR.CSharpParser;
namespace AsmoV2
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Texture2D _screenTexture;
        private Color[] _pixelData;
        private SpriteFont _font;
        // virtual pixel buffer size (like a Pico-8 style canvas)
        private int _bufferWidth = 360;
        private int _bufferHeight = 360;
        private int _scale = 3; // how much to scale the virtual canvas to the window
        private int _frameCounter = 0;
        private double _fps = 0.0;
        private int _fpsFrameCount = 0;
        private double _fpsElapsed = 0.0;
        private IRRuntime _runtime;
        // Console overlay text commands (printed via SpriteFont on top of the pixel buffer)
        private readonly List<(string text, int x, int y, Color color)> _consoleTexts = new();

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            // set window size based on pixel buffer and scale
            _graphics.PreferredBackBufferWidth = _bufferWidth * _scale;
            _graphics.PreferredBackBufferHeight = _bufferHeight * _scale;
            // disable VSync so the GPU is not forced to sync to the display
            _graphics.SynchronizeWithVerticalRetrace = true;
            // run Update/Draw as fast as possible (uncap FPS)
            IsFixedTimeStep = false;
            _graphics.ApplyChanges();
            _runtime = new IRRuntime(FileSystem.ReadAllText("Content/Main.OIR"));
            _runtime.EnableReflectionNativeMethods = true;

     

            _runtime.RegisterNativeMethod("OCRuntime.PixelBindings.SetPixel(int64,int64,int64)", (args) =>
            {
                Console.WriteLine("SetPixel called");
                if (args.Length >= 3 && args[0] is long lx && args[1] is long ly && args[2] is long lc)
                {
                    SetPixel((int)lx, (int)ly, ColorFromLong(lc));
                }
                return null;
            });

            _runtime.RegisterNativeMethod("OCRuntime.PixelBindings.FillRect(int64,int64,int64,int64,int64)", (args) =>
            {
                Console.WriteLine("FillRect called");
                if (args.Length >= 5 && args[0] is long sx && args[1] is long sy && args[2] is long w && args[3] is long h && args[4] is long c)
                {
                    DrawFilledRect((int)sx, (int)sy, (int)w, (int)h, ColorFromLong(c));
                }
                return null;
            });

            // Register Clear(int64) binding so Text IR can call OCRuntime.PixelBindings.Clear
            _runtime.RegisterNativeMethod("OCRuntime.PixelBindings.Clear(int64)", (args) =>
            {
                Console.WriteLine("Clear called");
                if (args.Length >= 1 && args[0] is long c)
                {
                    ClearPixelBuffer(ColorFromLong(c));
                }
                return null;
            });
            // Console-style bindings (PICO-8 like)
            _runtime.RegisterNativeMethod("OCRuntime.Console.Cls(int64)", (args) =>
            {
                Console.WriteLine("Console.Cls called");
                if (args.Length >= 1 && args[0] is long c)
                {
                    ClearPixelBuffer(ColorFromLong(c));
                    lock (_consoleTexts)
                    {
                        _consoleTexts.Clear();
                    }
                }
                return null;
            });

            _runtime.RegisterNativeMethod("OCRuntime.Console.PSet(int64,int64,int64)", (args) =>
            {
                Console.WriteLine("Console.PSet called");
                if (args.Length >= 3 && args[0] is long lx && args[1] is long ly && args[2] is long lc)
                {
                    SetPixel((int)lx, (int)ly, ColorFromLong(lc));
                }
                return null;
            });

            _runtime.RegisterNativeMethod("OCRuntime.Console.RectFill(int64,int64,int64,int64,int64)", (args) =>
            {
                Console.WriteLine("Console.RectFill called");
                if (args.Length >= 5 && args[0] is long sx && args[1] is long sy && args[2] is long w && args[3] is long h && args[4] is long c)
                {
                    DrawFilledRect((int)sx, (int)sy, (int)w, (int)h, ColorFromLong(c));
                }
                return null;
            });

            _runtime.RegisterNativeMethod("OCRuntime.Console.Prin(string,int64,int64,int64)", (args) =>
            {
                Console.WriteLine("Console.Prin called");
                if (args.Length >= 4 && args[0] is string s && args[1] is long px && args[2] is long py && args[3] is long pc)
                {
                    lock (_consoleTexts)
                    {
                        _consoleTexts.Add((s, (int)px, (int)py, ColorFromLong(pc)));
                    }
                }
                return null;
            });
            // Ensure runtime exceptions are reported
            _runtime.OnException += (ex) =>
            {
                Console.WriteLine("Runtime Exception: " + ex.ToString());
            };
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
            _pixelData = new Color[_bufferWidth * _bufferHeight];
            ClearPixelBuffer(Color.Black);

            // Run the IR now that the pixel buffer and texture are initialized
            _runtime.Run();
            // Ensure the texture reflects any immediate pixel changes
            _screenTexture.SetData(_pixelData);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            //ClearPixelBuffer(Color.Black);
            _screenTexture.SetData(_pixelData);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // update fps counter based on actual draws
            _fpsFrameCount++;
            _fpsElapsed += gameTime.ElapsedGameTime.TotalSeconds;
            if (_fpsElapsed >= 1.0)
            {
                _fps = _fpsFrameCount / _fpsElapsed;
                _fpsFrameCount = 0;
                _fpsElapsed = 0.0;
            }

            // Draw the pixel buffer scaled to the window using point sampling
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(_screenTexture, new Rectangle(0, 0, _bufferWidth * _scale, _bufferHeight * _scale), Color.White);
            _spriteBatch.End();

            // Draw FPS and console overlay using a regular sprite batch (no point sampling required)
            _spriteBatch.Begin();
            _spriteBatch.DrawString(_font, $"FPS: {_fps:F1}", new Vector2(4, 4), Color.White);

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

        // Helpers for manipulating the virtual pixel buffer
        private void ClearPixelBuffer(Color c)
        {
            for (int i = 0; i < _pixelData.Length; i++) _pixelData[i] = c;
        }

        private void SetPixel(int x, int y, Color c)
        {
            if (x < 0 || x >= _bufferWidth || y < 0 || y >= _bufferHeight) return;
            _pixelData[y * _bufferWidth + x] = c;
        }

        private void DrawFilledRect(int startX, int startY, int w, int h, Color c)
        {
            for (int yy = 0; yy < h; yy++)
            {
                int py = startY + yy;
                if (py < 0 || py >= _bufferHeight) continue;
                for (int xx = 0; xx < w; xx++)
                {
                    int px = startX + xx;
                    if (px < 0 || px >= _bufferWidth) continue;
                    SetPixel(px, py, c);
                }
            }
        }

        private static Color ColorFromLong(long value)
        {
            // Expect ARGB packed into 0xAARRGGBB
            uint v = (uint)value;
            byte a = (byte)(v >> 24);
            byte r = (byte)(v >> 16);
            byte g = (byte)(v >> 8);
            byte b = (byte)(v & 0xFF);
            return new Color(r, g, b, a);
        }
    }
}
