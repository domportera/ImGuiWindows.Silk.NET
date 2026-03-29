using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using JetBrains.Annotations;

namespace ImGuiWindows.DataTypes;

[MustDisposeResource]
public ref struct StyleScope : IDisposable
{
    public uint ColorCount { get; private set; }
    public uint StyleCount { get; private set; }

    [MustUseReturnValue]
    [MustDisposeResource]
    public static StyleScope Begin() => new(colorCount: 0, styleCount: 0);

    private StyleScope(uint colorCount, uint styleCount)
    {
        ColorCount = colorCount;
        StyleCount = styleCount;
    }

    public void Dispose()
    {
        for (; ColorCount > 0; --ColorCount)
        {
            ImGui.PopStyleColor();
        }

        for (; StyleCount > 0; --StyleCount)
        {
            ImGui.PopStyleVar();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(ImGuiStyleVar var, Vector2 value)
    {
        ++StyleCount;
        ImGui.PushStyleVar(var, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushX(ImGuiStyleVar var, float value)
    {
        ++StyleCount;
        ImGui.PushStyleVarX(var, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushY(ImGuiStyleVar var, float value)
    {
        ++StyleCount;
        ImGui.PushStyleVarY(var, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(ImGuiStyleVar var, float value)
    {
        ++StyleCount;
        ImGui.PushStyleVar(var, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(ImGuiCol var, Color value)
    {
        ++ColorCount;
        ImGui.PushStyleColor(var, new UIntColor4(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(ImGuiCol var, Vector4 value)
    {
        ++ColorCount;
        ImGui.PushStyleColor(var, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(ImGuiCol var, uint value)
    {
        ++ColorCount;
        ImGui.PushStyleColor(var, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(ImGuiCol var, int value)
    {
        ++ColorCount;
        ImGui.PushStyleColor(var, new UIntColor4(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(ImGuiCol colorParam, ImGuiCol newColor)
    {
        ++ColorCount;
        ImGui.PushStyleColor(colorParam, ImGui.GetColorU32(newColor));
    }
}