using ImGuiNET;

namespace ImguiWindows
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
        public TData? Show<TData>(string title, IImguiDrawer<TData> drawer, bool autoSize = false, in SimpleWindowOptions? options = null)
        {
            Show(title, (IImguiDrawer)drawer, autoSize, options);
            return drawer.Result;
        }
        
        public void Show(string title, IImguiDrawer drawer, bool autoSize = false, in SimpleWindowOptions? options = null);
        public async Task ShowAsync(string title, IImguiDrawer drawer, bool autoSize = false, SimpleWindowOptions? options = null)
        {
            var windowTask = StartAsyncWindow(title, drawer, autoSize, options, FontPack);
            await windowTask;
        }
    
        // we can't simply return the result here, because nullable type constraints dont work between reference and value types
        public async Task ShowAsync<TData>(string title, AsyncImguiDrawer<TData> drawer, Action<TData> assign, bool autoSize = false, SimpleWindowOptions? options = null)
        {
            var windowTask = StartAsyncWindow(title, drawer, autoSize, options, FontPack);
        
            await foreach (var result in drawer.GetResults())
            {
                if (result != null)
                    assign(result);
            }
        
            await windowTask;
        }
        
        private async Task StartAsyncWindow(string title, IImguiDrawer drawer, bool autoSize, SimpleWindowOptions? options, FontPack? fontPack)
        {
            var context = SynchronizationContext.Current;
        
            await Task.Run(() =>
            {
                Show(title, drawer, autoSize, options);
            }).ConfigureAwait(false);
        
            SynchronizationContext.SetSynchronizationContext(context);
            Console.WriteLine("Completed window run");
        }

        public FontPack? FontPack { get; }
    
        public void ShowMessageBox(string message, bool autoScale = false) => ShowMessageBox(message, "Notice", autoScale);
        public void ShowMessageBox(string text, string title, bool autoScale = false) => ShowMessageBox(text, title, str => str, autoScale, "Ok");
    
        public T? ShowMessageBox<T>(string text, string title, Func<T, string>? toButtonLabel, bool autoScale, params T[]? buttons)
        {
            return Show(title, new MessageBox<T>(text, buttons, toButtonLabel), autoScale);
        }
    }
}