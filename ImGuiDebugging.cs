using System.Drawing;
using System.Numerics;
using System.Reflection;
using ImGuiNET;
using ImGuiWindows.DataTypes;

namespace ImGuiWindows;

public static class ImGuiDebugging
{
    public static void DrawDebugInput(ImGuiIOPtr io, ref float renderSize)
    {
        DrawMouse(io);
        DrawKeyboard(io.KeysData.AsSpan(), renderSize);
        DrawSettings(io, renderSize: ref renderSize);
        ImGui.ShowDebugLogWindow();
        return;

        static void DrawMouse(ImGuiIOPtr io)
        {
            if (ImGui.Begin("Mouse"))
            {
                var mousePos = io.MousePos;
                var mouseWheel = io.MouseWheel;
                var mouseWheelH = io.MouseWheelH;
                var mouseDownRange = io.MouseDown.AsSpan();
                var mouseClickedRange = io.MouseClickedPos;
                var mouseDoubleClickedRange = io.MouseDoubleClicked;
                var mouseReleaseRange = io.MouseReleased;

                ImGui.Text($"Mouse position: {mousePos.X}, {mousePos.Y}");
                ImGui.Text($"Mouse wheel: {mouseWheel}, {mouseWheelH}");

                const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable |
                                              ImGuiTableFlags.SizingFixedFit;
                if (ImGui.BeginTable("Mouse", 4, flags))
                {
                    for (var i = 0; i < mouseDownRange.Length; i++)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text($"Mouse {i} {(mouseDownRange[i] ? "down" : "up")}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"Mouse {i} clicked: {mouseClickedRange[i]}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"Mouse {i} released: {mouseReleaseRange[i]}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"Mouse {i} double clicked: {mouseDoubleClickedRange[i]}");
                    }

                    ImGui.EndTable();
                }
            }

            ImGui.End();
        }

        static void DrawKeyboard(Span<ImGuiKeyData> allKeyData, float renderSize)
        {
            if (ImGui.Begin("Keyboard"))
            {
                var origin = ImGui.GetCursorPos();
                var originalOrigin = origin;
                const float rectSize = 64f;
                var size = new Vector2(rectSize, rectSize) * renderSize;

                for (var key = ImGuiKey.Tab; key <= ImGuiKey.Menu; key++)
                {
                    var index = key - ImGuiKey.NamedKey_BEGIN;
                    ref readonly var keyData = ref allKeyData[index];
                    DrawKey(key, origin, size, keyData.Down > 0);
                    AdvanceGrid(ref origin, size, originalOrigin);
                }

                NextRow(ref origin, size, originalOrigin, 2);

                for (var key = ImGuiKey._0; key <= ImGuiKey._9; key++)
                {
                    var index = key - ImGuiKey.NamedKey_BEGIN;
                    ref readonly var keyData = ref allKeyData[index];
                    DrawKey(key, origin, size, keyData.Down > 0);
                    AdvanceGrid(ref origin, size, originalOrigin);
                }

                NextRow(ref origin, size, originalOrigin, 2);

                for (var key = ImGuiKey.A; key <= ImGuiKey.Z; key++)
                {
                    var index = key - ImGuiKey.NamedKey_BEGIN;
                    ref readonly var keyData = ref allKeyData[index];
                    DrawKey(key, origin, size, keyData.Down > 0);
                    AdvanceGrid(ref origin, size, originalOrigin);
                }

                NextRow(ref origin, size, originalOrigin, 2);

                for (var key = ImGuiKey.F1; key <= ImGuiKey.F24; key++)
                {
                    var index = key - ImGuiKey.NamedKey_BEGIN;
                    ref readonly var keyData = ref allKeyData[index];
                    DrawKey(key, origin, size, keyData.Down > 0);
                    AdvanceGrid(ref origin, size, originalOrigin);
                }

                NextRow(ref origin, size, originalOrigin, 2);

                for (var key = ImGuiKey.Apostrophe; key <= ImGuiKey.AppForward; key++)
                {
                    var index = key - ImGuiKey.NamedKey_BEGIN;
                    ref readonly var keyData = ref allKeyData[index];
                    DrawKey(key, origin, size, keyData.Down > 0);
                    AdvanceGrid(ref origin, size, originalOrigin);
                }

                NextRow(ref origin, size, originalOrigin, 2);

                for (var key = ImGuiKey.GamepadStart; key < ImGuiKey.NamedKey_END; key++)
                {
                    var index = key - ImGuiKey.NamedKey_BEGIN;
                    ref readonly var keyData = ref allKeyData[index];
                    DrawKey(key, origin, size, keyData.Down > 0);
                    AdvanceGrid(ref origin, size, originalOrigin);
                }
            }

            ImGui.End();
            return;

            static void NextRow(ref Vector2 origin, Vector2 size, Vector2 originalOrigin, int count = 1)
            {
                origin.X = originalOrigin.X;
                origin.Y += size.Y * count;
            }

            static void AdvanceGrid(ref Vector2 origin, Vector2 size, Vector2 originalOrigin)
            {
                var available = new Vector2(ImGui.GetWindowWidth(), ImGui.GetWindowHeight()) - originalOrigin;
                origin.X += size.X;
                if (origin.X > available.X - size.X)
                {
                    origin.X = originalOrigin.X;
                    origin.Y += size.Y;
                }
            }

            static void DrawKey(ImGuiKey key, Vector2 min, Vector2 size, bool isDown)
            {
                var keyName = key.PrettyName;

                ImGui.SetCursorPos(min);
                using var style = StyleScope.Begin();
                if (isDown)
                {
                    style.Push(ImGuiCol.ChildBg, Color.Pink);
                }

                if (ImGui.BeginChild(keyName, size: size,
                        child_flags: ImGuiChildFlags.Borders,
                        window_flags: ImGuiWindowFlags.NoDecoration |
                                      ImGuiWindowFlags.NoFocusOnAppearing))
                {
                    ImGui.TextWrapped(keyName);
                }

                ImGui.EndChild();
            }
        }
    }

