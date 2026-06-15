using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using JetBrains.Annotations;
using Silk.NET.Maths;

namespace ImGuiWindows;

public readonly struct GridRect
{
    public readonly Vector2 Min;
    public readonly Vector2 Max;

    public GridRect(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }

    public Vector2 Size => Max - Min;

    // implicit conversion to imguirect

    public static implicit operator Rectangle<float>(GridRect rect)
    {
        return new Rectangle<float>(rect.Min.ToGeneric(), (rect.Max - rect.Min).ToGeneric());
    }
}

public ref struct GridState : IDisposable
{
    private readonly Vector2 _start;
    private readonly Vector2 _cellSize;
    private readonly Vector2 _spacing;
    private readonly int _columns;

    // X = column, Y = row
    private Vector2D<int> _position;

    public GridRect this[int colX, int rowY] => Cell(new Vector2D<int>(colX, rowY));
    public GridRect this[Vector2D<int> index] => Cell(index);

    private GridState(Vector2 start, Vector2 cellSize, int columns, Vector2 spacing)
    {
        _start = start;
        _cellSize = cellSize;
        _columns = Math.Max(1, columns);
        _spacing = spacing;
        _position = new Vector2D<int>(-1, 0);
    }

    [MustUseReturnValue]
    public static GridState Begin(ReadOnlySpan<char> id, Vector2 cellSize, int columns = -1, Vector2 spacing = default)
    {
        ImGui.PushID(id);
        
        if (columns < 0)
        {
            // auto-calculate columns based on width
            var width = ImGui.GetContentRegionAvail().X;
            columns = (int)MathF.Round(width / (cellSize.X + spacing.X));
        }

        return new GridState(ImGui.GetCursorPos(), cellSize, columns, spacing);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GridRect Cell(int colX, int rowY) => Cell(new Vector2D<int>(colX, rowY));

    public GridRect Cell(Vector2D<int> position)
    {
        var min = _start + (Vector2)position * (_cellSize + _spacing);
        var max = min + _cellSize;
        return new GridRect(min, max);
    }

    public GridRect CurrentCell() => Cell(_position);

    public GridRect NextCell()
    {
        ++_position.X;
        if (_position.X >= _columns)
        {
            _position.X = 0;
            _position.Y++;
        }
        
        return Cell(_position);
    }

    public GridRect NextRow(int count = 1, bool keepColumn = false)
    {
        if (!keepColumn)
            _position.X = -1;

        _position.Y += Math.Max(1, count);
        return Cell(_position);
    }

    public void Dispose()
    {
        ImGui.PopID();
    }
}