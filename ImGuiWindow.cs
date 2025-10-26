using System.Diagnostics;
using System.Numerics;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Windowing;

namespace ImGuiWindows
{
    internal sealed class ImGuiWindow: IWindow
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
        
        private IWindow Window
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

        [Conditional("DEBUG")]
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

        private CloseInfo? _stackTrace;

        private readonly record struct CloseInfo(
            string StackTrace,
            ulong UpdateCount,
            ulong EventUpdateCount,
            ulong RenderCount);

        private void OnClose()
        {
            var stackTrace = new CloseInfo(Environment.StackTrace, _updateCount, _eventUpdateCount, _renderCount);
            if (!IsClosing)
            {
                var log = stackTrace == _stackTrace ? $"with same call stack:\n{stackTrace}" : $"with different call stack:\n{_stackTrace}\n\nPrevious callstack:\n{stackTrace}";
                Console.Error.WriteLine($"{nameof(OnClose)} called twice {log}");
                return;
            }

            _stackTrace = stackTrace;
            _isClosing = true;
            _imguiHandler?.Dispose();
        }

        private bool _isClosing;

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

        private void OnWindowResize(Vector2D<int> size) => _windowImpl.OnWindowResize(size);

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
        private ulong _updateCount, _eventUpdateCount, _renderCount;

        public IImguiDrawer Drawer => _drawer;

        public bool Loaded => _imguiHandler != null;

        private bool _isDisposed;

        private void UpdateEvents()
        {
            Window.DoEvents();
            ++_eventUpdateCount;
        }

        private void UpdateWindow()
        {
            Window.DoUpdate();
            ++_updateCount;
        }

        private void Render()
        {
            Window.DoRender();
            ++_renderCount;
        }

        #region IWindow
        public bool IsClosing
        {
            get => _isClosing || _window.IsClosing;
            set => _window.IsClosing = value;
        }
        bool IViewProperties.ShouldSwapAutomatically
        {
            get => _window.ShouldSwapAutomatically;
            set => _window.ShouldSwapAutomatically = value;
        }

        bool IViewProperties.IsEventDriven
        {
            get => _window.IsEventDriven;
            set => _window.IsEventDriven = value;
        }

        bool IViewProperties.IsContextControlDisabled
        {
            get => _window.IsContextControlDisabled;
            set => _window.IsContextControlDisabled = value;
        }

        bool IWindowProperties.IsVisible
        {
            get => _window.IsVisible;
            set => _window.IsVisible = value;
        }

        Vector2D<int> IWindowProperties.Position
        {
            get => _window.Position;
            set => _window.Position = value;
        }

        Vector2D<int> IViewProperties.Size => ((IViewProperties)_window).Size;

        string IWindowProperties.Title
        {
            get => _window.Title;
            set => _window.Title = value;
        }

        WindowState IWindowProperties.WindowState
        {
            get => _window.WindowState;
            set => _window.WindowState = value;
        }

        WindowBorder IWindowProperties.WindowBorder
        {
            get => _window.WindowBorder;
            set => _window.WindowBorder = value;
        }

        bool IWindowProperties.TransparentFramebuffer => _window.TransparentFramebuffer;

        bool IWindowProperties.TopMost
        {
            get => _window.TopMost;
            set => _window.TopMost = value;
        }

        IGLContext? IWindowProperties.SharedContext => _window.SharedContext;

        string? IWindowProperties.WindowClass => _window.WindowClass;

        Vector2D<int> IWindowProperties.Size
        {
            get => _window.Size;
            set => _window.Size = value;
        }

        double IViewProperties.FramesPerSecond
        {
            get => _window.FramesPerSecond;
            set => _window.FramesPerSecond = value;
        }

        double IViewProperties.UpdatesPerSecond
        {
            get => _window.UpdatesPerSecond;
            set => _window.UpdatesPerSecond = value;
        }

        GraphicsAPI IViewProperties.API => _window.API;

        bool IViewProperties.VSync
        {
            get => _window.VSync;
            set => _window.VSync = value;
        }

