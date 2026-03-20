using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;

namespace ImGuiWindows;

internal class ImguiInputContext
{
    private readonly List<char> _pressedChars = new();
    public ImguiInputContext(InputContext inputContext)
    {
        ImGuiLog.Debug("Initializing input context");
        inputContext.ConnectionChanged += OnInputConnectionChanged;

        // keyboard events
        inputContext.Keyboards.KeyChanged += OnKeyChanged;
        inputContext.Keyboards.KeyChar += OnKeyChar;

        // gamepad events
        inputContext.Gamepads.ThumbstickMove += OnGamepadThumbstickMove;
        inputContext.Gamepads.TriggerMove += OnGamepadTriggerMove;
        inputContext.Gamepads.ButtonChanged += OnGamepadButtonChanged;

        // joystick events
        inputContext.Joysticks.AxisMove += OnJoystickAxisMove;
        inputContext.Joysticks.ButtonChanged += OnJoystickButtonChanged;
        inputContext.Joysticks.HatMove += OnJoystickHatMove;

        // pointer events
        inputContext.Pointers.ButtonChanged += OnPointerButtonChanged;
        inputContext.Pointers.Click += OnPointerClick;
        inputContext.Pointers.DoubleClick += OnPointerDoubleClick;
        inputContext.Pointers.GripChanged += OnPointerGripChanged;
        inputContext.Pointers.PointChanged += OnPointerPointChanged;
        inputContext.Pointers.TargetChanged += OnPointerTargetChanged;
        inputContext.Pointers.MouseScroll += OnPointerScroll;
    }
    
    public void UpdateImGuiInput(ImGuiIOPtr io, InputContext inputContext)
    {
        Vector2? mousePos = null;
        Vector2? wheelPos = null;

        foreach (var mouse in inputContext.Pointers)
        {
            var state = mouse.State;
            var points = state.Points;
            if (points is { Count: > 0 })
            {
                // todo: compare against pointer target - we only want points related to the pointer target of our 
                // imgui surface
                mousePos = points[0].Position.AsVector2();
            }

            if (state is MouseState mouseState)
            {
                wheelPos = mouseState.WheelPosition;
            }

            var buttons = state.Buttons;
            io.MouseDown[0] |= buttons[PointerButton.Primary];
            io.MouseDown[1] |= buttons[PointerButton.Secondary];
            io.MouseDown[2] |= buttons[PointerButton.Button3];
        }

        io.MousePos = mousePos ?? Vector2.Zero;

        var wheel = wheelPos ?? Vector2.Zero;
        io.MouseWheel = wheel.Y;
        io.MouseWheelH = wheel.X;

        foreach (var c in _pressedChars)
        {
            io.AddInputCharacter(c);
        }

        _pressedChars.Clear();

        foreach (var kb in inputContext.Keyboards)
        {
            var keys = kb.State.Keys;
            io.KeyCtrl |= keys[KeyName.ControlLeft] || keys[KeyName.ControlRight];
            io.KeyAlt |= keys[KeyName.AltLeft] || keys[KeyName.AltRight];
            io.KeyShift |= keys[KeyName.ShiftLeft] || keys[KeyName.ShiftRight];
            io.KeySuper |= keys[KeyName.SuperLeft] || keys[KeyName.SuperRight];
        }
    }

    private void OnInputConnectionChanged(ConnectionEvent obj)
    {
        ImGuiLog.Debug($"{obj.Device.Name} {obj.Device.Id} {(obj.IsConnected ? "connected" : "disconnected")}");
    }

    private void OnJoystickHatMove(JoystickHatMoveEvent obj)
    {
        ImGuiLog.Debug($"{nameof(JoystickAxisMoveEvent)} from {obj.Joystick} moved to {obj.Value}");
        ;
    }

    private void OnJoystickButtonChanged(ButtonChangedEvent<JoystickButton> obj)
    {
        ImGuiLog.Debug(
            $"{nameof(ButtonChangedEvent<>)} {obj.Button.Name} from {obj.Device} changed to {obj.Button.IsDown}");
    }

