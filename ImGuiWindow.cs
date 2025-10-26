using System.Numerics;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Windowing;

namespace ImGuiWindows
{
    internal sealed class ImGuiWindow
    {
        private readonly bool _autoScaleImgui;

        // todo: expose runtime-editable options
        private WindowOptions _windowOptions;

        public ImGuiWindow(IWindowImplementation window, IImguiDrawer drawer, FontPack? fontPack,
            object graphicsContextLockObj, WindowOptions windowOptions, WindowSizeFlags sizeFlags)
        {
            _autoScaleImgui = sizeFlags.HasFlag(WindowSizeFlags.ResizeGui);
            _windowOptions = windowOptions;
            _windowImpl = window;
            _drawer = drawer;
            _fontPack = fontPack;
            _graphicsContextLock = graphicsContextLockObj;
            
            _window = Silk.NET.Windowing.Window.Create(windowOptions);
            SubscribeToWindow(_window);
            _window.Initialize();
        }
        
        public IWindow Window
        {
            get
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this); 
                return _window;
            }
        }

        public void RunUntilClosed()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this); 
            _window.Run();
            Dispose();
        }

        public void Dispose()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this); 
            _isDisposed = true;
            UnsubscribeFromWindow(_window);
            
            try
            {
                _graphicsContext?.Dispose();
                _inputContext?.Dispose();
                _window.Dispose();

                _windowImpl.Dispose();
                _graphicsContext = null;
                _inputContext = null;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error disposing of window: {e}");
            }
        }

        private void UnsubscribeFromWindow(IWindow window)
        {
            window.Load -= OnLoad;
            window.Render -= RenderWindowContents;
            window.FramebufferResize -= OnWindowResize;
            window.Update -= OnWindowUpdate;
            window.FocusChanged -= OnFocusChanged;
            window.Closing -= OnClose;
            window.FileDrop -= OnFileDrop;
        }

        private void SubscribeToWindow(IWindow window)
        {
            window.Load += OnLoad;
            window.Render += RenderWindowContents;
            window.FramebufferResize += OnWindowResize;
            window.Update += OnWindowUpdate;
            window.FocusChanged += OnFocusChanged;
            window.Closing += OnClose;
            window.FileDrop += OnFileDrop;
        }

        private void OnFileDrop(string[] filePaths)
        {
            _imguiHandler?.OnFileDrop(filePaths);
        }

        private void OnFocusChanged(bool isFocused)
        {
            if (!isFocused && _windowOptions.TopMost)
            {
                // todo: force re-focus once silk.NET supports that ? wayland may not allow it anyway..
            }

            _imguiHandler?.OnWindowFocusChanged(isFocused);
        }

        private void RenderWindowContents(double deltaTime)
        {
#if WH_DEBUG_FLOW
        Console.WriteLine("Starting render");
#endif

            DebugMouse("RenderWindowContents");
            lock (_graphicsContextLock)
            {
                var windowSize = _window.Size;
                var clearColor = _imguiHandler?.ClearColor ?? _windowImpl.DefaultClearColor;

                if (_windowImpl.Render(clearColor, deltaTime))
                {
                    _imguiHandler?.Draw(new Vector2(windowSize.X, windowSize.Y), deltaTime, _windowScale ?? 1);
                    _windowImpl.EndRender();
                }
            }
        }

        private void DebugMouse(string callsite)
        {
            // var mice = _inputContext!.Mice;
            // int mCounter = 0;
            // int wCounter = 0;
            // foreach(var mouse in mice)
            // {
            //     mCounter++;
            //     foreach (var wheel in mouse.ScrollWheels)
            //     {
            //         wCounter++;
            //         Console.WriteLine($"scroll in mouse {mouse.Name} ({mCounter}, {wCounter}) at {callsite}:" + wheel.Y);
            //     }
            //     
            //     wCounter = 0;
            // }
        }

        private void OnLoad()
        {
            _graphicsContext = _windowImpl.InitializeGraphicsAndInputContexts(_window, out _inputContext);
            _imguiHandler = new ImGuiHandler(_windowImpl.GetImguiImplementation(), _drawer, _fontPack,
                _graphicsContextLock, _autoScaleImgui);
        }

        private void OnClose()
        {
            _imguiHandler?.Dispose();
            IsClosed = true;
        }

        private void OnWindowUpdate(double deltaSeconds)
        {
            DebugMouse("OnWindowUpdate");
            if (_imguiHandler == null) return;

            _windowScale = GetWindowScale(_window);
            _imguiHandler.OnWindowUpdate(deltaSeconds, out var shouldCloseWindow);
            if (shouldCloseWindow)
            {
                _window.Close();
            }
        }

        private void OnWindowResize(Vector2D<int> size)
        {
            _windowImpl.OnWindowResize(size);
        }
        

        private unsafe float GetWindowScale(IWindow window)
        {
            using var sdl = Sdl.GetApi();
            var sdlWindow = (Silk.NET.SDL.Window*)window.Handle;
            int displayIndex = sdl.GetWindowDisplayIndex(sdlWindow);

            float ddpi = 0, hdpi = 0, vdpi = 0;
            if (sdl.GetDisplayDPI(displayIndex, &ddpi, &hdpi, &vdpi) == 0)
            {
                // Standard DPI is usually 96
                return 96f / ddpi;
            }

            // Fallback if DPI can't be retrieved
            return 1f;
        }


        private readonly object _graphicsContextLock;
        private readonly IWindowImplementation _windowImpl;
        private readonly IWindow _window;
        private IInputContext? _inputContext;
        private NativeAPI? _graphicsContext;
        private readonly FontPack? _fontPack;

        private readonly IImguiDrawer _drawer;
        private ImGuiHandler? _imguiHandler;
        private float? _windowScale;
        public bool IsClosed { get; private set; }

        public IImguiDrawer Drawer => _drawer;

        public bool Loaded => _imguiHandler != null;

        private bool _isDisposed;
    }
}