        VideoMode IViewProperties.VideoMode => _window.VideoMode;

        int? IViewProperties.PreferredDepthBufferBits => _window.PreferredDepthBufferBits;

        int? IViewProperties.PreferredStencilBufferBits => _window.PreferredStencilBufferBits;

        Vector4D<int>? IViewProperties.PreferredBitDepth => _window.PreferredBitDepth;

        int? IViewProperties.Samples => _window.Samples;

        IWindow IWindowHost.CreateWindow(WindowOptions opts)
        {
            return _window.CreateWindow(opts);
        }

        IGLContext? IGLContextSource.GLContext => _window.GLContext;

        IVkSurface? IVkSurfaceSource.VkSurface => _window.VkSurface;

        INativeWindow? INativeWindowSource.Native => _window.Native;

        void IView.Initialize()
        {
            _window.Initialize();
        }

        void IView.DoRender()
        {
            Render();
        }

        void IView.DoUpdate()
        {
            UpdateWindow();
        }

        void IView.DoEvents()
        {
            UpdateEvents();
            _window.DoEvents();
        }

        void IView.ContinueEvents()
        {
            if (!IsClosing)
            {
                _window.ContinueEvents();
            }
        }

        void IView.Reset() => _window.Reset();

        void IView.Focus() => _window.Focus();

        void IView.Close() => _window.Close();

        Vector2D<int> IView.PointToClient(Vector2D<int> point) => _window.PointToClient(point);

        Vector2D<int> IView.PointToScreen(Vector2D<int> point) => _window.PointToScreen(point);

        Vector2D<int> IView.PointToFramebuffer(Vector2D<int> point) => _window.PointToFramebuffer(point);

        object IView.Invoke(Delegate d, params object[] args) => _window.Invoke(d, args);

        void IView.Run(Action onFrame) => _window.Run(onFrame);

        nint IView.Handle => _window.Handle;


        Rectangle<int> IWindow.BorderSize => _window.BorderSize;

        event Action<Vector2D<int>>? IWindow.Move
        {
            add => _window.Move += value;
            remove => _window.Move -= value;
        }

        event Action<WindowState>? IWindow.StateChanged
        {
            add => _window.StateChanged += value;
            remove => _window.StateChanged -= value;
        }

        event Action<string[]>? IWindow.FileDrop
        {
            add => _window.FileDrop += value;
            remove => _window.FileDrop -= value;
        }

        void IWindow.SetWindowIcon(ReadOnlySpan<RawImage> icons)
        {
            _window.SetWindowIcon(icons);
        }

        IWindowHost? IWindow.Parent => _window.Parent;

        IMonitor? IWindow.Monitor
        {
            get => _window.Monitor;
            set => _window.Monitor = value;
        }

        bool IView.IsClosing => ((IView)_window).IsClosing;

        double IView.Time => _window.Time;

        Vector2D<int> IView.FramebufferSize => _window.FramebufferSize;

        bool IView.IsInitialized => _window.IsInitialized;

        event Action<Vector2D<int>>? IView.Resize
        {
            add => _window.Resize += value;
            remove => _window.Resize -= value;
        }

        event Action<Vector2D<int>>? IView.FramebufferResize
        {
            add => _window.FramebufferResize += value;
            remove => _window.FramebufferResize -= value;
        }

        event Action? IView.Closing
        {
            add => _window.Closing += value;
            remove => _window.Closing -= value;
        }

        event Action<bool>? IView.FocusChanged
        {
            add => _window.FocusChanged += value;
            remove => _window.FocusChanged -= value;
        }

        event Action? IView.Load
        {
            add => _window.Load += value;
            remove => _window.Load -= value;
        }

        event Action<double>? IView.Update
        {
            add => _window.Update += value;
            remove => _window.Update -= value;
        }

        event Action<double>? IView.Render
        {
            add => _window.Render += value;
            remove => _window.Render -= value;
        }
        #endregion
    }
}