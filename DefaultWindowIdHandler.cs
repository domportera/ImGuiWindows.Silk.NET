using System.Runtime.CompilerServices;

namespace ImGuiWindows;

/// <summary>
/// A utility for generating unique window IDs on a per-object basis - currently unused
/// </summary>
internal static class DefaultWindowIdHandler
{
    private static uint _windowCounter = int.MaxValue;
    private static readonly Dictionary<uint, string> IdTitles = new();
    private static readonly Action<object> OnWindowDisposedDynamicIdAction;
    private static readonly Lock StringIdLock = new();

    private static readonly Dictionary<IDisposed, uint> WindowIds = new();
    private static readonly Lock IntegerIdLock = new();
    
    /// <summary>
    /// Window ID for imgui only -
    /// every window gets its own ID. Leaving this "null" by default
    /// will assign one automatically
    /// </summary>
    /// <param name="obj"> The object to whom this ID is assigned</param>
    /// <param name="title"> The title of the window</param>
    /// <param name="windowId">The ID of the window. If null, a new ID will be generated</param>
    public static ReadOnlySpan<char> GetIdStringFromCache(IDisposed obj, string title, int? windowId)
    {
        var id = GetIdFor(obj, windowId);

        string? idStr;
        var needsSubscribe = false;
        lock (StringIdLock)
        {
            if (!IdTitles.TryGetValue(id, out idStr))
            {
                idStr = IdTitles[id] = $"{title}##{id}";
                needsSubscribe = true;
            }
        }

        if (needsSubscribe)
        {
            obj.Disposed += OnWindowDisposedDynamicIdAction;
        }

        return idStr;

        static uint GetIdFor(IDisposed obj, int? windowId)
        {
            uint id;

            switch (windowId)
            {
                case null:
                {
                    break;
                }
                case < 1:
                {
                    ImGuiLog.Error($"Window ID must be between 1 and {int.MaxValue}");
                    break;
                }
                default:
                {
                    var val = windowId.Value;
                    id = Unsafe.As<int, uint>(ref val);

                    lock (IntegerIdLock)
                    {
#if DEBUG
                        // check for ID consistency
                        if (WindowIds.TryGetValue(obj, out var storedId))
                        {
                            if (storedId == id) return id;

                            ImGuiLog.Error("Window changed id at runtime - this is not supported. " +
                                           "This is handled in debug builds, but release builds will leak memory.");

                            lock (StringIdLock)
                            {
                                _ = IdTitles.Remove(storedId, out _);
                            }

                            return WindowIds[obj] = id;
                        }
#endif
                        return WindowIds[obj] = id;
                    }
                }
            }

            lock (IntegerIdLock)
            {
                if (!WindowIds.TryGetValue(obj, out id))
                {
                    WindowIds[obj] = id = Interlocked.Increment(ref _windowCounter);
                }
            }

            return id;
        }
    }

    static DefaultWindowIdHandler()
    {
        OnWindowDisposedDynamicIdAction = OnWindowDisposed;
    }

    private static void OnWindowDisposed(object sender)
    {
        if (sender is not IDisposed obj)
        {
            throw new ArgumentException($"Window disposal must provide {nameof(IDisposed)}");
        }

        obj.Disposed -= OnWindowDisposedDynamicIdAction;

        uint id;

        bool removed;
        lock (IntegerIdLock)
        {
            removed = WindowIds.Remove(obj, out id);
        }

        if (!removed)
        {
            ImGuiLog.Error($"Failed to remove window's string-based id {obj}");
            return;
        }

        lock (StringIdLock)
        {
            removed = IdTitles.Remove(id);
        }

        if (!removed)
        {
            ImGuiLog.Error($"Failed to remove window's integer-based id {obj}");
        }
    }
}