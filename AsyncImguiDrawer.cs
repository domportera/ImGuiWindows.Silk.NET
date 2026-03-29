using ImGuiWindows.DataTypes;

namespace ImGuiWindows
{
    public abstract class AsyncImguiDrawer<T> : IImguiDrawer<T>
    {
        public T? Result
        {
            get;
            protected set
            {
                field = value;

                _resultEvent.Set();
                _resultEvent.WaitOne();
                _resultEvent.Reset();

                if (CloseOnResult)
                {
                    _cts.Cancel();
                }
            }
        }

        private readonly AutoResetEvent _resultEvent = new(false);

        public async IAsyncEnumerable<T?> GetResults()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Run(() =>
                        {
                            _resultEvent.WaitOne();
                            _resultEvent.Set();
                        }, _cts.Token)
                        .ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                yield return Result;
            }
        }

        public abstract void Init();

        public void Draw(double deltaSeconds, ImFonts fonts, float dpiScale, out bool shouldTerminate)
        {
            OnRenderImpl(deltaSeconds, fonts, dpiScale, out shouldTerminate);
            shouldTerminate |= _cts.IsCancellationRequested;
        }
        
        protected abstract void OnRenderImpl(double deltaSeconds, ImFonts fonts, float dpiScale, out bool shouldTerminate);

        public void OnClose()
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            _cts.Dispose();
            _resultEvent.Reset();

            ClosingCallback?.Invoke();
        }

        public abstract void OnFileDrop(IReadOnlyList<string> filePaths);

        public abstract void OnWindowFocusChanged(bool changedTo);

        private readonly CancellationTokenSource _cts = new();

        public bool CloseOnResult { get; init; } = true;

        public Action? ClosingCallback { get; init; }

        public void ForceClose() => _cts.Cancel();
    }
}