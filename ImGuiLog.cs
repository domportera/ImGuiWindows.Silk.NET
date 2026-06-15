// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Common;
using ImGuiNET;
using JetBrains.Annotations;
using XenoAtom.Logging;
using XenoAtom.Logging.Writers;

namespace ImGuiWindows;

public static class ImGuiLog
{
    private static LogBeginScope _logScope;
    private static readonly Logger Logger;
    private const string MemberProp = "member";
    private const string PathProp = "path";
    private const string LineProp = "line";
    [ThreadStatic] private static StringBuilder? _sb;

    static ImGuiLog()
    {
        var config = new LogManagerConfig
        {
            RootLogger =
            {
                MinimumLevel = LogLevel.All,
                Writers =
                {
                    new StreamLogWriter(Console.OpenStandardOutput())
                }
            }
        };

        LogManager.Initialize(config);
        var logger = LogManager.GetLogger("App");
        _logScope = logger.BeginScope(new LogProperties { MemberProp, PathProp, LineProp });
        logger.Info("Inside request scope");
        Logger = logger;
    }


    public static void EndScope() => _logScope.Dispose();

    public static void Shutdown() => LogManager.Shutdown();

    [StackTraceHidden, DebuggerHidden]
    public static void Error(string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? member = null)
    {
        var log = GenerateLog(message, path, line, member, true, out var props);
        Logger.Error(log, props);
        LogToImgui(log);
    }

    [StackTraceHidden, DebuggerHidden]
    private static string GenerateLog(string? message, string? path, int line, string? member, bool stackTrace,
        out LogProperties props)
    {
        const string traceFmt = "{0} (at {1}:{2})";
        props = GetProperties(path, line, member);
        _sb ??= new();
        var value = stackTrace
            ? _sb.Append(message)
                .Append(" (")
                .AppendFormat(traceFmt, member, path, line)
                .AppendLine(")")
                .Append(new StackTrace())
                .AppendLine()
                .ToString()
            : _sb.Append(message)
                .Append(" (")
                .AppendFormat(traceFmt, member, path, line)
                .AppendLine(")")
                .ToString();
        _sb.Clear();
        return value;
    }

    [StackTraceHidden, DebuggerHidden]
    private static LogProperties GetProperties(string? path, int line, string? member)
    {
        if (path is not null)
        {
            if (member is not null)
            {
                return new LogProperties
                    { (MemberProp, member), (PathProp, path), (LineProp, line.ToString()) };
            }

            return new LogProperties { (PathProp, path), (LineProp, line.ToString()) };
        }

        if (member is not null)
        {
            return new LogProperties { (MemberProp, member) };
        }

        return new LogProperties();
    }

    [Conditional("DEBUG")]
    [StackTraceHidden, DebuggerHidden]
    public static void Debug(string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? member = null)
    {
        var log = GenerateLog(message, path, line, member, false, out var props);
        Logger.Debug(log, props);
        LogToImgui(log);
    }

    [StackTraceHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogToImgui(string log)
    {
        lock (BufferedLogs)
        {
            BufferedLogs.Enqueue(log);
        }
    }

    [StackTraceHidden, DebuggerHidden]
    public static void Warn(string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? member = null)
    {
        var log = GenerateLog(message, path, line, member, false, out var props);
        Logger.Warn(log, props);
        LogToImgui(log);
    }

    /// <summary>
    /// Prints a warning that the caller did not dispose of the object properly
    /// </summary>
    [StackTraceHidden, DebuggerHidden]
    public static void DisposalWarning(string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? member = null)
    {
        _sb ??= new StringBuilder();
        var msg = _sb.Append("Object not disposed properly. ").Append(message).ToString();
        Logger.Warn(msg, GetProperties(path, line, member));
        LogToImgui(msg);
    }

    internal static void PushLogs()
    {
        if (ImGui.GetCurrentContext() == nint.Zero)
        {
            throw new InvalidOperationException("ImGui context is null");
        }


        ReadOnlySpan<char> newLine = ['\n'];

        lock (BufferedLogs)
        {
            while (BufferedLogs.TryDequeue(out var old))
            {
                ImGui.DebugLog(old);
                ImGui.DebugLog(newLine);
            }
        }
    }

    private static readonly Queue<string> BufferedLogs = new();
}