using System.Numerics;
using Silk.NET.Input.Sdl;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;

namespace ImGuiWindows;

public sealed class WindowRunner
{
    public readonly object RenderContextLock = new();
    static WindowRunner()
    {
        SdlWindowing.RegisterPlatform();
        SdlInput.RegisterPlatform();
    }
    
    private readonly IImguiWindowProvider _windowProvider;

    public WindowRunner(IImguiWindowProvider windowProvider, SynchronizationContext? mainThreadContext = null)
    {
        _windowProvider = windowProvider;
        var context = mainThreadContext ?? SynchronizationContext.Current ?? new SynchronizationContext();
        MainThreadContext = context;
    }
    
    internal IReadOnlyList<ImGuiWindow> Windows => _windows;
    private readonly List<ImGuiWindow> _windows = new();
    public SynchronizationContext MainThreadContext { get; }
    

    // todo - runtime font updates while window is running
    private bool IsClosed(IImguiDrawer drawer)
    {
        for (int i = 0; i < _windows.Count; i++)
        {
            if (_windows[i].Drawer == drawer)
            {
                return _windows[i].IsClosed;
            }
        }
        
        throw new Exception("Drawer not found");
    }

    
    public async Task<TData?> Show<TData>(string title, IImguiDrawer<TData> drawer, SimpleWindowOptions? options = null)
    {
        await Show(title, (IImguiDrawer)drawer, options);
        return drawer.Result;
    }
    
    // we can't simply return the result here, because nullable type constraints dont work between reference and value types
    public async Task Show<TData>(string title, AsyncImguiDrawer<TData> drawer, Action<TData> assign,
        SimpleWindowOptions? options = null)
    {
        var windowTask = Show(title, drawer, options);

        await foreach (var result in drawer.GetResults())
        {
            if (result != null)
                assign(result);
        }

        await windowTask;
    }
    
    public async Task Show(string title, IImguiDrawer drawer, SimpleWindowOptions? options = null)
    {
        var previousContext = SynchronizationContext.Current;
        if (previousContext != MainThreadContext)
        {
            // shift to specified context
            SynchronizationContext.SetSynchronizationContext(MainThreadContext);
        }
        
        CreateWindow(title, options, drawer, _windowProvider);
        while (!IsClosed(drawer))
        {
            await Task.Yield();
        }
        
        SynchronizationContext.SetSynchronizationContext(previousContext);
    }

    private void CreateWindow(string title, SimpleWindowOptions? options, IImguiDrawer drawer, IImguiWindowProvider windowProvider)
    {
        var opts = ConstructWindowOptions(options, windowProvider, title);
        var windowImpl = windowProvider.CreateWindow(opts);
        var windowHelper = new ImGuiWindow(windowImpl, drawer, windowProvider.FontPack, RenderContextLock, opts, options?.SizeFlags ?? windowProvider.DefaultSizeFlags ?? DefaultSizeFlags);
        _windows.Add(windowHelper);
    }
    

    private static WindowSizeFlags DefaultSizeFlags => WindowSizeFlags.ResizeWindow | WindowSizeFlags.ResizeGui;

    private static WindowOptions DefaultOptions { get; } = new()
    {
        API = GraphicsAPI.Default,
        IsEventDriven = true,
        ShouldSwapAutomatically = true,
        IsVisible = true,
        Position = new Vector2D<int>(600, 600),
        Size = new Vector2D<int>(400, 320),
        FramesPerSecond = 60,
        UpdatesPerSecond = 60,
        PreferredDepthBufferBits = 0,
        PreferredStencilBufferBits = 0,
        PreferredBitDepth = new Vector4D<int>(8, 8, 8, 8),
        Samples = 0,
        VSync = true,
        TopMost = false,
        WindowBorder = WindowBorder.Resizable
    };


    private static WindowOptions ConstructWindowOptions(in SimpleWindowOptions? options, IImguiWindowProvider provider, string title)
    {
        var fullOptions = provider.DefaultOptions ?? DefaultOptions;
        if (options.HasValue)
        {
            var val = options.Value;
            fullOptions.Size = new Vector2D<int>((int)val.Size.X, (int)val.Size.Y);
            fullOptions.FramesPerSecond = val.Fps;
            fullOptions.VSync = val.Vsync;
            fullOptions.WindowBorder = val.SizeFlags.HasFlag(WindowSizeFlags.ResizeWindow)
                ? WindowBorder.Resizable
                : WindowBorder.Fixed;
            fullOptions.TopMost = val.AlwaysOnTop;
            fullOptions.IsEventDriven = false; // we will handle the event-driven option ourselves
        }

        fullOptions.Title = title;

        return fullOptions;
    }

    public void MainThreadUpdate()
    { 
        var previousContext = SynchronizationContext.Current;
        var modifiedSyncContext = false;
        if (previousContext != MainThreadContext)
        {
            SynchronizationContext.SetSynchronizationContext(MainThreadContext);
            modifiedSyncContext = true;
        }
        
        // input events
        var windows = _windows;
        for (int i = 0; i < windows.Count; i++)
        {
            windows[i].Window.DoEvents();
        }

        foreach (var window in windows)
        {
            window.Window.DoUpdate();
        }
        
        // check for closed
        for (var index = 0; index < windows.Count; index++)
        {
            var window = windows[index];
            if (window.IsClosed)
            {
                _windows.RemoveAt(index--);
                window.Dispose();
            }
        }

        if (modifiedSyncContext)
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    public void Render()
    {
        foreach (var window in Windows)
        {
            if (window.Loaded)
            {
                try
                {
                    window.Window.DoRender();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }
        }
    }
    
    public void AddToMainThread(Action action) => throw new NotImplementedException();

    public void ShowMessageBox(string message) => ShowMessageBox(message, "Notice");
    public void ShowMessageBox(string text, string title) => ShowMessageBox<string>(text, title, str => str);

    public T? ShowMessageBox<T>(string text, string title, Func<T, string>? toButtonLabel, params T[]? buttons)
    {
        return ShowMessageBox(text, title, toButtonLabel, null, buttons);
    }

    public T? ShowMessageBox<T>(string text, string title, Func<T, string>? toButtonLabel,
        SimpleWindowOptions? options, params T[]? buttons)
    {
        return Show(title, new MessageBox<T>(text, buttons, toButtonLabel), options ?? new SimpleWindowOptions()
        {
            Size = new Vector2(400, 200),
            SizeFlags = WindowSizeFlags.ResizeGui | WindowSizeFlags.ResizeWindow,
            AlwaysOnTop = true,
            Fps = 60,
            Vsync = true
        }).Result;
    }
}