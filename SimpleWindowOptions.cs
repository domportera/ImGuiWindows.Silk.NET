using System.Numerics;

namespace ImguiWindows
{
    public readonly record struct SimpleWindowOptions(
        Vector2 Size,
        int Fps,
        bool Vsync,
        bool IsResizable,
        bool AlwaysOnTop = true);
}