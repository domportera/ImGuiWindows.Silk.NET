using System.Runtime.CompilerServices;

namespace ImGuiWindows;

public static class EnumExtensions
{
    extension<T>(T value) where T : unmanaged, Enum
    {
        public string PrettyName
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => PrettyEnumNames<T>.Name(value);
        }

        public bool HasAny(params ReadOnlySpan<T> flags)
        {
            var has = false;
            for (var i = 0; i < flags.Length; i++)
            {
                has |= value.HasFlag(flags[i]);
            }

            return has;
        }

        public bool HasAll(params ReadOnlySpan<T> flags)
        {
            var has = true;
            for (var i = 0; i < flags.Length; i++)
            {
                has &= value.HasFlag(flags[i]);
            }

            return has;
        }
    }
}