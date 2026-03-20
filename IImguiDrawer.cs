namespace ImGuiWindows
{
    public interface IImguiDrawer
    {
        public void Init();
        public void Draw(double deltaSeconds, ImFonts fonts, float dpiScale, out bool shouldTerminate);

        public void OnClose();
        public void OnFileDrop(IReadOnlyList<string> filePaths);
        public void OnWindowFocusChanged(bool changedTo);
        public Action? MainMenuBarAction => null;
    }

    public interface IImguiDrawer<out T> : IImguiDrawer
    {
        public T? Result { get; }
    }
}