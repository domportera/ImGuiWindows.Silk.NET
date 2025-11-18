using Silk.NET.Windowing;

namespace ImGuiWindows;

public interface IImguiWindowProvider
{
    public IWindowImplementation CreateWindow(in WindowOptions options, ImGuiWindow? parent);

    public FontPack? FontPack { get; }
    WindowOptions? DefaultOptions => null;
    WindowSizeFlags? DefaultSizeFlags => null;
}