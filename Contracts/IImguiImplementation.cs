using ImGuiNET;

namespace ImGuiWindows.Contracts;

public interface IImguiImplementation : ITemporaryObject, IDisposable
{
    public void Init(ImGuiIOPtr io);
    void RenderImDrawData(ImDrawDataPtr getDrawData);
}