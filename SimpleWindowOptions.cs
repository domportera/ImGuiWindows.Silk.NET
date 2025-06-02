using System.Numerics;

namespace ImGuiWindows
{
    public readonly record struct SimpleWindowOptions(
        Vector2 Size,
        int Fps,
        bool Vsync,
        bool IsResizable,
        bool AlwaysOnTop = true);
}