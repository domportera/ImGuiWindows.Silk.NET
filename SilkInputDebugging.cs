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
                var strId = string.IsNullOrWhiteSpace(k.Name) ? $"Unknown Keyboard {index}" : k.Name;
                if (ImGui.BeginChild(strId, size: default, child_flags: ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY))
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
                var strId = string.IsNullOrWhiteSpace(g.Name) ? $"Unknown Gamepad {index}" : g.Name;
                try
                {
                    DimGui.DrawController(strId, g.State.Buttons.AsReadOnlyList(), g.State.Thumbsticks, g.State.Triggers);
                }
                catch(Exception ex)
                {
                    var log = $"Error drawing controller {strId}: {ex.Message}";
                    ImGuiLog.Error(log);
                    ImGui.Text(log);
                }
            }
        }

        ImGui.End();
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
}