    private static void DrawSettings(ImGuiIOPtr io, ref float renderSize)
    {
        object ioBox = io;
        if (ImGui.Begin("Settings"))
        {
            ImGui.DragFloat(label: "Render size", v: ref renderSize, v_speed: 0.1f, v_min: 0.1f, v_max: 1f);

            if (ImGui.BeginTable("Imgui config parameters", 4,
                    ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable))
            {
                ImGui.TableAngledHeadersRow();
                ImGui.Text("Return type");

                ImGui.TableNextColumn();
                ImGui.Text("Return parameter info");

                ImGui.TableNextColumn();
                ImGui.Text("Name");

                ImGui.TableNextColumn();
                ImGui.Text("Value");


                ImGui.TableNextRow();
                foreach (var c in ConfigRefAccessors)
                {
                    var returnType = c.ReturnType;
                    using (var style = StyleScope.Begin())
                    {
                        if (returnType.IsPointer)
                        {
                            style.Push(ImGuiCol.Text, Color.Blue);
                        }
                        else if (returnType.IsConstructedGenericType)
                        {
                            style.Push(ImGuiCol.Text, Color.Coral);
                        }
                        else if (returnType.IsValueType)
                        {
                            style.Push(ImGuiCol.Text, Color.Violet);
                        }
                        else if (returnType.IsByRef)
                        {
                            style.Push(ImGuiCol.Text, Color.DarkViolet);
                        }
                        else if (returnType.IsByRefLike)
                        {
                            style.Push(ImGuiCol.Text, Color.MediumVioletRed);
                        }
                        else if (returnType.IsFunctionPointer || returnType.IsUnmanagedFunctionPointer)
                        {
                            style.Push(ImGuiCol.Text, Color.Green);
                        }

                        var returnParameter = c.ReturnParameter;

                        ImGui.Text(returnType.ToString());

                        ImGui.TableNextColumn();
                        ImGui.Text(returnParameter.ToString());
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text(c.Name);


                    using (var style = StyleScope.Begin())
                    {
                        try
                        {
                            var o = c.Invoke(ioBox, null);
                            ImGui.Text(
                                $"{c.Name} {o?.GetType().ToString() ?? "(no type for null)"} | {(o == null ? "<null>" : o.ToString())}");
                        }
                        catch (Exception e)
                        {
                            style.Push(ImGuiCol.Text, new Vector4(1f, 0f, 0f, 1f));
                            ImGui.Text($"{c.Name}: EXCEPTION - {e.Message}");
                        }
                    }
                }

                ImGui.EndTable();
            }
        }

        ImGui.End();
    }

    static ImGuiDebugging()
    {
        var properties = typeof(ImGuiIOPtr).GetProperties();
        List<MethodInfo> allAccessors = [];
        foreach (var p in properties)
        {
            var accessors = p.GetAccessors(nonPublic: false);
            foreach (var a in accessors)
            {
                if (a.GetParameters().Length == 0 && a.ReturnType.IsByRef || a.ReturnType.IsByRefLike)
                {
                    allAccessors.Add(a);
                }
            }
        }

        ConfigRefAccessors = allAccessors;
    }

    private static readonly IReadOnlyList<MethodInfo> ConfigRefAccessors;
}

public static class ImGuiExtensions
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
}