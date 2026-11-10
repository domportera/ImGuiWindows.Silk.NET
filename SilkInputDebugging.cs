using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;

namespace ImGuiWindows;

public static class SilkInputDebugging
{
    public static void DrawInputState(InputContext inputContext)
    {
        if (ImGui.Begin("Keyboards"))
        {
            for (var index = 0; index < inputContext.Keyboards.Count; index++)
            {
                var k = inputContext.Keyboards[index];
                var strId = GetDeviceName(k);
                if (ImGui.BeginChild(strId, size: default,
                        child_flags: ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY))
                {
                    ImGui.Text(strId);
                    DrawKeyboard(k);
                }

                ImGui.EndChild();
            }
        }

        ImGui.End();

        if (ImGui.Begin("Controllers"))
        {
            for (var index = 0; index < inputContext.Gamepads.Count; index++)
            {
                var g = inputContext.Gamepads[index];
                var strId = GetDeviceName(g);
                try
                {
                    DimGui.DrawController(strId, g.State.Buttons.AsReadOnlyList(), g.State.Thumbsticks,
                        g.State.Triggers);
                }
                catch (Exception ex)
                {
                    var log = $"Error drawing controller {strId}: {ex.Message}";
                    ImGuiLog.Error(log);
                    ImGui.Text(log);
                }
            }
        }

        ImGui.End();

        if (ImGui.Begin("Pointers"))
        {
            for (var index = 0; index < inputContext.Pointers.Count; index++)
            {
                var p = inputContext.Pointers[index];
                var strId = GetDeviceName(p);
                if (ImGui.BeginChild(strId, default, ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysAutoResize))
                {
                    try
                    {
                        DrawPointer(strId, p);
                    }
                    catch (Exception ex)
                    {
                        var log = $"Error drawing pointer {strId}: {ex}";
                        ImGuiLog.Error(log);
                        ImGui.Text(log);
                    }
                }
                ImGui.EndChild();
            }
        }

        ImGui.End();

        if (ImGui.Begin("Device list"))
        {
            var devices = inputContext.Devices;
            foreach (var device in devices)
            {
                DrawDeviceIdentityInfo(device);
            }
        }

        ImGui.End();
    }

    private static string GetDeviceName(IInputDevice g) => string.IsNullOrWhiteSpace(g.Name)
        ? $"Unknown {g.GetType().Name} {g.Id}"
        : g.Name;

    private static void DrawDeviceIdentityInfo(IInputDevice device)
    {
        var name = GetDeviceName(device);
        var id = device.Id;
        ImGui.Text($"Name: {name} | Id: {id}");
    }

    private static void DrawKeyboard(IKeyboard keyboard)
    {
        using var gridState = GridState.Begin("Silk keys", new Vector2(64, 64),
            columns: KeyLayoutStandard.GetUpperBound(0) + 1);

#pragma warning disable IGW0001
        DimGui.DrawEnumGrid(KeyLayoutStandard, gridState,
            getEnumState: (enumValue, _, _) => keyboard.State.Keys[enumValue].IsDown);
#pragma warning restore IGW0001
    }


    private static readonly KeyName[,] KeyLayoutStandard = new[,]
    {
        {
            KeyName.Escape, default, KeyName.F1, KeyName.F2, KeyName.F3, KeyName.F4, KeyName.F5, KeyName.F6, KeyName.F7,
            KeyName.F8, KeyName.F9, KeyName.F10, KeyName.F11, KeyName.F12, KeyName.PrintScreen, KeyName.ScrollLock,
            KeyName.Pause, KeyName.MediaPlayPause, KeyName.VolumeDown, KeyName.VolumeUp, KeyName.Mute
        },
        {
            KeyName.Grave, KeyName.Number1, KeyName.Number2, KeyName.Number3, KeyName.Number4, KeyName.Number5,
            KeyName.Number6, KeyName.Number7, KeyName.Number8, KeyName.Number9, KeyName.Number0, KeyName.Minus,
            KeyName.Equals, KeyName.Backspace, KeyName.Delete, KeyName.Home, KeyName.PageUp, KeyName.NumLockClear,
            KeyName.KeypadDivide, KeyName.KeypadMultiply, KeyName.KeypadMinus
        },
        {
            KeyName.Tab, KeyName.Q, KeyName.W, KeyName.E, KeyName.R, KeyName.T, KeyName.Y, KeyName.U, KeyName.I,
            KeyName.O, KeyName.P, KeyName.LeftBracket, KeyName.RightBracket, KeyName.Backslash, KeyName.Insert,
            KeyName.End, KeyName.PageDown, KeyName.Keypad7, KeyName.Keypad8, KeyName.Keypad9, KeyName.KeypadPlus
        },
        {
            KeyName.CapsLock, KeyName.A, KeyName.S, KeyName.D, KeyName.F, KeyName.G, KeyName.H, KeyName.J, KeyName.K,
            KeyName.L, KeyName.Semicolon, KeyName.Apostrophe, KeyName.Return, KeyName.Return, default, default, default,
            KeyName.Keypad4, KeyName.Keypad5, KeyName.Keypad6, KeyName.KeypadPlus
        },
        {
            KeyName.ShiftLeft, KeyName.Z, KeyName.X, KeyName.C, KeyName.V, KeyName.B, KeyName.N, KeyName.M,
            KeyName.Comma, KeyName.Period, KeyName.Slash, KeyName.ShiftRight, KeyName.ShiftRight, default, KeyName.Up,
            default, KeyName.KeypadMultiply, KeyName.Keypad1, KeyName.Keypad2, KeyName.Keypad3, KeyName.KeypadEnter
        },
        {
            KeyName.ControlLeft, KeyName.SuperLeft, KeyName.AltLeft, default, default, KeyName.Space, default,
            default, default, default, KeyName.AltRight, KeyName.SuperRight, KeyName.ControlRight, default,
            KeyName.Left, KeyName.Down, KeyName.Right, default, KeyName.Keypad0, KeyName.KeypadDecimal,
            KeyName.KeypadEnter
        }
    };

