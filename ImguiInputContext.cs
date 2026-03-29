using ImGuiNET;
using Silk.NET.Input;

namespace ImGuiWindows;

// todo - manage multiple imgui contexts - these events probably also need to be re-queued here and applied in BeginImGuiInput
internal class ImguiInputContext
{
    private readonly List<char> _pressedChars = [];
    private readonly IPointerTarget? _pointerTarget;

    public ImguiInputContext(InputContext inputContext, IPointerTarget? target)
    {
        _pointerTarget = target;

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

    public void BeginImGuiInput(ImGuiIOPtr io, InputContext inputContext)
    {
        // io.ClearEventsQueue()
        // io.ClearInputKeys();
        // io.ClearInputMouse();

        _pressedChars.Clear();
        io.KeyCtrl = false;
        io.KeyAlt = false;
        io.KeyShift = false;
        io.KeySuper = false;

        foreach (var kb in inputContext.Keyboards)
        {
            var modifiers = kb.State.Modifiers;
            // io.KeyCtrl |= (modifiers & (KeyModifiers.ControlLeft | KeyModifiers.ControlRight)) != 0;
            // io.KeyAlt |= (modifiers & (KeyModifiers.AltLeft | KeyModifiers.AltRight)) != 0;
            // io.KeyShift |= (modifiers & (KeyModifiers.ShiftLeft | KeyModifiers.ShiftRight)) != 0;
            // io.KeySuper |= (modifiers & (KeyModifiers.SuperLeft | KeyModifiers.SuperRight)) != 0;
        }
    }

    public void EndImGuiInput()
    {
        return;
        var io = ImGui.GetIO();

        for (var i = 0; i < io.MouseClicked.Count; ++i)
        {
            io.MouseClicked[i] = false;
        }

        for (var i = 0; i < io.MouseDoubleClicked.Count; ++i)
        {
            io.MouseDoubleClicked[i] = false;
        }
    }

    private void OnInputConnectionChanged(ConnectionEvent obj)
    {
        ImGuiLog.Debug($"{obj.Device.Name} {obj.Device.Id} {(obj.IsConnected ? "connected" : "disconnected")}");
    }

    private void OnJoystickHatMove(JoystickHatMoveEvent obj)
    {
        ImGuiLog.Debug($"{nameof(JoystickAxisMoveEvent)} from {obj.Joystick} moved to {obj.Value}");
    }

    private void OnJoystickButtonChanged(ButtonChangedEvent<JoystickButton> obj)
    {
        ImGuiLog.Debug(
            $"{nameof(ButtonChangedEvent<>)} {obj.Button.Name} from {obj.Device} changed to {obj.Button.IsDown}");
    }

    private void OnPointerScroll(MouseScrollEvent obj)
    {
        var delta = obj.Delta * 0.5f;
        ImGui.GetIO().AddMouseWheelEvent(-delta.X, delta.Y);
    }

    private void OnPointerTargetChanged(PointerTargetChangedEvent obj)
    {
        ImGuiLog.Debug(
            $"{nameof(PointerTargetChangedEvent)} from {obj.Pointer} changed from to {obj.Target} with bounds {obj.OldBounds} to new bounds: {obj.NewBounds}");
    }

    private void OnPointerPointChanged(PointChangedEvent obj)
    {
        // ImGuiLog.Debug($"{nameof(PointChangedEvent)} from {obj.Pointer} changed from {obj.OldPoint} to {obj.NewPoint}");
        if (obj.NewPoint is null)
            return;
        var pt = obj.NewPoint.Value;

        ImGui.GetIO().AddMousePosEvent(pt.Position.X, pt.Position.Y);
    }

    private void OnPointerGripChanged(PointerGripChangedEvent obj)
    {
        ImGuiLog.Debug(
            $"{nameof(PointerGripChangedEvent)} from {obj.Pointer} changed from {obj.GripPressure - obj.Delta} to {obj.GripPressure}");
    }

    private void OnPointerDoubleClick(PointerClickEvent obj)
    {
        ImGui.GetIO().MouseDoubleClicked[obj.Button.Index()] = true;
        ImGuiLog.Debug(
            $"Double-{nameof(PointerClickEvent)} from {obj.Pointer} with button {obj.Button} at {obj.Point}");
    }

    private void OnPointerClick(PointerClickEvent obj)
    {
        ImGui.GetIO().MouseClicked[obj.Button.Index()] = true;
        ImGuiLog.Debug($"{nameof(PointerClickEvent)} from {obj.Pointer} with button {obj.Button} at {obj.Point}");
    }

    private void OnPointerButtonChanged(ButtonChangedEvent<PointerButton> obj)
    {
        // todo - multi-device support?
        ImGui.GetIO().AddMouseButtonEvent(obj.Button.Name.Index(), obj.Button.IsDown);
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
        ImGuiLog.Debug(
            $"{nameof(GamepadThumbstickMoveEvent)} from {obj.Gamepad} changed from {obj.Value - obj.Delta} to {obj.Value}");
    }

    private void OnKeyChar(KeyCharEvent obj)
    {
        ImGuiLog.Debug($"{nameof(KeyCharEvent)} {obj.Character} from {obj.Keyboard} pressed");
        if (obj.Character is not null)
        {
            ImGui.GetIO().AddInputCharacter(obj.Character.Value);
            _pressedChars.Add(obj.Character.Value);
        }
    }

    private void OnKeyChanged(KeyChangedEvent evt)
    {
        var key = evt.Key;
        ImGuiLog.Debug($"{nameof(KeyCharEvent)} {key} from {evt.Keyboard} changed to {key.IsDown}");
        ImGui.GetIO().AddKeyEvent(evt.Key.Name.ToImGuiKey(), evt.Key.IsDown);
        //io.SetKeyEventNativeData(imGuiKey, (int) evt.Key.Namscancode);
    }
}