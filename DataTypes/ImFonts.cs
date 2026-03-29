using System.Diagnostics.CodeAnalysis;
using ImGuiNET;

namespace ImGuiWindows.DataTypes
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public sealed class ImFonts(ImFontPtr[]? fonts)
    {
        // ReSharper disable once ReplaceWithPrimaryConstructorParameter
        private readonly ImFontPtr[]? _fonts = fonts;

        [MemberNotNullWhen(true, nameof(_fonts))]
        public unsafe bool HasFonts =>
            _fonts is { Length: > 3 } && _fonts.All(x => x.NativePtr != null && x.IsLoaded());

        public ImFontPtr Small => HasFonts ? _fonts[0] : ImGui.GetIO().Fonts.Fonts[0];
        public ImFontPtr Regular => HasFonts ? _fonts[1] : ImGui.GetIO().Fonts.Fonts[0];
        public ImFontPtr Bold => HasFonts ? _fonts[2] : ImGui.GetIO().Fonts.Fonts[0];
        public ImFontPtr Large => HasFonts ? _fonts[3] : ImGui.GetIO().Fonts.Fonts[0];

        public int Count => _fonts?.Length ?? 0;

        public ref ImFontPtr this[int index]
        {
            get
            {
                if (_fonts is null)
                {
                    return ref ImGui.GetIO().Fonts.Fonts[index];
                }

                return ref _fonts[index];
            }
        }
    }

    public record struct FontPack(TtfFont Regular, TtfFont Bold, TtfFont Small, TtfFont Large);

    public record struct TtfFont(string Path, float PixelSize);
}