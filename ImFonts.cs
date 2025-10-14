using System.Numerics;
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

    public interface IImguiWindowProvider
    {
        public object ContextLock { get; }
        public void SetFonts(FontPack fontPack);

        public TData? Show<TData>(string title, IImguiDrawer<TData> drawer, in SimpleWindowOptions? options = null)
        {
            Show(title, (IImguiDrawer)drawer, options);
            return drawer.Result;
        }

        public void Show(string title, IImguiDrawer drawer, in SimpleWindowOptions? options = null);

        public async Task ShowAsync(string title, IImguiDrawer drawer, SimpleWindowOptions? options = null)
        {
            var windowTask = StartAsyncWindow(title, drawer, options);
            await windowTask;
        }

        // we can't simply return the result here, because nullable type constraints dont work between reference and value types
        public async Task ShowAsync<TData>(string title, AsyncImguiDrawer<TData> drawer, Action<TData> assign,
            SimpleWindowOptions? options = null)
        {
            var windowTask = StartAsyncWindow(title, drawer, options);

            await foreach (var result in drawer.GetResults())
            {
                if (result != null)
                    assign(result);
            }

            await windowTask;
        }

        private async Task StartAsyncWindow(string title, IImguiDrawer drawer, SimpleWindowOptions? options)
        {
            var context = SynchronizationContext.Current;

            await Task.Run(() => { Show(title, drawer, options); }).ConfigureAwait(false);

            SynchronizationContext.SetSynchronizationContext(context);
            Console.WriteLine("Completed window run");
        }

        public FontPack? FontPack { get; }

        public void ShowMessageBox(string message) => ShowMessageBox(message, "Notice");
        public void ShowMessageBox(string text, string title) => ShowMessageBox(text, title, str => str, "Ok");

        public T? ShowMessageBox<T>(string text, string title, Func<T, string>? toButtonLabel, params T[]? buttons)
        {
            return Show(title, new MessageBox<T>(text, buttons, toButtonLabel), new SimpleWindowOptions()
            {
                Size = new Vector2(400, 200),
                SizeFlags = WindowSizeFlags.ResizeGui | WindowSizeFlags.ResizeWindow,
                AlwaysOnTop = true,
                Fps = 60,
                Vsync = true
            });
        }

        public T? ShowMessageBox<T>(string text, string title, Func<T, string>? toButtonLabel,
            SimpleWindowOptions options, params T[]? buttons)
        {
            return Show(title, new MessageBox<T>(text, buttons, toButtonLabel), options);
        }
    }
}