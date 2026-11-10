using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using ImGuiWindows.DataTypes;
using Silk.NET.Input;
using Color = System.Drawing.Color;

namespace ImGuiWindows;

public static class DimGui
{
    extension<T>(RangeAccessor<T> accessor) where T : struct
    {
        public Span<T> AsSpan()
        {
            unsafe
            {
                return new Span<T>(accessor.Data, accessor.Count);
            }
        }
    }

    /// <param name="value"></param>
    /// <typeparam name="T"></typeparam>
    extension<T>(T value) where T : unmanaged
    {
        /// <summary>
        /// Compares two values of the same type for equality.
        /// </summary>
        /// <remarks>
        /// This will only be faster than <see cref="EqualsUnsafe"/> if you are providing a large struct.
        /// This method does not require hardware support for vectorization, and thus will likely be slower
        /// than vectorized methods for large types.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool EqualsUnsafe(in T other)
        {
            var left = (byte*)&value;
            var right = (byte*)Unsafe.AsPointer(in other);
            var length = sizeof(T);
            var index = 0;
            ulong diff = 0;

            var maxSimdIndex = length & ~(sizeof(ulong) - 1); // equivalent to length - (length % sizeof(ulong))
            for (; index < maxSimdIndex; index += sizeof(ulong))
            {
                diff |= Unsafe.ReadUnaligned<ulong>(left + index) ^ Unsafe.ReadUnaligned<ulong>(right + index);
            }

            // remaining bytes
            for (; index < length; ++index)
            {
                diff |= (byte)(left[index] ^ right[index]);
            }

            return diff == 0;
        }
    }

    public static void DrawTextBox(ReadOnlySpan<char> text, Vector2 min, Vector2 size, bool isFilled)
    {
        ImGui.SetCursorPos(min);
        using var style = StyleScope.Begin();

        if (isFilled)
        {
            style.Push(ImGuiCol.ChildBg, Color.Pink);
        }

        if (ImGui.BeginChild(str_id: text,
                size: size,
                child_flags: ImGuiChildFlags.Borders,
                window_flags: ImGuiWindowFlags.NoDecoration |
                              ImGuiWindowFlags.NoFocusOnAppearing))
        {
            ImGui.TextWrapped(text);
        }

        ImGui.EndChild();
    }

    /// <summary>
    /// Draws a controller to the screen (using a Dualsense as a reference) and highlights the active inputs
    /// </summary>
    /// <param name="label"></param>
    /// <param name="buttons"></param>
    /// <param name="thumbSticks"></param>
    /// <param name="triggers"></param>
    public static void DrawController(ReadOnlySpan<char> label, IReadOnlyList<Button<JoystickButton>> buttons,
        DualReadOnlyList<Vector2> thumbSticks, DualReadOnlyList<float> triggers)
    {
        if (buttons == null)
            throw new ArgumentNullException(nameof(buttons));

        if (buttons.Count == 0)
            return;

        if (ImGui.BeginChild(label))
        {
            // draw a dualsense controller layout
            var surfaceMin = ImGui.GetCursorStartPos();
            var screenMin = ImGui.GetCursorScreenPos();
            var surfaceSize = ImGui.GetContentRegionAvail();

            List<(JoystickButton button, bool down)> unknown = [];

            foreach (var b in buttons)
            {
                var isDown = b.IsDown;
                if (!isDown)
                {
                    ImGui.BeginDisabled();
                }

                if (!ButtonDrawPositions01.TryGetValue(b.Name, out var info))
                {
                    unknown.Add((b.Name, b.IsDown));
                }
                else
                {
                    var min = surfaceMin + info.pos01 * surfaceSize;
                    var size = info.size01 * surfaceSize;
                    size = new Vector2(Math.Min(size.X, size.Y));

                    ImGui.SetCursorPos(min);
                    ImGui.Button(b.Name.PrettyName, size);
                }


                if (!isDown)
                {
                    ImGui.EndDisabled();
                }
            }

            // now handle thumbsticks and triggers
            var leftTriggerPosInfo = TriggerPositions.Left;
            var rightTriggerPosInfo = TriggerPositions.Right;

            DrawTrigger(
                text: "Left Trigger",
                screenMin: screenMin,
                surfaceMin: surfaceMin,
                surfaceSize: surfaceSize,
                rect01: leftTriggerPosInfo,
                value: triggers.Left);

            DrawTrigger(
                text: "Right Trigger",
                screenMin: screenMin,
                surfaceMin: surfaceMin,
                surfaceSize: surfaceSize,
                rect01: rightTriggerPosInfo,
                value: triggers.Right);

            // for thumbsticks, draw a rectangle with a dot in the middle whose position is based on the thumbstick's 2d value
            var leftThumbStickPosInfo = ThumbstickPositions.Left;
            var rightThumbStickPosInfo = ThumbstickPositions.Right;

            DrawThumbstick(
                text: "Left Thumbstick",
                screenMin: screenMin,
                surfaceMin: surfaceMin,
                surfaceSize: surfaceSize,
                rect01: leftThumbStickPosInfo,
                value: thumbSticks.Left);

            DrawThumbstick(
                text: "Right Thumbstick",
                screenMin: screenMin,
                surfaceMin: surfaceMin,
                surfaceSize: surfaceSize,
                rect01: rightThumbStickPosInfo,
                value: thumbSticks.Right);

            for (var index = 0; index < unknown.Count; index++)
            {
                var b = unknown[index];
                ImGui.PushID(index);
                ImGui.BeginDisabled();
                var down = b.down;
                var name = b.button.IsIdentified() ? b.button.PrettyName : $"Unknown Button {b.button}";

                ImGui.Checkbox(name, ref down);

                ImGui.EndDisabled();
                ImGui.PopID();
            }
        }

        ImGui.EndChild();
    }

