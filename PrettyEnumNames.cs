using System.Collections.Frozen;

namespace ImGuiWindows;

internal static class PrettyEnumNames<T> where T : unmanaged, Enum
{
    private static readonly FrozenDictionary<T, string> _names;

    static PrettyEnumNames()
    {
        var names = new Dictionary<T, string>();
        var enumValues = Enum.GetValues<T>();
        foreach (var value in enumValues)
        {
            var name = value.ToString();

            // convert TitleCase to Tile Case
            if (name.Length > 1)
            {
                var previousUpper = true;
                for (var index = 0; index < name.Length; index++)
                {
                    var c = name[index];
                    var isUpper = char.IsUpper(c);
                    if (isUpper && !previousUpper)
                    {
                        // insert a space
                        name = name.Insert(index++, " ");
                    }

                    previousUpper = isUpper;
                }
            }

            if (!names.TryAdd(value, name))
            {
                names.Remove(value, out var original);
                names[value] = $"{original} | {name}";
            }
        }

        _names = names.ToFrozenDictionary();
    }

    internal static string Name(T value) => _names[value];
}