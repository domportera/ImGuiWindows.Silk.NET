using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using ImGuiWindows.Contracts;
using ImGuiWindows.DataTypes;
using Silk.NET.Input;

namespace ImGuiWindows
{
    internal sealed class ImGuiHandler
    {
        private void RentContext(out nint context)
        {
            _contextLock.Enter();
            context = _context;
        }

        private void ReturnContext() => _contextLock.Exit();

        public ImGuiHandler(FontPack? fontPack, Lock? lockObj,
            bool autoScaleContent, string title, Action<ImGuiIOPtr> initFonts, ImguiInputContext imguiInputContext)
        {
            _mainWindowId = title;
            _autoScaleContent = autoScaleContent;
            _contextLock = lockObj ?? new Lock();
            _imguiInputContext = imguiInputContext;


            _context = ImGui.CreateContext();
            var previousContext = ImGui.GetCurrentContext();
            ImGui.SetCurrentContext(_context);
            ImportFonts(fontPack, out _fontObj);
            var io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable; // is this one necessary?
            // todo: enable additional config flags, e.g. DPI awareness
            initFonts(io);

            if (previousContext != nint.Zero && previousContext != _context)
            {
                ImGui.SetCurrentContext(previousContext);
            }

            return;

            static void ImportFonts(FontPack? maybeFontPack, out ImFonts fontObj)
            {
                if (!maybeFontPack.HasValue)
                {
                    fontObj = new ImFonts([]);
                    ImGui.GetIO().Fonts.AddFontDefault();
                    return;
                }

                var fontPack = maybeFontPack.Value;

                var io = ImGui.GetIO();
                var fontAtlasPtr = io.Fonts;
                var fonts = new ImFontPtr[4];
                fonts[0] = fontAtlasPtr.AddFontFromFileTTF(fontPack.Small.Path, fontPack.Small.PixelSize);
                fonts[1] = fontAtlasPtr.AddFontFromFileTTF(fontPack.Regular.Path, fontPack.Regular.PixelSize);
                fonts[2] = fontAtlasPtr.AddFontFromFileTTF(fontPack.Bold.Path, fontPack.Bold.PixelSize);
                fonts[3] = fontAtlasPtr.AddFontFromFileTTF(fontPack.Large.Path, fontPack.Large.PixelSize);

                if (!fontAtlasPtr.Build())
                {
                    ImGuiLog.Error("Failed to build font atlas");
                }

                fontObj = new ImFonts(fonts);
            }
        }

