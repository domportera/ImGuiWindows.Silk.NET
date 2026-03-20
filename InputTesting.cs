// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Silk.NET.Core;
using Silk.NET.Input;
using Surface = Silk.NET.Windowing.Surface;
using TestLog = ImGuiWindows.ImGuiLog;

namespace ImGuiWindows;

internal class InputTesting
{
    private static void ExecuteInput(InputContext? context, INativeWindow window)
    {
        if (context is null)
        {
            return;
        }

        // TestLog.Debug($"Update called on {window}");
        context.Update();
    }


  
}