    public static void DrawPointer(string strId, IPointerDevice p)
    {
        ImGui.Text($"ID: {p.Id}");

        const ImGuiChildFlags imGuiChildFlags = ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeX |
                                                ImGuiChildFlags.AutoResizeY;
        if (ImGui.BeginChild("Targets", new Vector2(ImGui.GetContentRegionAvail().X, 0), imGuiChildFlags))
        {
            foreach (var target in p.Targets)
            {
                if (target is null)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text,
                        ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.2f, 0.2f, 1f)));
                    ImGui.Text("Null target??");
                    ImGui.PopStyleColor();
                    ImGui.NewLine();
                    continue;
                }

                var bounds = target.Bounds;
                ImGui.Text("Target " + target);
                ImGui.Text("Bounds: ");
                ImGui.SameLine();
                ImGui.Text(bounds.ToString());
                ImGui.Text("Point count:");
                ImGui.SameLine();
                var count = target.GetPointCount(p);
                ImGui.Text(count.ToString());
                ImGui.NewLine();
            }
        }

        ImGui.EndChild();

        var state = p.State;
        if (ImGui.BeginChild("Points", default, imGuiChildFlags))
        {
            foreach (var pt in state.Points)
            {
                DrawTargetPoint(pt);
            }
        }

        ImGui.EndChild();

        if (ImGui.BeginChild("Buttons", default, imGuiChildFlags))
        {
            var buttons = state.Buttons;
            using var gridState = GridState.Begin(strId, new Vector2(64, 64));

            try
            {
#pragma warning disable IGW0001
                DimGui.DrawEnumGrid<PointerButton>(PointerButtonNames, gridState,
                    (button, _, _) =>
                    {
                        {
                            try
                            {
                                return buttons[button].IsDown;
                            }
                            catch (Exception ex)
                            {
                                var log = $"Error drawing button {button} for {strId}: {ex}";
                                ImGuiLog.Error(log);
                                return false;
                            }
                        }
                    });
#pragma warning restore IGW0001
            }
            catch (Exception ex)
            {
                var log = $"Error drawing buttons for {strId}: {ex}";
                ImGuiLog.Error(log);
            }
        }

        ImGui.EndChild();
    }


    private static readonly PointerButton[,] PointerButtonNames;

    // todo: improve visualization, make it much more in-depth
    private static void DrawTargetPoint(in TargetPoint pt)
    {
        ImGui.BeginDisabled();
        var id = pt.Id;
        ImGui.InputInt("Point ID: ", ref id);
        var target = pt.Target;
        if (target is null)
        {
            ImGui.Text("Target is null");
            ImGui.EndDisabled();
            return;
        }

        var bounds = target.Bounds;
        var boundSize3 = bounds.Size;

        const float maxWidth = 300f;
        var width = Math.Min(ImGui.GetContentRegionAvail().X, maxWidth);
        var height = width * (boundSize3.Y / boundSize3.X);

        // draw box
        var boxSize = new Vector2(width, height);
        var cursorStart = ImGui.GetCursorPos();
        var cursorScreenStart = ImGui.GetCursorScreenPos();
        DimGui.DrawTextBox(pt.Target?.ToString() ?? "Null target", cursorStart, boxSize, isFilled: true);

        var normalizedPosition = pt.NormalizedPosition;
        var ptGuiPos = new Vector2(normalizedPosition.X, normalizedPosition.Y) * boxSize + cursorScreenStart;
        ImGui.GetWindowDrawList()
            .AddCircleFilled(ptGuiPos, 2f + 4f * pt.Pressure, ImGui.GetColorU32(ImGuiCol.NavCursor));

        // var maxPosition = cursorStart + boxSize;
        // ImGui.Dummy(boxSize);
        // ImGui.SetCursorPos(cursorStart with { Y = maxPosition.Y + ImGui.GetTextLineHeightWithSpacing() });

        ImGui.EndDisabled();
    }

    static SilkInputDebugging()
    {
        var allValues = Enum.GetValues<PointerButton>().Where(x => x != PointerButton.Unknown).ToArray();
        PointerButtonNames = new PointerButton[2, (int)Math.Ceiling(allValues.Length / 2f)];
        var whichValue = 0;
        var yLen = PointerButtonNames.GetLength(0);
        var xLen = PointerButtonNames.GetLength(1);
        for (var x = 0; x < xLen; x++)
        {
            for (var y = 0; y < yLen; y++)
            {
                if (whichValue < allValues.Length)
                {
                    PointerButtonNames[y, x] = allValues[whichValue++];
                }
            }
        }
    }
}