        public void Draw(in DrawArgs args, IImguiDrawer drawer, out bool shouldTerminate)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(ImGuiHandler));
            }

            if (args.Display.DrawableWidth < 1 || args.Display.DrawableHeight < 1)
            {
                shouldTerminate = false;
                return;
            }


            _contextLock.Enter();
            var c = new ContextContainer
            {
                Context = _context,
                ContextToRestore = ImGui.GetCurrentContext(),
                FontObj = _fontObj,
                AutoScaleContent = _autoScaleContent,
                SystemWindowScaling = args.SystemWindowScaling,
            };

            BeginContext(_context, args, ref c);

            _contextLock.Exit();

            // todo - do we need to lock the context for the duration of this entire method?
            // probably.. since this is not process-based, but rather frame-based
            // so we can't have multiple threads drawing at the same time
            DrawFrame(drawer, new Vector2(args.Display.DrawableWidth, args.Display.DrawableHeight), args.DeltaSeconds,
                args.SystemWindowScaling, out shouldTerminate);

            ImGuiLog.PushLogs();
            _contextLock.Enter();
            EndContext(ref c, args);
            _contextLock.Exit();
        }

        //private void UpdateClearColor()
        //{
        //    var colorVector = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
        //    var colorVecByteValue = colorVector * 255;
        //    ClearColor = Color.FromArgb((int)colorVecByteValue.X, (int)colorVecByteValue.Y, (int)colorVecByteValue.Z, (int)colorVecByteValue.W);
        //}


        private static unsafe void BeginContext(nint context, in DrawArgs args,
            ref ContextContainer c)
        {
            if (c.HasContextToRestore)
            {
                // we copy the style from the current context
                ImGuiStyle copiedStyle = default;
                Unsafe.Copy(destination: ref copiedStyle, source: ImGui.GetStyle().NativePtr);

                // we switch to our own context
                ImGui.SetCurrentContext(context);

                // we apply the copied style to our context
                Unsafe.Copy(ImGui.GetStyle().NativePtr, ref copiedStyle);
            }
            else if (c.ContextToRestore != c.Context)
            {
                ImGui.SetCurrentContext(context);
                ImGui.StyleColorsDark();
            }

            if (c.AutoScaleContent && c.FontObj is not null)
            {
                Span<float> originalFontScales = stackalloc float[5];
                // ApplyScaleFactor(c.FontObj, c.SystemWindowScaling, c.OriginalStyle, originalFontScales);
                fixed (float* fontScales = c.OriginalFontScales)
                {
                    //     originalFontScales.CopyTo(new Span<float>(fontScales, 5));
                }
            }

            var io = ImGui.GetIO();
            SetPerFrameImGuiData(ref io, args);
            args.ImguiInput.BeginImGuiInput(io);


            ImGui.NewFrame();

            return;

            static void ApplyScaleFactor(ImFonts fontObj, float systemWindowScaling, ImGuiStylePtr originalStyle,
                Span<float> originalFontScales)
            {
                var scaleFactor = 1f / systemWindowScaling;
                originalStyle.ScaleAllSizes(scaleFactor);
                var currentFont = ImGui.GetFont();
                if (currentFont.NativePtr is not null)
                {
                    originalFontScales[4] = currentFont.Scale;
                    currentFont.Scale *= scaleFactor;
                }

                for (var i = 0; i < fontObj.Count; i++)
                {
                    originalFontScales[i] = fontObj[i].Scale;
                    fontObj[i].Scale *= scaleFactor;
                }
            }

            // Sets per-frame data based on the associated window.
            static void SetPerFrameImGuiData(ref ImGuiIOPtr io, in DrawArgs args)
            {
                ref readonly var d = ref args.Display;

                if (d.WindowWidth is not null && d.WindowHeight is not null)
                {
                    io.DisplaySize = new Vector2(d.WindowWidth.Value, d.WindowHeight.Value);
                    io.DisplayFramebufferScale = new Vector2(d.DrawableWidth, d.DrawableHeight) / new Vector2(
                        d.WindowWidth.Value, d.WindowHeight.Value);
                }
                else
                {
                    io.DisplaySize = new Vector2(d.DrawableWidth, d.DrawableHeight);
                    io.DisplayFramebufferScale = Vector2.One;
                }

                io.DeltaTime = (float)args.DeltaSeconds;
            }
        }

        private static unsafe void EndContext(ref ContextContainer context, in DrawArgs drawArgs)
        {
            ImGui.EndFrame();

            if (context.AutoScaleContent && context.FontObj is not null)
            {
                fixed (float* fontScales = context.OriginalFontScales)
                {
                    //     RevertScaleFactor(context.FontObj, context.SystemWindowScaling, context.OriginalStyle, fontScales);
                }
            }

            drawArgs.ImguiInput.EndImGuiInput();

            if (context.HasContextToRestore)
            {
                ImGui.SetCurrentContext(context.ContextToRestore);
            }

            return;

            static void RevertScaleFactor(ImFonts fontObj, float systemWindowScaling, ImGuiStylePtr originalStyle,
                float* originalFontScales)
            {
                if (originalStyle.NativePtr is not null)
                {
                    originalStyle.ScaleAllSizes(systemWindowScaling);
                }

                var originalFontScale = originalFontScales[4];
                if (originalFontScale > 0f)
                {
                    ImGui.GetFont().Scale = originalFontScales[4];
                }

                for (var i = 0; i < fontObj.Count; i++)
                {
                    var og = originalFontScales[i];
                    ref var font = ref fontObj[i];
                    if (font.NativePtr is not null && og > 0f)
                    {
                        font.Scale = og;
                    }
                }
            }
        }


        private unsafe ref struct ContextContainer
        {
            public nint Context { get; init; }
            public bool HasContextToRestore => ContextToRestore != nint.Zero && Context != ContextToRestore;
            public ImFonts? FontObj { get; init; }
            public nint ContextToRestore;
            public bool AutoScaleContent { get; init; }
            public float SystemWindowScaling { get; init; }

            public fixed float OriginalFontScales[5];
        }

        public readonly ref struct DrawArgs(
            DisplayInfo display,
            double deltaSeconds,
            InputContext inputContext,
            ImguiInputContext imguiInput,
            float systemWindowScaling)
        {
            public readonly DisplayInfo Display = display;
            public readonly double DeltaSeconds = deltaSeconds;
            public readonly InputContext InputContext = inputContext;
            public readonly ImguiInputContext ImguiInput = imguiInput;
            public readonly float SystemWindowScaling = systemWindowScaling;
        }

        public readonly ref struct DisplayInfo(
            int? windowWidth,
            int? windowHeight,
            int drawableWidth,
            int drawableHeight)
        {
            public readonly int? WindowWidth = windowWidth;
            public readonly int? WindowHeight = windowHeight;
            public readonly int DrawableWidth = drawableWidth;
            public readonly int DrawableHeight = drawableHeight;
        }


        private void DrawFrame(IImguiDrawer drawer, Vector2 drawableSize, double deltaTime, float systemWindowScaling,
            out bool shouldTerminate)
        {
            const ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoMove |
                                                 ImGuiWindowFlags.NoResize |
                                                 ImGuiWindowFlags.NoTitleBar |
                                                 ImGuiWindowFlags.AlwaysAutoResize |
                                                 ImGuiWindowFlags.NoBringToFrontOnFocus;

            var mainMenuBarAction = drawer.MainMenuBarAction;

            var flags = windowFlags;
            if (mainMenuBarAction != null)
            {
                flags |= ImGuiWindowFlags.MenuBar;
            }

            ImGui.SetNextWindowSize(drawableSize);
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
                            ImGuiLog.Error($"Error in main menu bar action: {ex.Message}");
                        }

                        ImGui.EndMenuBar();
                    }
                }


                try
                {
                    drawer.Draw(deltaTime, _fontObj!, systemWindowScaling, _imguiInputContext, out shouldTerminate);
                }
                catch (Exception ex)
                {
                    shouldTerminate = true;
                    ImGuiLog.Error($"Error rendering {_mainWindowId} imgui window: {ex.Message}");
                }

                if (_lowMemoryAlertedTime + LowMemoryAlertDurationSeconds < Stopwatch.GetTimestampInSeconds())
                {
                    if (ImGui.BeginPopup("Low memory alert"))
                    {
                        ImGui.Text("Low memory detected.");
                        ImGui.EndPopup();
                    }
                }
            }
            else
            {
                shouldTerminate = false;
            }

            ImGui.End();
        }

        public void Dispose()
        {
            _isDisposed = true;
            lock (_contextLock)
            {
                ImGui.DestroyContext(_context);
            }
        }


        public void Render(IImguiImplementation impl)
        {
            // todo - only render if our draw args changed from last time
            RentContext(out var context);

            var oldCtx = ImGui.GetCurrentContext();
            if (oldCtx != context)
            {
                ImGui.SetCurrentContext(context);
            }

            ImGui.Render();
            impl.RenderImDrawData(ImGui.GetDrawData());

            if (oldCtx != context)
            {
                ImGui.SetCurrentContext(oldCtx);
            }

            ReturnContext();
        }

        public void LowMemoryAlert()
        {
            _lowMemoryAlertedTime = Stopwatch.GetTimestampInSeconds();
            ImGuiLog.Warn("Low memory detected");
        }

        private readonly ImguiInputContext _imguiInputContext;
        private readonly string _mainWindowId;
        private readonly Lock _contextLock;
        private readonly ImFonts? _fontObj;
        private readonly nint _context;
        private readonly bool _autoScaleContent;
        private bool _isDisposed;
        private double _lowMemoryAlertedTime = double.MinValue;
        private const double LowMemoryAlertDurationSeconds = 3;
    }
}