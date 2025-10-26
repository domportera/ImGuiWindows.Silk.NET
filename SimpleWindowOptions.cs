using System.Numerics;

namespace ImGuiWindows
{
    public readonly record struct SimpleWindowOptions(
        Vector2 Size,
        int Fps,
        bool Vsync,
        WindowSizeFlags SizeFlags,
        bool AlwaysOnTop = false)
    {
        public bool IsResizable =>
            SizeFlags.HasFlag(WindowSizeFlags.ResizeGui);
    }

    [Flags]
    public enum WindowSizeFlags
    {
        None = 0,
        ResizeWindow = 1 << 0,
        ResizeGui = 1 << 1,
    }
}