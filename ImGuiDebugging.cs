using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Text;
using Common;
using ImGuiNET;
using ImGuiWindows.DataTypes;

namespace ImGuiWindows;

public static class ImGuiDebugging
{
    [ThreadStatic] private static StringBuilder? _sb;

    private static void TextAndClear(StringBuilder sb)
    {
        var str = sb.ToDisposableString(true);
        ImGui.Text(str);
        str.Dispose();
    }

    public static void DrawDebugInput(ImGuiIOPtr io, ref float renderSize, ref string textEdit)
    {
        DrawMouse(io);
        DrawKeyboard(io.KeysData.AsSpan(), renderSize);
        DrawTextEditor(ref textEdit);
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

                using var mousePosStr = mousePos.ToDisposableString();
                _sb ??= new StringBuilder();
                _sb.Append("Mouse position: ").Append(mousePosStr).AppendLine()
                    .Append("Mouse wheel: (").Append(mouseWheel).Append(", ").Append(mouseWheelH).Append(')');

                TextAndClear(_sb);

                const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable |
                                              ImGuiTableFlags.SizingFixedFit;
                if (ImGui.BeginTable("Mouse", 4, flags))
                {
                    for (var i = 0; i < mouseDownRange.Length; i++)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        _sb.Append("Mouse ").Append(i).Append(' ').Append(mouseDownRange[i] ? "down" : "up");
                        TextAndClear(_sb);

                        ImGui.TableNextColumn();
                        var rangeStr = mouseClickedRange[i].ToDisposableString();
                        _sb.Append("Mouse ").Append(i).Append(" clicked: ").Append(rangeStr);
                        rangeStr.Dispose();
                        TextAndClear(_sb);

                        ImGui.TableNextColumn();
                        _sb.Append("Mouse ").Append(i).Append(" released: ").Append(mouseReleaseRange[i]);
                        TextAndClear(_sb);

                        ImGui.TableNextColumn();
                        _sb.Append("Mouse ").Append(i).Append(" double clicked: ").Append(mouseDoubleClickedRange[i]);
                        TextAndClear(_sb);
                    }


                    ImGui.EndTable();
                }
            }

            ImGui.End();
        }
    }

    private static void DrawKeyboard(Span<ImGuiKeyData> allKeyData, float renderSize)
    {
        if (ImGui.Begin("Keyboard"))
        {
            const float rectSize = 64f;
            var cellSize = new Vector2(rectSize, rectSize) * renderSize;

            var grid = GridState.Begin("KeyboardGrid", cellSize, columns: 20);

            DrawKeys(ref grid, ImGuiKey.Tab, ImGuiKey.Menu, allKeyData);
            grid.NextRow();

            DrawKeys(ref grid, ImGuiKey.A, ImGuiKey.Z, allKeyData);
            grid.NextRow();

            DrawKeys(ref grid, ImGuiKey.A, ImGuiKey.Z, allKeyData);
            grid.NextRow();

            DrawKeys(ref grid, ImGuiKey.F1, ImGuiKey.F24, allKeyData);
            grid.NextRow();

            DrawKeys(ref grid, ImGuiKey.Apostrophe, ImGuiKey.AppForward, allKeyData);
            grid.NextRow();

            DrawKeys(ref grid, ImGuiKey.GamepadStart, ImGuiKey.ReservedForModSuper, allKeyData);
            grid.Dispose();
        }

        ImGui.End();
        return;

        static void DrawKeys(ref GridState gridState, ImGuiKey start, ImGuiKey end, Span<ImGuiKeyData> allKeyData)
        {
            for (var key = start; key <= end; key++)
            {
                var index = key - ImGuiKey.NamedKey_BEGIN;
                ref readonly var keyData = ref allKeyData[index];
                var cell = gridState.NextCell();
                DimGui.DrawTextBox(key.PrettyName, cell.Min, cell.Size, keyData.Down > 0);
            }
        }
    }

    private static void DrawTextEditor(ref string textEdit)
    {
        if (ImGui.Begin("Text Editor"))
        {
            var wantsText = ImGui.GetIO().WantTextInput;
            ImGui.Checkbox("wants text", ref wantsText);
            ImGui.InputTextMultiline(label: "Text entry test", input: ref textEdit, maxLength: int.MaxValue,
                Vector2.Zero, ImGuiInputTextFlags.AllowTabInput);
        }

        ImGui.End();
    }

    private static void DrawSettings(ImGuiIOPtr io, ref float renderSize)
    {
        _sb ??= new StringBuilder();
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
                object ioBox = io;
                for (var index = 0; index < ConfigRefAccessors.Count; index++)
                {
                    var c = ConfigRefAccessors[index];
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


                        var nameText = returnType.FullName ?? returnType.ToString();
                        ImGui.Text(nameText);

                        ImGui.TableNextColumn();
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                        if (c.ReturnParameter is not null)
                        {
                            var returnParamType = c.ReturnParameter.ParameterType;
                            var paramNameText = returnParamType.FullName ??
                                                // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
                                                returnParamType.Name ?? returnParamType.ToString();
                            ImGui.Text(paramNameText);
                        }
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text(c.Name);


                    using (var style = StyleScope.Begin())
                    {
                        try
                        {
                            var o = c.Invoke(ioBox, null);
                            _sb.Append(c.Name).Append(' ').Append(o?.GetType() as object ?? "(no type for null)")
                                .Append(" | ").Append(o ?? "<null>");
                            TextAndClear(_sb);
                        }
                        catch (Exception e)
                        {
                            style.Push(ImGuiCol.Text, new Vector4(1f, 0f, 0f, 1f));
                            _sb.Append(c.Name).Append(": EXCEPTION - ").Append(e.Message);
                            TextAndClear(_sb);
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
                if (a.GetParameters().Length == 0 && (a.ReturnType.IsByRef || a.ReturnType.IsByRefLike))
                {
                    allAccessors.Add(a);
                }
            }
        }

        ConfigRefAccessors = allAccessors;
    }

    private static readonly IReadOnlyList<MethodInfo> ConfigRefAccessors;
}