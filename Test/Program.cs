using System.Numerics;
using ImGuiWindows.OpenGL;
using ImGuiNET;
using ImGuiWindows;
using ImGuiWindows.DataTypes;
using Silk.NET.Input;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using Surface = Silk.NET.Windowing.Surface;

namespace Test;

internal class Program
{
    private static void Main(string[] args)
    {
        ISurfaceApplication.Run<TestWindow>();
        ImGuiLog.EndScope();
        ImGuiLog.Shutdown();
    }
}

internal sealed class TestWindow : ISurfaceApplication
{
    private static OpenGLWindow? _window;

    public static void Initialize<TSurface>(TSurface surface) where TSurface : Surface
    {
        _window = new OpenGLWindow
        {
            Surface = surface,
            Drawer = new TestDrawer()
        };
    }
}


internal class TestDrawer : IImguiDrawer
{
    private Vector4 _color = Vector4.One;
    private float _renderSize = 1f;
    private string _textEdit = "";

    
    public void Init()
    {
    }
    
    public void Draw(double deltaSeconds, ImFonts fonts, float dpiScale, ImguiInputContext ctx, out bool shouldTerminate)
    {
        shouldTerminate = false;
        var io = ImGui.GetIO();
        ImGuiDebugging.DrawDebugInput(io, ref _renderSize, ref _textEdit);
        DrawMouseInfo(io);
        var inputContext = ctx.InputContext;
        SilkInputDebugging.DrawInputState(inputContext);
    }

    private static void DrawMouseInfo(ImGuiIOPtr io)
    {
      
    }


    public void OnClose()
    {
    }

    public void OnFileDrop(IReadOnlyList<string> filePaths)
    {
    }

    public void OnWindowFocusChanged(bool changedTo)
    {
    }
}