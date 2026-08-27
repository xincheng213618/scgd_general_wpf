using ColorVision.Algorithms;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Coordinates modeless analysis calls and result windows for one ImageView document.</summary>
    internal static class ImageAlgorithmAnalysisSession
    {
        private sealed class State
        {
            public readonly object Sync = new();
            public Guid InvocationId;
            public CancellationTokenSource? Cancellation;
            public Window? ResultWindow;
        }

        private static readonly ConditionalWeakTable<ImageProcessingContext, State> States = new();

        public static CancellationTokenSource Begin(ImageProcessingContext image, Guid invocationId)
        {
            State state = States.GetOrCreateValue(image);
            CancellationTokenSource cancellation = new();
            lock (state.Sync)
            {
                TryCancel(state.Cancellation);
                state.InvocationId = invocationId;
                state.Cancellation = cancellation;
            }
            return cancellation;
        }

        public static void CompleteRun(ImageProcessingContext image, Guid invocationId, CancellationTokenSource cancellation)
        {
            State state = States.GetOrCreateValue(image);
            lock (state.Sync)
            {
                if (state.InvocationId == invocationId && ReferenceEquals(state.Cancellation, cancellation)) state.Cancellation = null;
            }
        }

        public static bool IsCurrent(ImageProcessingContext image, Guid documentId, long revision, Guid invocationId)
        {
            State state = States.GetOrCreateValue(image);
            lock (state.Sync)
            {
                return state.InvocationId == invocationId
                    && !image.IsDisposed
                    && image.DocumentInstanceId == documentId
                    && image.IsCurrentImageRevision(revision);
            }
        }

        public static bool CanPresent(ImageProcessingContext image, Guid documentId, long revision, Guid invocationId, out Window? previous)
        {
            State state = States.GetOrCreateValue(image);
            lock (state.Sync)
            {
                bool current = state.InvocationId == invocationId
                    && !image.IsDisposed
                    && image.DocumentInstanceId == documentId
                    && image.IsCurrentImageRevision(revision);
                previous = current ? state.ResultWindow : null;
                if (current) state.ResultWindow = null;
                return current;
            }
        }

        public static void Present(ImageProcessingContext image, Guid invocationId, Window window)
        {
            State state = States.GetOrCreateValue(image);
            lock (state.Sync)
            {
                if (state.InvocationId != invocationId) return;
                state.ResultWindow = window;
            }
            window.Closed += (_, _) =>
            {
                lock (state.Sync)
                {
                    if (ReferenceEquals(state.ResultWindow, window)) state.ResultWindow = null;
                }
            };
        }

        private static void TryCancel(CancellationTokenSource? cancellation)
        {
            if (cancellation == null) return;
            try { cancellation.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    internal sealed class ImageAlgorithmProgressWindow : Window
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly ProgressBar _progress;
        private readonly TextBlock _status;
        private bool _completed;

        public ImageAlgorithmProgressWindow(string title, CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
            Title = title;
            Width = 380;
            Height = 155;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            StackPanel panel = new() { Margin = new Thickness(16) };
            _status = new TextBlock { Text = "准备执行...", Margin = new Thickness(0, 0, 0, 10) };
            _progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 18, Margin = new Thickness(0, 0, 0, 12) };
            Button cancel = new() { Content = "取消", Width = 80, HorizontalAlignment = HorizontalAlignment.Right };
            cancel.Click += (_, _) => Cancel();
            panel.Children.Add(_status);
            panel.Children.Add(_progress);
            panel.Children.Add(cancel);
            Content = panel;
            Closing += (_, args) =>
            {
                if (_completed) return;
                args.Cancel = true;
                Cancel();
            };
        }

        public bool WasCancelled { get; private set; }

        public void Report(AlgorithmProgress progress)
        {
            if (_completed || WasCancelled) return;
            _progress.Value = Math.Clamp(progress.Fraction, 0, 1) * 100;
            _status.Text = string.IsNullOrWhiteSpace(progress.Message) ? progress.Stage : $"{progress.Stage}：{progress.Message}";
        }

        public void Complete()
        {
            if (_completed) return;
            _completed = true;
            Close();
        }

        private void Cancel()
        {
            if (WasCancelled) return;
            WasCancelled = true;
            _status.Text = "正在取消...";
            _cancellation.Cancel();
        }
    }
}