    private void OnPointerScroll(MouseScrollEvent obj)
    {
        ImGuiLog.Debug(
            $"{nameof(MouseScrollEvent)} from {obj.Mouse} changed from {obj.WheelPosition - obj.Delta} to {obj.WheelPosition}");
    }

    private void OnPointerTargetChanged(PointerTargetChangedEvent obj)
    {
        ImGuiLog.Debug(
            $"{nameof(PointerTargetChangedEvent)} from {obj.Pointer} changed from to {obj.Target} with bounds {obj.OldBounds} to new bounds: {obj.NewBounds}");
    }

    private void OnPointerPointChanged(PointChangedEvent obj)
    {
        // ImGuiLog.Debug($"{nameof(PointChangedEvent)} from {obj.Pointer} changed from {obj.OldPoint} to {obj.NewPoint}");
    }

    private void OnPointerGripChanged(PointerGripChangedEvent obj)
    {
        ImGuiLog.Debug(
            $"{nameof(PointerGripChangedEvent)} from {obj.Pointer} changed from {obj.GripPressure - obj.Delta} to {obj.GripPressure}");
    }

    private void OnPointerDoubleClick(PointerClickEvent obj)
    {
        ImGuiLog.Debug($"Double-{nameof(PointerClickEvent)} from {obj.Pointer} with button {obj.Button} at {obj.Point}");
    }

    private void OnPointerClick(PointerClickEvent obj)
    {
        ImGuiLog.Debug($"{nameof(PointerClickEvent)} from {obj.Pointer} with button {obj.Button} at {obj.Point}");
    }

    private void OnPointerButtonChanged(ButtonChangedEvent<PointerButton> obj)
    {
        ImGuiLog.Debug($"{nameof(ButtonChangedEvent<>)} {obj.Button.Name} from {obj.Device} changed to {obj.Button.IsDown}");
    }

    private void OnJoystickAxisMove(JoystickAxisMoveEvent obj)
    {
        ImGuiLog.Debug(
            $"{nameof(JoystickAxisMoveEvent)} {obj.Axis} from {obj.Joystick} changed from {obj.Value - obj.Delta} to {obj.Value}");
    }

    private void OnGamepadButtonChanged(ButtonChangedEvent<JoystickButton> obj)
    {
        ImGuiLog.Debug(
            $"{nameof(ButtonChangedEvent<>)} {obj.Button.Name} from {obj.Device} changed to {obj.Button.IsDown}");
    }

    private void OnGamepadTriggerMove(GamepadTriggerMoveEvent obj)
    {
        ImGuiLog.Debug(
            $"{nameof(GamepadTriggerMoveEvent)} from {obj.Gamepad} changed from {obj.Value - obj.Delta} to {obj.Value}");
    }

    private void OnGamepadThumbstickMove(GamepadThumbstickMoveEvent obj)
    {
        ImGuiLog.Debug($"{nameof(GamepadThumbstickMoveEvent)} from {obj.Gamepad} changed from {obj.Value - obj.Delta} to {obj.Value}");
    }

    private void OnKeyChar(KeyCharEvent obj)
    {
        ImGuiLog.Debug($"{nameof(KeyCharEvent)} {obj.Character} from {obj.Keyboard} pressed");

        if (obj.Character.HasValue)
            _pressedChars.Add(obj.Character.Value);
    }

    private void OnKeyChanged(KeyChangedEvent evt)
    {
        var key = evt.Key;
        ImGuiLog.Debug($"{nameof(KeyCharEvent)} {key} from {evt.Keyboard} changed to {key.IsDown}");
        ImGui.GetIO().AddKeyEvent(evt.Key.Name.ToImGuiKey(), evt.Key.IsDown);
        //io.SetKeyEventNativeData(imGuiKey, (int) evt.Key.Namscancode);
    }
}