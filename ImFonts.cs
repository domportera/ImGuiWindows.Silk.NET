using ImGuiNET;

namespace ImGuiWindows
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public sealed class ImFonts(ImFontPtr[] fonts)
    {
        public readonly bool HasFonts = fonts.Length > 3;
        public ImFontPtr Small => HasFonts ? fonts[0] : ImGui.GetIO().Fonts.Fonts[0];
        public ImFontPtr Regular => HasFonts ? fonts[1] : ImGui.GetIO().Fonts.Fonts[0];
        public ImFontPtr Bold => HasFonts ? fonts[2] : ImGui.GetIO().Fonts.Fonts[0];
        public ImFontPtr Large => HasFonts ? fonts[3] : ImGui.GetIO().Fonts.Fonts[0];

        public int Count => fonts.Length;

        public ImFontPtr this[int index] => fonts[index];
    }
    
    public record struct FontPack(TtfFont Regular, TtfFont Bold, TtfFont Small, TtfFont Large);
    public record struct TtfFont(string Path, float PixelSize);
}