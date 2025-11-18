using System.Collections.Concurrent;
using System.Diagnostics;
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
    private IWindow? _current;

    public WindowRunner(IImguiWindowProvider windowProvider, SynchronizationContext? mainThreadContext = null)
    {
        _windowProvider = windowProvider;
        var context = mainThreadContext ?? SynchronizationContext.Current ?? new SynchronizationContext();
        MainThreadContext = context;
    }

    internal IReadOnlyList<IWindow> Windows => _windows;
    private readonly List<IWindow> _windows = new();
    public SynchronizationContext MainThreadContext { get; }
    
    // todo - runtime font updates while window is running
    private bool IsClosed(IImguiDrawer drawer)
    {
        for (int i = 0; i < _windows.Count; i++)
        {
            if (_windows[i] is ImGuiWindow window && window.Drawer == drawer)
            {
                return window.IsClosing;
            }
        }

        #if DEBUG
        Console.Error.WriteLine("Drawer not found");
        #endif
        return true;
    }


    public async Task<TData?> Show<TData>(string title, IImguiDrawer<TData> drawer, SimpleWindowOptions? options = null)
    {
        await Show(title, (IImguiDrawer)drawer, options).ConfigureAwait(false);
        return drawer.Result;
    }

    // we can't simply return the result here, because nullable type constraints dont work between reference and value types
    public async Task Show<TData>(string title, AsyncImguiDrawer<TData> drawer, Action<TData> assign,
        SimpleWindowOptions? options = null)
    {
        var windowTask = Show(title, drawer, options);

        await foreach (var result in drawer.GetResults().ConfigureAwait(false))
        {
            if (result != null)
                assign(result);
        }

        await windowTask.ConfigureAwait(false);
    }

    public async Task Show(string title, IImguiDrawer drawer, SimpleWindowOptions? options = null)
    {
        var created = false;
        
        Dispatch(() =>
        {
            CreateWindow(title, options, drawer, _windowProvider);
            created = true;
        });
        
        while (!created || !IsClosed(drawer))
        {
            await Task.Yield();
        }
    }

    private void Dispatch(Action action) => _actionQueue.Enqueue(action);

    private void CreateWindow(string title, SimpleWindowOptions? options, IImguiDrawer drawer,
        IImguiWindowProvider windowProvider)
    {
        var opts = ConstructWindowOptions(options, windowProvider, title);
        var parent = _current as ImGuiWindow;
        var windowImpl = windowProvider.CreateWindow(opts, parent);
        var windowHelper = new ImGuiWindow(windowImpl, drawer, parent, windowProvider.FontPack, RenderContextLock, opts,
            options?.SizeFlags ?? windowProvider.DefaultSizeFlags ?? DefaultSizeFlags);

        if (parent is null)
        {
            _windows.Add(windowHelper);
        }
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


    private static WindowOptions ConstructWindowOptions(in SimpleWindowOptions? options, IImguiWindowProvider provider,
        string title)
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

        ExecuteDispatched();

        // input events
        var windows = _windows;
        for (var index = windows.Count - 1; index >= 0; index--)
        {
            var window = windows[index];
            _current = window;
            try
            {
                window.DoEvents();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            
            ExecuteDispatched();
            _current = null;
        }

        for (var index = 0; index < windows.Count; index++)
        {
            var window = windows[index];
            _current = window;
            try
            {
                window.DoUpdate();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            
            ExecuteDispatched();
            _current = null;
        }
        
        // check for closed
        for (var index = 0; index < windows.Count; index++)
        {
            var window = windows[index];
            if (window.IsClosing)
            {
                _current = window;
                try
                {
                    window.Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }

                ExecuteDispatched();
                _current = null;
                _windows.RemoveAt(index--);
            }
        }

        if (windows.Count == 0)
        {
            ExecuteDispatched();
        }

        if (modifiedSyncContext)
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    private void ExecuteDispatched()
    {
        while (_actionQueue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }
    }

    public void Render()
    {
        for (var index = _windows.Count - 1; index >= 0; index--)
        {
            var window = _windows[index];
            if (!window.IsVisible) continue;
            
            _current = window;
            try
            {
                window.DoRender();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            
            ExecuteDispatched();
            _current = null;
        }
    }

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
    
    private readonly ConcurrentQueue<Action> _actionQueue = new();
}