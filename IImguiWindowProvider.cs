using Silk.NET.Windowing;

namespace ImGuiWindows;

public interface IImguiWindowProvider
{

    public IWindowImplementation CreateWindow(in WindowOptions options);

    public FontPack? FontPack { get; }
    WindowOptions? DefaultOptions => null;
    WindowSizeFlags? DefaultSizeFlags => null;
}