using System.Numerics;
using ImGuiNET;

namespace ImguiWindows;

internal sealed class MessageBox<T> : IImguiDrawer<T>
{
    private readonly int _startingButtonId;
    private int _framesSinceButtonPress = 0;
    private bool _buttonPressed = false;

    public MessageBox(string message, T[]? buttons, Func<T, string>? toButtonLabel)
    {
        if (buttons == null || buttons.Length == 0)
        {
            buttons = [];
        }

        toButtonLabel ??= item => item!.ToString()!;
        _message = message;
        _buttons = buttons;
        _toButtonLabel = toButtonLabel;
        _startingButtonId = Random.Shared.Next(int.MinValue, int.MaxValue);
        _result = default;
    }

    public void Init()
    {
    }

    public void OnRender(string windowName, double deltaSeconds, ImFonts fonts, float dpiScale)
    {
        var contentRegion = ImGui.GetContentRegionAvail();
        var padding = contentRegion.X * 0.1f;
        var widthAvailable = contentRegion.X - padding;


        if (!string.IsNullOrWhiteSpace(_message))
        {
            ImGui.PushFont(fonts.Regular);
            ImGui.NewLine();
            ImGui.PushTextWrapPos(widthAvailable);
            ImGui.SetCursorPosX(padding);
            ImGui.TextWrapped(_message);
            ImGui.PopTextWrapPos();
            ImGui.PopFont();

            DrawSpacing(fonts);
            ImGui.PushFont(fonts.Small);

            ImGui.SetCursorPosX(padding);
            if (ImGui.Button("Copy to clipboard"))
            {
                ImGui.SetClipboardText(_message);
            }

            var style = ImGui.GetStyle();
            var originalHoverFlags = style.HoverFlagsForTooltipMouse;
            style.HoverFlagsForTooltipMouse = ImGuiHoveredFlags.DelayNone;
            if (ImGui.BeginItemTooltip())
            {
                ImGui.Text(
                    "Make sure to paste somewhere before closing the window,\nas some events copy text to the clipboard and can overwrite it.");
                ImGui.EndTooltip();
            }

            style.HoverFlagsForTooltipMouse = originalHoverFlags;

            ImGui.PopFont();

            DrawSpacing(fonts);
            ImGui.Separator();
            DrawSpacing(fonts);
        }

        ImGui.PushFont(fonts.Regular);

        var width = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(width, 0);
        var i = _startingButtonId;
        _framesSinceButtonPress = _buttonPressed ? _framesSinceButtonPress + 1 : 0;
        foreach (var button in _buttons)
        {
            var name = _toButtonLabel.Invoke(button);

            unchecked
            {
                ImGui.PushID(++i);
            }

            if (ImGui.Button(name, size))
            {
                _result = button;
                _buttonPressed = true;
            }

            ImGui.PopID();

            ImGui.Spacing();
        }

        ImGui.PopFont();

        DrawSpacing(fonts);

        return;

        static void DrawSpacing(ImFonts fonts)
        {
            if (fonts.HasFonts)
            {
                ImGui.PushFont(fonts.Small);
                ImGui.NewLine();
                ImGui.PopFont();
            }
            else
            {
                const int spacingAmount = 4;
                for (int i = 0; i < spacingAmount; i++)
                    ImGui.Spacing();
            }
        }
    }

    public void OnWindowUpdate(double deltaSeconds, out bool shouldClose)
    {
        // framecount is a hack in case of colliding ids - we want our buttons to reset their pressed state
        shouldClose = _result != null && _framesSinceButtonPress > 0;
    }

    public void OnClose()
    {
    }

    public void OnFileDrop(string[] filePaths)
    {
        // do nothing - drag and drop could be supported by another window!
    }

    public void OnWindowFocusChanged(bool changedTo)
    {
        // do nothing
    }

    public T? Result => _result;

    private readonly Func<T, string> _toButtonLabel;
    private T? _result;
    private readonly T[] _buttons;
    private readonly string _message;
}