    private static void DrawTrigger(
        ReadOnlySpan<char> text,
        Vector2 screenMin,
        Vector2 surfaceMin,
        Vector2 surfaceSize,
        (Vector2 pos01, Vector2 size01) rect01,
        float value)
    {
        var (screenRectMin, screenRectSize) = NormalizedToSurfaceRect(screenMin, surfaceSize, rect01);

        // draw fill as a rect sized proportionally to the value of the axis
        screenRectSize.Y *= value;
        var fillMax = screenRectMin + screenRectSize;
        ImGui.GetWindowDrawList().AddRectFilled(screenRectMin, fillMax, ImGui.GetColorU32(ImGuiCol.ButtonActive));

        var (surfaceRectMin, surfaceRectSize) = NormalizedToSurfaceRect(surfaceMin, surfaceSize, rect01);
        // draw text box for label and border
        DrawTextBox(text, surfaceRectMin, surfaceRectSize, false);
    }

    private static void DrawThumbstick(
        ReadOnlySpan<char> text,
        Vector2 screenMin,
        Vector2 surfaceMin,
        Vector2 surfaceSize,
        (Vector2 pos01, Vector2 size01) rect01,
        Vector2 value)
    {
        var (min, size) = NormalizedToSurfaceRect(surfaceMin, surfaceSize, rect01);
        size = new Vector2(Math.Max(size.X, size.Y)); // square
        DrawTextBox(text, min, size, false);


        var (screenRectMin, _) = NormalizedToSurfaceRect(screenMin, surfaceSize, rect01);
        var halfSize = size * 0.5f;
        var center = screenRectMin + halfSize;

        ImGui.GetWindowDrawList().AddCircleFilled(
            center: center + (value with { Y = -value.Y } * halfSize),
            radius: Math.Max(halfSize.X, halfSize.Y) * _stickPositionIndicatorSize,
            col: ImGui.GetColorU32(ImGuiCol.ButtonActive));
    }

    private static (Vector2 Min, Vector2 Size) NormalizedToSurfaceRect(Vector2 min, Vector2 size,
        (Vector2 pos01, Vector2 size01) rect01)
    {
        return (
            Min: min + rect01.pos01 * size,
            Size: rect01.size01 * size);
    }


    private const float _faceButtonSize = 0.105f;
    private const float _stickSize = 0.170f;
    private static readonly Vector2 ControllerOrigin = new(0, 0.03f);
    private const float _stickPositionIndicatorSize = 0.1f;
    private const float _stickButtonIndicatorSize = 0.2f;

