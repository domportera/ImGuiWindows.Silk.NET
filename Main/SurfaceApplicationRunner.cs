using Silk.NET.Windowing;

namespace ImGuiWindows;

internal class SurfaceApplicationRunner<TApplication, TDrawer> : ISurfaceApplication 
    where TApplication : SurfaceRenderer, new()
    where TDrawer : IImguiDrawer, new()
{
    public required TDrawer Drawer { private get; init; }
    public static void Initialize<TSurface>(TSurface surface) where TSurface : Surface
    {
        var surfaceApplication = new TApplication
        {
            Surface = surface,
            Drawer = new TDrawer(),
        };
    }
}