using System.Drawing;
using System.Runtime.InteropServices;

namespace ImGuiWindows.DataTypes;

[StructLayout(LayoutKind.Explicit)]
internal readonly ref struct UIntColor4
{
    [FieldOffset(0)] public readonly uint UnsignedValue;
    [FieldOffset(0)] public readonly int SignedValue;

    [FieldOffset(0)] public readonly byte R;
    [FieldOffset(1)] public readonly byte G;
    [FieldOffset(2)] public readonly byte B;
    [FieldOffset(3)] public readonly byte A;

    public static implicit operator UIntColor4(Color color) => new(color);
    public static implicit operator uint(UIntColor4 color4) => color4.UnsignedValue;
    public static implicit operator int(UIntColor4 color4) => color4.SignedValue;

    public static implicit operator (byte R, byte G, byte B, byte A)(UIntColor4 color4) =>
        (color4.R, color4.G, color4.B, color4.A);

    public static implicit operator (int R, int G, int B, int A)(UIntColor4 color4) =>
        (color4.R, color4.G, color4.B, color4.A);

    public static implicit operator (uint R, uint G, uint B, uint A)(UIntColor4 color4) =>
        (color4.R, color4.G, color4.B, color4.A);


    public UIntColor4(Color color)
    {
        R = color.R;
        G = color.G;
        B = color.B;
        A = color.A;
    }

    public UIntColor4(int value)
    {
        SignedValue = value;
    }
}