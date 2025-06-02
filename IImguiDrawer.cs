
namespace ImguiWindows
{
    public interface IImguiDrawer
    {
        public void Init();
        public void OnRender(string windowName, double deltaSeconds, ImFonts fonts, float dpiScale);

        public void OnWindowUpdate(double deltaSeconds, out bool shouldClose);

        public void OnClose();
        public void OnFileDrop(string[] filePaths);
        public void OnWindowFocusChanged(bool changedTo);
    }

    public interface IImguiDrawer<out T> : IImguiDrawer
    {
        public T? Result { get; }
    }
}