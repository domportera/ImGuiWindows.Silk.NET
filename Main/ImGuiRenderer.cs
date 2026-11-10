using System.Runtime.CompilerServices;
using AppWindows;
using ImGuiWindows.Contracts;
using ImGuiWindows.DataTypes;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace ImGuiWindows;

/// <summary>
/// Dear ImGui implementation of <see cref="SurfaceHost" />: it owns the ImGui context and input plumbing and hands
/// every frame to the drawer.
/// </summary>
/// <remarks>
/// The graphics API is only involved through <see cref="Create" />, so an OpenGL, Vulkan or software backend can be
/// plugged in without touching this class.
/// </remarks>
public abstract class ImGuiRenderer : SurfaceHost
{
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

    /// <summary>Creates the ImGui backend for the surface, which is what ties this renderer to a graphics API.</summary>
    protected abstract IImguiImplementation Create();

    /// <summary>Releases whatever <see cref="Create" /> allocated.</summary>
    protected abstract void OnTerminate();

    protected sealed override void OnCreated(SurfaceLifecycleEvent evt)
    {
        var surface = evt.Surface;
        _inputContext = surface.CreateInput();

        var title = surface.Window?.Title ?? Drawer.GetType().Name;

        ImGuiLog.Debug($"[IMGUI] Creating ImGui implementation for {title}");
        evt.Surface.MakeCurrent();
        _imGuiImplementation = Create();

        ImGuiLog.Debug($"[IMGUI] Creating ImGui input context for {title}");
        _imguiInputContext = new ImguiInputContext(_inputContext, null);

        ImGuiLog.Debug($"[IMGUI] Creating ImGuiHandler for {title}");
        _imGuiHandler = new ImGuiHandler(FontPack, _contextLock, true, title, _imGuiImplementation.Init,
            _imguiInputContext);
    }

    protected abstract void BeginDrawContext(int width, int height);

    protected sealed override void OnFrame(SurfaceTimingEvent evt, int drawableWidth, int drawableHeight, int clientWidth, int clientHeight, float pixelDensity)
    {
        evt.Surface.MakeCurrent();
        BeginDrawContext(drawableWidth, drawableHeight);
        _inputContext!.Update();
        _imGuiHandler!.Draw(
            args: new DrawArgs(
                display: new DisplayInfo(clientWidth, clientHeight, drawableWidth, drawableHeight, pixelDensity),
                deltaSeconds: evt.DeltaTime),
            inputContext: _imguiInputContext!,
            drawer: Drawer,
            shouldTerminate: out var shouldTerminate);

        if (shouldTerminate)
        {
            Close();
        }
    }

    protected override void OnRender(SurfaceTimingEvent evt)
    {
        _imGuiHandler!.Render(_imGuiImplementation!);
    }

    protected sealed override void OnTerminated(SurfaceLifecycleEvent evt)
    {
        Drawer.OnClose();
        _imGuiHandler?.Dispose();
        OnTerminate();
        evt.Surface.OpenGL?.Dispose();
    }

    protected override void OnLowMemory(SurfaceLifecycleEvent evt) => _imGuiHandler?.LowMemoryAlert();

    protected override void OnFileDrop(WindowFileEvent evt) => Drawer.OnFileDrop(evt.Files);

    protected override void OnFocusChanged(WindowToggleEvent evt) => Drawer.OnWindowFocusChanged(evt.Value);

    private ImguiInputContext? _imguiInputContext;
    private ImGuiHandler? _imGuiHandler;
    private IImguiImplementation? _imGuiImplementation;
    private InputContext? _inputContext;
    private readonly Lock _contextLock = new();
}

