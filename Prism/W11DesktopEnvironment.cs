using System;
using Adamantite.GFX;
using Canvas = Adamantite.GFX.Canvas;
using Adamantite.GPU; 
using Microsoft.Xna.Framework.Input;
using StarChart.stdlib.W11.Windowing;
using StarChart.Plugins; // If needed for PluginContext etc, but try to avoid if possible

namespace StarChart.stdlib.W11
{
    public class W11DesktopEnvironment : IGraphicsSubsystem
    {
        private StarChart.Runtime _runtime;
        public DisplayServer Server { get; private set; }
        private Compositor _compositor;
        private Surface _mainSurface;
        
        public IStarChartWindowManager? WindowManager { get; set; }

        // Input tracking
        private bool _prevLeftDown;
        private KeyboardState _prevKeyboard;

        public W11DesktopEnvironment()
        {
            Server = new DisplayServer();
            _compositor = new Compositor();
        }

        public void Initialize(Runtime runtime)
        {
            _runtime = runtime;
        }

        public void Update(double deltaTime)
        {
            if (WindowManager != null)
            {
                WindowManager.Update();
            }
            // Check for built-in TWM if registered
            else if (Server.WindowManager is TwmManager twm)
            {
                twm.Update();
            }
        }

        public void ProcessInput(MouseState mouse, KeyboardState keyboard)
        {
            // Pass raw screen coordinates
            int mx = mouse.X;
            int my = mouse.Y;
            bool leftDown = mouse.LeftButton == ButtonState.Pressed;
            bool leftPressed = leftDown && !_prevLeftDown;
            bool leftReleased = !leftDown && _prevLeftDown;
            _prevLeftDown = leftDown;

            // Give WindowManager a chance first
            if (WindowManager != null)
            {
                WindowManager.HandleMouse(mx, my, leftDown, leftPressed, leftReleased);
            }
            // Or built-in TWM if present (legacy check or explicit integration?)
            // TwmManager implements IStarChartWindowManager, so we can cast Server.WindowManager if needed?
            // Actually Server.WindowManager is just an object in DisplayServer class?
            // DisplayServer.WindowManager property: public object? WindowManager { get; set; }
            // So we can cast.
            else if (Server.WindowManager is IStarChartWindowManager iTwm)
            {
                iTwm.HandleMouse(mx, my, leftDown, leftPressed, leftReleased);
            }
            
            // If no window manager, emulate X11 "click to focus" behaviour
            if (WindowManager == null && !(Server.WindowManager is IStarChartWindowManager))
            {
                 // iterate from top-most to bottom
                for (int i = Server.Windows.Count - 1; i >= 0; i--)
                {
                    var w = Server.Windows[i];
                    if (!w.IsMapped || w.IsDestroyed) continue;
                    
                    int wScale = Math.Max(1, w.Scale);
                    int onscreenW = w.Canvas.Width * wScale;
                    int onscreenH = w.Canvas.Height * wScale;
                    if (mx < w.Geometry.X || my < w.Geometry.Y) continue;
                    if (mx >= w.Geometry.X + onscreenW || my >= w.Geometry.Y + onscreenH) continue;

                    // We hit window `w` — focus/raise on press
                    if (leftPressed)
                    {
                        Server.BringToFront(w);
                        Server.FocusWindow(w);
                    }
                    else
                    {
                        // Optional: Mouse move events?
                    }
                    break;
                }
            }

            _prevKeyboard = keyboard;
        }

        public void Render(Canvas target)
        {
            if (target == null) return;

            if (_mainSurface == null || _mainSurface.Width != target.width || _mainSurface.Height != target.height)
            {
                _mainSurface = new Surface(target.width, target.height);
            }

            if (Server.Windows.Count > 0)
            {
                 _compositor.Compose(Server, _mainSurface);
                 
                 // Transfer surface pixels to Canvas
                 if (_mainSurface.Pixels != null && target.PixelData != null)
                 {
                     // Faster copy if possible, but safe loop for now
                     // Assuming same size
                     int len = _mainSurface.Pixels.Length;
                     if (target.PixelData.Length < len) len = target.PixelData.Length;
                     
                     for (int i = 0; i < len; i++)
                     {
                        uint p = _mainSurface.Pixels[i];
                        target.PixelData[i] = new Microsoft.Xna.Framework.Color(
                            (byte)((p >> 16) & 0xFF),
                            (byte)((p >> 8) & 0xFF),
                            (byte)(p & 0xFF),
                            (byte)((p >> 24) & 0xFF)
                        );
                     }
                     try { target.MarkDirtyRect(0, 0, target.width, target.height); } catch {}
                 }
            }
            else 
            {
                target.Clear(Microsoft.Xna.Framework.Color.Black);
            }
        }
    }
}
