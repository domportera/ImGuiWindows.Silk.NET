using System.Runtime.CompilerServices;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace ImGuiWindows;

public abstract class SurfaceRenderer
{
    public required Surface Surface
    {
        init
        {
            OnInitialize(value);
            Subscribe(true, value);
        }
    }

    public required IImguiDrawer Drawer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private get;
        init
        {
            field = value;
            value.Init();
        }
    }
    
    public FontPack? FontPack { private get; init; }

    protected SurfaceRenderer()
    {
        _resizeAction = OnDrawableSizeChanged;
        _coordinateAction = OnWindowSizeChanged;
        _createAction = OnSurfaceCreated;
        _updateAction = OnSurfaceUpdate;
        _terminateAction = OnSurfaceTerminate;
        _renderAction = OnSurfaceRender;
        _lowMemoryAction = OnLowMemory;
        _fileDropAction = OnFileDrop;
        _focusChangedAction = OnWindowFocusChanged;
        _pauseAction = OnSurfacePause;
        _resumeAction = OnSurfaceResume;
    }

    protected abstract void OnInitialize(Surface surface);
    protected abstract IImguiImplementation Create(SurfaceLifecycleEvent surface);
    protected abstract void OnTerminate(SurfaceLifecycleEvent evt);
    protected abstract void BeginFrame(SurfaceTimingEvent evt, int frameBufferWidth, int frameBufferHeight);
    protected abstract void OnPause(SurfaceLifecycleEvent evt, bool isPaused);
    
    private void OnSurfacePause(SurfaceLifecycleEvent evt)
    {
        OnPause(evt, true);
    }
    
    private void OnSurfaceResume(SurfaceLifecycleEvent evt)
    {
        OnPause(evt, false);
    }
    
    private void OnSurfaceCreated(SurfaceLifecycleEvent evt)
    {
        var surface = evt.Surface;
        _inputContext = surface.CreateInput();
        var window = surface.Window;
        string title;
        if (window != null)
        {
            title = window.Title;
            _windowWidth = (int)Math.Round(window.Size.X);
            _windowHeight = (int)Math.Round(window.Size.Y);
        }
        else
        {
            title = Drawer.GetType().Name;
        }

        var drawableSize = surface.DrawableSize;
        _drawableWidth = (int)Math.Round(drawableSize.X);
        _drawableHeight = (int)Math.Round(drawableSize.Y);
        
        ImGuiLog.Debug($"[IMGUI] Creating ImGui input context for {title}");;
        _imguiInputContext = new ImguiInputContext(_inputContext, null);
        ImGuiLog.Debug($"[IMGUI] Creating ImGui implementation for {title}");;
        _imGuiImplementation = Create(evt);
        ImGuiLog.Debug($"[IMGUI] Creating ImGuiHandler for {title}");;
        _imGuiHandler = new ImGuiHandler(FontPack, _contextLock, true, title, _imGuiImplementation.Init);
    }
    
    private void OnSurfaceUpdate(SurfaceTimingEvent obj)
    {
        #if DEBUG
        if(_imguiInputContext is null)
            throw new InvalidOperationException("Input context is null");
        
        if (_imGuiHandler is null)
            throw new InvalidOperationException("ImGui handler is null");
        
        if(_inputContext is null)
            throw new InvalidOperationException("Input context is null");
        #endif
        
        
        _inputContext.Update();

        _imGuiHandler.Draw(
            drawer: Drawer,
            args: new ImGuiHandler.DrawArgs(
                display: new ImGuiHandler.DisplayInfo(_windowWidth, _windowHeight, _drawableWidth, _drawableHeight),
                deltaSeconds: obj.DeltaTime,
                inputContext: _inputContext,
                imguiInput: _imguiInputContext,
                systemWindowScaling: obj.Surface.Scale?.PixelDensity ?? _windowWidth / _drawableWidth ?? 1f), 
            shouldTerminate: out var shouldTerminate);
        

        if (shouldTerminate)
        {
            obj.Surface.Terminate();
        }
    }

    private void Render()
    {
        #if DEBUG
        if(_imGuiImplementation is null)
            throw new InvalidOperationException("ImGui implementation is null");
        
        if(_imGuiHandler is null)
            throw new InvalidOperationException("ImGui handler is null");
        #endif
        
        _imGuiHandler.Render(_imGuiImplementation);
    }

    private void OnWindowSizeChanged(WindowCoordinatesEvent evt)
    {
        _windowWidth = (int)Math.Round(evt.NewSize.X);
        _windowHeight = (int)Math.Round(evt.NewSize.Y);
    }

    private void OnDrawableSizeChanged(SurfaceResizeEvent evt)
    {
        _drawableWidth = (int)Math.Round(evt.NewSize.X);
        _drawableHeight = (int)Math.Round(evt.NewSize.Y);
    }
    

    private void OnLowMemory(SurfaceLifecycleEvent obj)
    {
        _imGuiHandler?.LowMemoryAlert();
    }

    private void OnSurfaceTerminate(SurfaceLifecycleEvent evt)
    {
        Drawer.OnClose();
        _imGuiHandler?.Dispose();
        OnTerminate(evt);
        Subscribe(false, evt.Surface);
    }

    private void OnSurfaceRender(SurfaceTimingEvent evt)
    {
        BeginFrame(evt, _drawableWidth, _drawableHeight);
        Render();
    }
    
    private void OnFileDrop(WindowFileEvent obj) => Drawer.OnFileDrop(obj.Files);
    
    private void OnWindowFocusChanged(WindowToggleEvent obj) => Drawer.OnWindowFocusChanged(obj.Value);

    private void Subscribe(bool add, Surface surface)
    {
        var window = surface.Window;
        if (add)
        {
            surface.DrawableSizeChanged += _resizeAction;
            surface.Created += _createAction;
            surface.Update += _updateAction;
            surface.Terminating += _terminateAction;
            surface.Render += _renderAction;
            surface.LowMemory += _lowMemoryAction;
            surface.Pausing += _pauseAction;
            surface.Resuming += _resumeAction;
            if (window != null)
            {
                window.CoordinatesChanged += _coordinateAction;
                window.FileDrop += _fileDropAction;
                window.FocusChanged += _focusChangedAction;
            }
        }
        else
        {
            surface.DrawableSizeChanged -= _resizeAction;
            surface.Created -= _createAction;
            surface.Update -= _updateAction;
            surface.Terminating -= _terminateAction;
            surface.Render -= _renderAction;
            surface.LowMemory -= _lowMemoryAction;
            surface.Pausing -= _pauseAction;
            surface.Resuming -= _resumeAction;

            if (window != null)
            {
                window.CoordinatesChanged -= _coordinateAction;
                window.FileDrop -= _fileDropAction;
                window.FocusChanged -= _focusChangedAction;
            }
        }
    }


    private ImguiInputContext? _imguiInputContext;
    private ImGuiHandler? _imGuiHandler;
    private IImguiImplementation? _imGuiImplementation;
    private InputContext? _inputContext;
    private int? _windowWidth;
    private int? _windowHeight;
    private int _drawableWidth;
    private int _drawableHeight;
    private readonly Lock _contextLock = new();
    private readonly Action<SurfaceResizeEvent> _resizeAction;
    private readonly Action<WindowCoordinatesEvent> _coordinateAction;
    private readonly Action<SurfaceLifecycleEvent> _createAction;
    private readonly Action<SurfaceTimingEvent> _updateAction;
    private readonly Action<SurfaceLifecycleEvent> _terminateAction;
    private readonly Action<SurfaceLifecycleEvent> _pauseAction;
    private readonly Action<SurfaceLifecycleEvent> _resumeAction;
    private readonly Action<SurfaceTimingEvent> _renderAction;
    private readonly Action<SurfaceLifecycleEvent> _lowMemoryAction;
    private readonly Action<WindowFileEvent> _fileDropAction;
    private readonly Action<WindowToggleEvent> _focusChangedAction;
    
}