using ImGuiWindows.DataTypes;

namespace ImGuiWindows
{
    /// <summary>
    /// Base class for drawers that produce a result (dialogs, prompts, message boxes).
    /// </summary>
    /// <remarks>
    /// The result is a plain property and closing is a plain flag, so a drawer that is driven synchronously -
    /// draw, assign <see cref="Result"/>, close - pays nothing extra. There are no events, no cancellation
    /// source, and no task unless <see cref="Completion"/> is actually awaited.
    /// </remarks>
    public abstract class AsyncImguiDrawer<T> : IImguiDrawer<T>
    {
        /// <summary>
        /// Gets the result produced by the drawer. Assigning a non-null value closes the window.
        /// </summary>
        public T? Result
        {
            get;
            protected set
            {
                field = value;
                if (value is not null)
                {
                    _closeRequested = true;
                }
            }
        }

        /// <summary>
        /// Gets a task that completes with <see cref="Result"/> once the window has closed. It is created on first
        /// access, so drawers whose result is consumed synchronously never allocate one.
        /// </summary>
        public Task<T?> Completion => (_completion ??= CreateCompletion()).Task;

        /// <summary>Requests that the window closes at the end of the current frame, without producing a result.</summary>
        public void ForceClose() => _closeRequested = true;

        public virtual void Init()
        {
        }

        public virtual void OnFileDrop(IReadOnlyList<string> filePaths)
        {
        }

        public virtual void OnWindowFocusChanged(bool changedTo)
        {
        }

        /// <summary>
        /// Draws the drawer and lets it decide whether the window should close. A close requested through
        /// <see cref="Result"/> or <see cref="ForceClose"/> is applied here, so subclasses never set it themselves.
        /// </summary>
        public void Draw(double deltaSeconds, ImFonts fonts, float dpiScale, ImguiInputContext inputContext,
            out bool shouldTerminate)
        {
            OnRenderImpl(deltaSeconds, fonts, dpiScale, inputContext, out shouldTerminate);
            shouldTerminate |= _closeRequested;
        }

        protected abstract void OnRenderImpl(double deltaSeconds, ImFonts fonts, float dpiScale,
            ImguiInputContext inputContext, out bool shouldTerminate);

        public void OnClose()
        {
            _closed = true;
            _completion?.TrySetResult(Result);
        }

        private TaskCompletionSource<T?> CreateCompletion()
        {
            var completion = new TaskCompletionSource<T?>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_closed)
            {
                completion.TrySetResult(Result);
            }

            return completion;
        }

        private TaskCompletionSource<T?>? _completion;
        private bool _closeRequested;
        private bool _closed;
    }
}
