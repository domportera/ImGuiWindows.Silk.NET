// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    }

    [StackTraceHidden, DebuggerHidden]
    private static string GenerateLog(string? message, string? path, int line, string? member, bool stackTrace, out LogProperties props)
    {
        const string traceFmt = "{0} (at {1}:{2})";
        props = GetProperties(path, line, member);
        return stackTrace 
            ? $"{message} ({string.Format(traceFmt, member, path, line)}){Environment.NewLine}{new StackTrace()}" 
            : $"{message} ({string.Format(traceFmt, member, path, line)})";
    }

    [StackTraceHidden, DebuggerHidden]
    private static LogProperties GetProperties(string? path, int line, string? member)
    {
        if (path is not null)
        {
            if (member is not null)
            {
                return new LogProperties
                    {(MemberProp, member), (PathProp, path), (LineProp, line.ToString()) };
            }
            
            return new LogProperties { (PathProp, path), (LineProp, line.ToString()) };
        }

        if (member is not null)
        {
            return new LogProperties {(MemberProp, member) };       
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
    }


    [StackTraceHidden, DebuggerHidden]
    public static void Warn(string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? member = null)
    {
        var log = GenerateLog(message, path, line, member, false, out var props);
        Logger.Warn(log, props);
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
        Logger.Warn($"Object not disposed properly. {message}", GetProperties(path, line, member));
    }
}