    private static readonly Dictionary<JoystickButton, (Vector2 pos01, Vector2 size01)> ButtonDrawPositions01 = new()
    {
        // Face buttons / right cluster
        [JoystickButton.ButtonUp] = (new Vector2(0.765f, 0.185f) + ControllerOrigin, new Vector2(_faceButtonSize)),
        [JoystickButton.ButtonRight] = (new Vector2(0.850f, 0.295f) + ControllerOrigin, new Vector2(_faceButtonSize)),
        [JoystickButton.ButtonDown] = (new Vector2(0.765f, 0.405f) + ControllerOrigin, new Vector2(_faceButtonSize)),
        [JoystickButton.ButtonLeft] = (new Vector2(0.680f, 0.295f) + ControllerOrigin, new Vector2(_faceButtonSize)),

        // D-pad / left cluster
        [JoystickButton.DPadUp] = (new Vector2(0.150f, 0.185f) + ControllerOrigin, new Vector2(_faceButtonSize)),
        [JoystickButton.DPadRight] = (new Vector2(0.235f, 0.295f) + ControllerOrigin, new Vector2(_faceButtonSize)),
        [JoystickButton.DPadDown] = (new Vector2(0.150f, 0.405f) + ControllerOrigin, new Vector2(_faceButtonSize)),
        [JoystickButton.DPadLeft] = (new Vector2(0.065f, 0.295f) + ControllerOrigin, new Vector2(_faceButtonSize)),

        // Shoulder buttons
        [JoystickButton.LeftBumper] = (new Vector2(0.070f, 0.060f) + ControllerOrigin, new Vector2(0.245f, 0.090f)),
        [JoystickButton.RightBumper] = (new Vector2(0.685f, 0.060f) + ControllerOrigin, new Vector2(0.245f, 0.090f)),

        // Centre/system buttons
        [JoystickButton.Back] = (new Vector2(0.330f, 0.255f) + ControllerOrigin, new Vector2(0.125f, 0.070f)),
        [JoystickButton.Home] = (new Vector2(0.445f, 0.365f) + ControllerOrigin, new Vector2(0.110f)),
        [JoystickButton.Start] = (new Vector2(0.545f, 0.255f) + ControllerOrigin, new Vector2(0.125f, 0.070f)),

        // Stick buttons
        [JoystickButton.LeftStick] = (new Vector2(0.245f, 0.650f) + ControllerOrigin,
            new Vector2(_stickSize * _stickButtonIndicatorSize)),
        [JoystickButton.RightStick] = (new Vector2(0.585f, 0.650f) + ControllerOrigin,
            new Vector2(_stickSize * _stickButtonIndicatorSize))
    };

    // todo - initialize in static constructor to make it legible lol
    private static readonly DualReadOnlyList<(Vector2 pos01, Vector2 size)> ThumbstickPositions =
        new(getLeft: () => ButtonDrawPositions01[JoystickButton.LeftStick] with { size01 = new Vector2(_stickSize) },
            getRight: () => ButtonDrawPositions01[JoystickButton.RightStick] with { size01 = new Vector2(_stickSize) });

    private static readonly DualReadOnlyList<(Vector2 pos01, Vector2 size01)> TriggerPositions =
        new(getLeft: () => ButtonDrawPositions01[JoystickButton.LeftBumper] with
            {
                pos01 = new Vector2(ButtonDrawPositions01[JoystickButton.LeftBumper].pos01.X, 0) + ControllerOrigin
            },
            getRight: () => ButtonDrawPositions01[JoystickButton.RightBumper] with
            {
                pos01 = new Vector2(ButtonDrawPositions01[JoystickButton.RightBumper].pos01.X, 0) + ControllerOrigin
            });

    [Experimental("IGW0001")]
    public static void DrawEnumGrid<T>(in T[,] enumValues, GridState gridState,
        Func<T, int, int, bool>? getEnumState = null)
        where T : unmanaged, Enum
    {
        var lowX = enumValues.GetLowerBound(1);
        var lowY = enumValues.GetLowerBound(0);
        var highX = enumValues.GetUpperBound(1);
        var highY = enumValues.GetUpperBound(0);

        if (getEnumState != null)
        {
            for (var x = lowX; x <= highX; x++)
            {
                for (var y = lowY; y <= highY; y++)
                {
                    var enumValue = enumValues[y, x];
                    if (enumValue.EqualsUnsafe(default))
                        continue;

                    var down = getEnumState(enumValue, x, y);
                    var rect = gridState.Cell(x, y);
                    try
                    {
                        DrawTextBox(NameOf(enumValue), rect.Min, rect.Size, down);
                    }
                    catch (Exception e)
                    {
                        ImGuiLog.Error(e.ToString());
                    }
                }
            }
        }
        else
        {
            for (var x = lowX; x <= highX; x++)
            {
                for (var y = lowY; y <= highY; y++)
                {
                    var rect = gridState.Cell(x, y);
                    var enumValue = enumValues[y, x];
                    DrawTextBox(NameOf(enumValue), rect.Min, rect.Size, false);
                }
            }
        }

        return;

        static ReadOnlySpan<char> NameOf(T item)
        {
            var text = item.PrettyName;
            if (string.IsNullOrEmpty(text))
            {
                text = "##default-unknown";
            }

            return text;
        }
    }
}