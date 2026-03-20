using System.Drawing;
using ImGuiNET;

namespace ImGuiWindows;

public interface IImguiImplementation : IDisposed, IDisposable
{
    public void Init(ImGuiIOPtr io);
    void RenderImDrawData(ImDrawDataPtr getDrawData);
}

public interface IDisposed
{
    event Action<object>? Disposed;
}