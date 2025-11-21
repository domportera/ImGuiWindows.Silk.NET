using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;

namespace ImGuiWindows
{
    internal sealed class ImGuiHandler
    {
        private readonly IImguiDrawer _drawer;
        private readonly string _windowTitle;
        private readonly IntPtr? _originalContext;
        private readonly object _contextLock;
        private readonly FontPack? _fontPack;
        private readonly string _mainWindowId;
        private ImFonts? _fontObj;
        private readonly IntPtr _context;
        private readonly bool _autoScaleContent;


        public ImGuiHandler(IImguiImplementation impl, IImguiDrawer drawer, FontPack? fontPack, object? lockObj,
            bool autoScaleContent)
        {
            _autoScaleContent = autoScaleContent;
            _windowTitle = impl.Title;
            _mainWindowId = impl.MainWindowId;
            _drawer = drawer;
            _fontPack = fontPack;
            _contextLock = lockObj ?? new object();
            _imguiController = impl;

            lock (_contextLock)
            {
                var previousContext = ImGui.GetCurrentContext();
                _originalContext = previousContext == IntPtr.Zero ? null : previousContext;
                _context = impl.InitializeControllerContext(InitializeStyle);
                _drawer.Init();
            }
        }

        private unsafe void InitializeStyle()
        {
            if (_originalContext.HasValue && _originalContext != _context)
            {
                var myContext = ImGui.GetCurrentContext();

                // first we switch to the previous imgui context
                ImGui.SetCurrentContext(_originalContext.Value);

                // we copy the style from the previous context
                ImGuiStyle copiedStyle = default;
                Unsafe.Copy(destination: ref copiedStyle, source: ImGui.GetStyle().NativePtr);

                // we switch back to our own context
                ImGui.SetCurrentContext(myContext);

                // we apply the copied style to the current context
                Unsafe.Copy(ImGui.GetStyle().NativePtr, ref copiedStyle);
            }

            var colorVector = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
            var colorVecByteValue = colorVector * 255;
            ClearColor = Color.FromArgb((int)colorVecByteValue.X, (int)colorVecByteValue.Y, (int)colorVecByteValue.Z);

            // do we need or want to do this?
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;

            if (!_fontPack.HasValue)
            {
                _fontObj = new ImFonts([]);
                return;
            }

            var fontPack = _fontPack.Value;

            var io = ImGui.GetIO();
            var fontAtlasPtr = io.Fonts;
            var fonts = new ImFontPtr[4];
            fonts[0] = fontAtlasPtr.AddFontFromFileTTF(fontPack.Small.Path, fontPack.Small.PixelSize);
            fonts[1] = fontAtlasPtr.AddFontFromFileTTF(fontPack.Regular.Path, fontPack.Regular.PixelSize);
            fonts[2] = fontAtlasPtr.AddFontFromFileTTF(fontPack.Bold.Path, fontPack.Bold.PixelSize);
            fonts[3] = fontAtlasPtr.AddFontFromFileTTF(fontPack.Large.Path, fontPack.Large.PixelSize);

            if (!fontAtlasPtr.Build())
            {
                Console.WriteLine("Failed to build font atlas");
            }

            _fontObj = new ImFonts(fonts);
        }

        public void Draw(Vector2 windowSize, double deltaTime, float systemWindowScaling)
        {
            lock (_contextLock)
            {
                var contextToRestore = ImGui.GetCurrentContext();
                if (contextToRestore == IntPtr.Zero || contextToRestore == _context)
                {
                    contextToRestore = _originalContext ?? _context;
                }

                ImGui.SetCurrentContext(_context);
                var originalStyle = ImGui.GetStyle();
                Span<float> originalFontScales = stackalloc float[5];
                if (_autoScaleContent)
                {
                    ApplyScaleFactor(systemWindowScaling, originalStyle, originalFontScales);
                }

                if (_imguiController.StartImguiFrame((float)deltaTime))
                {
                    DrawFrame(windowSize, deltaTime, systemWindowScaling);
                    _imguiController.EndImguiFrame();
                }

                if (_autoScaleContent)
                {
                    RevertScaleFactor(systemWindowScaling, originalStyle, originalFontScales);
                }

                // restore
                ImGui.SetCurrentContext(contextToRestore);
            }
        }

        private void RevertScaleFactor(float systemWindowScaling, ImGuiStylePtr originalStyle, Span<float> originalFontScales)
        {
            originalStyle.ScaleAllSizes(systemWindowScaling);
            ImGui.GetFont().Scale = originalFontScales[4];
            for (int i = 0; i < _fontObj!.Count; i++)
            {
                _fontObj[i].Scale = originalFontScales[i];
            }
        }

        private void ApplyScaleFactor(float systemWindowScaling, ImGuiStylePtr originalStyle, Span<float> originalFontScales)
        {
            var scaleFactor = 1f / systemWindowScaling;
            originalStyle.ScaleAllSizes(scaleFactor);
            originalFontScales[4] = ImGui.GetFont().Scale;
            ImGui.GetFont().Scale *= scaleFactor;
            for (int i = 0; i < _fontObj!.Count; i++)
            {
                originalFontScales[i] = _fontObj[i].Scale;
                _fontObj[i].Scale *= scaleFactor;
            }
        }

        private void DrawFrame(Vector2 windowSize, double deltaTime, float systemWindowScaling)
        {
            const ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoMove |
                                                 ImGuiWindowFlags.NoResize |
                                                 ImGuiWindowFlags.NoTitleBar |
                                                 ImGuiWindowFlags.AlwaysAutoResize;

            var mainMenuBarAction = _drawer.MainMenuBarAction;

            var flags = windowFlags;
            if (mainMenuBarAction != null)
            {
                flags |= ImGuiWindowFlags.MenuBar;
            }

            ImGui.SetNextWindowSize(windowSize);
            ImGui.SetNextWindowPos(Vector2.Zero);
            
            if (ImGui.Begin(_mainWindowId, flags))
            {
                if (mainMenuBarAction != null)
                {
                    if (ImGui.BeginMenuBar())
                    {
                        try
                        {
                            mainMenuBarAction();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in main menu bar action: {ex.Message}");
                        }

                        ImGui.EndMenuBar();
                    }
                }

                try
                {
                    _drawer.OnRender(_windowTitle, deltaTime, _fontObj!, systemWindowScaling);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error rendering {_windowTitle} imgui window: {ex.Message}");
                }
            }

            ImGui.End();
        }

        public void Dispose()
        {
            lock (_contextLock)
            {
                _drawer.OnClose();
                _imguiController.Dispose();
            }
        }

        private readonly IImguiImplementation _imguiController;
        public Color? ClearColor { get; private set; }

        // forwards window events
        public void OnWindowUpdate(double deltaSeconds, out bool shouldCloseWindow)
        {
            _drawer.OnWindowUpdate(deltaSeconds, out shouldCloseWindow);

            // calculate dpi 
        }

        public void OnWindowFocusChanged(bool isFocused)
        {
            _drawer.OnWindowFocusChanged(isFocused);
        }

        public void OnFileDrop(string[] filePaths)
        {
            _drawer.OnFileDrop(filePaths);
        }
    }
}