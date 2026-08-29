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
            public long Sequence;
            public Guid InvocationId;
            public AlgorithmInvocationClaim? Claim;
            public CancellationTokenSource? Cancellation;
            public Window? ResultWindow;
        }

        private readonly record struct StateSnapshot(
            long Sequence,
            Guid InvocationId,
            AlgorithmInvocationClaim? Claim,
            CancellationTokenSource? Cancellation,
            Window? ResultWindow);

        private static readonly ConditionalWeakTable<ImageProcessingContext, State> States = new();

        public static CancellationTokenSource Begin(
            ImageProcessingContext image,
            Guid documentId,
            long sourceRevision,
            Guid ownerId,
            Guid invocationId,
            Action<AlgorithmInvocationClaim>? beforeStateAccept = null)
        {
            State state = States.GetOrCreateValue(image);
            CancellationTokenSource cancellation = new();
            CancellationTokenSource? supersededCancellation = null;
            Window? supersededWindow = null;
            StateSnapshot previousState = default;
            AlgorithmInvocationClaim installedClaim = default;
            bool stateInstalled = false;
            bool began;
            try
            {
                began = image.TryBeginAlgorithmAnalysisInvocation(
                        ownerId,
                        documentId,
                        sourceRevision,
                        invocationId,
                        cancellation,
                        claim =>
                        {
                            beforeStateAccept?.Invoke(claim);
                            if (!image.IsCurrentAlgorithmInvocation(claim))
                                throw new InvalidOperationException("The analysis claim was superseded during acceptance.");
                            lock (state.Sync)
                            {
                                if (claim.Sequence <= state.Sequence && state.Claim != claim)
                                    throw new InvalidOperationException("A stale analysis claim cannot replace newer session state.");
                                previousState = Snapshot(state);
                                if (state.Claim.HasValue && state.Claim.Value != claim)
                                {
                                    supersededCancellation = state.Cancellation;
                                    supersededWindow = state.ResultWindow;
                                }
                                state.Sequence = claim.Sequence;
                                state.InvocationId = invocationId;
                                state.Claim = claim;
                                state.Cancellation = cancellation;
                                state.ResultWindow = null;
                                installedClaim = claim;
                                stateInstalled = true;
                            }
                        },
                        out _);
            }
            catch
            {
                if (stateInstalled)
                {
                    lock (state.Sync)
                    {
                        if (state.Claim == installedClaim
                            && state.InvocationId == invocationId
                            && ReferenceEquals(state.Cancellation, cancellation))
                        {
                            Restore(state, previousState);
                        }
                    }
                }
                TryCancel(cancellation);
                cancellation.Dispose();
                throw;
            }
            if (!began)
            {
                cancellation.Cancel();
                return cancellation;
            }
            TryCancel(supersededCancellation);
            Close(supersededWindow);
            return cancellation;
        }

        public static void CompleteRun(ImageProcessingContext image, Guid invocationId, CancellationTokenSource cancellation)
        {
            State state = States.GetOrCreateValue(image);
            AlgorithmInvocationClaim? claim = null;
            lock (state.Sync)
            {
                if (state.InvocationId == invocationId && ReferenceEquals(state.Cancellation, cancellation))
                {
                    state.Cancellation = null;
                    claim = state.Claim;
                }
            }
            if (claim.HasValue) image.CompleteAlgorithmInvocationRun(claim.Value, cancellation);
        }

        public static bool IsCurrent(ImageProcessingContext image, Guid documentId, long revision, Guid invocationId)
        {
            State state = States.GetOrCreateValue(image);
            AlgorithmInvocationClaim? claim;
            lock (state.Sync)
            {
                claim = state.InvocationId == invocationId ? state.Claim : null;
            }
            return claim.HasValue
                && image.IsCurrentAlgorithmInvocation(claim.Value)
                && !image.IsDisposed
                && image.DocumentInstanceId == documentId
                && image.IsCurrentImageRevision(revision);
        }

        internal static Guid TrackedInvocationId(ImageProcessingContext image)
        {
            State state = States.GetOrCreateValue(image);
            lock (state.Sync) return state.InvocationId;
        }

        public static bool CanPresent(ImageProcessingContext image, Guid documentId, long revision, Guid invocationId, out Window? previous)
        {
            State state = States.GetOrCreateValue(image);
            AlgorithmInvocationClaim? claim;
            lock (state.Sync)
            {
                claim = state.InvocationId == invocationId ? state.Claim : null;
            }
            bool current = claim.HasValue
                && image.IsCurrentAlgorithmInvocation(claim.Value)
                && !image.IsDisposed
                && image.DocumentInstanceId == documentId
                && image.IsCurrentImageRevision(revision);
            lock (state.Sync)
            {
                current &= state.InvocationId == invocationId && state.Claim == claim;
                previous = current ? state.ResultWindow : null;
                if (current) state.ResultWindow = null;
            }
            return current;
        }

        public static bool Present(ImageProcessingContext image, Guid invocationId, Window window)
        {
            State state = States.GetOrCreateValue(image);
            AlgorithmInvocationClaim? claim;
            lock (state.Sync)
            {
                claim = state.InvocationId == invocationId ? state.Claim : null;
            }
            if (!claim.HasValue || !image.IsCurrentAlgorithmInvocation(claim.Value)) return false;
            lock (state.Sync)
            {
                if (state.InvocationId != invocationId || state.Claim != claim) return false;
                state.ResultWindow = window;
            }
            window.Closed += (_, _) =>
            {
                AlgorithmInvocationClaim? claim = null;
                lock (state.Sync)
                {
                    if (ReferenceEquals(state.ResultWindow, window))
                    {
                        state.ResultWindow = null;
                        claim = state.Claim;
                        state.Claim = null;
                        state.InvocationId = Guid.Empty;
                        state.Cancellation = null;
                    }
                }
                if (claim.HasValue) image.TryReleaseAlgorithmInvocation(claim.Value);
            };

            if (IsClaimCurrent(image, state, invocationId)) return true;
            lock (state.Sync)
            {
                if (ReferenceEquals(state.ResultWindow, window)) state.ResultWindow = null;
            }
            return false;
        }

        public static void Release(ImageProcessingContext image, Guid invocationId)
        {
            State state = States.GetOrCreateValue(image);
            CancellationTokenSource? cancellation;
            AlgorithmInvocationClaim? claim;
            Window? resultWindow;
            lock (state.Sync)
            {
                if (state.InvocationId != invocationId) return;
                state.InvocationId = Guid.Empty;
                claim = state.Claim;
                state.Claim = null;
                cancellation = state.Cancellation;
                state.Cancellation = null;
                resultWindow = state.ResultWindow;
                state.ResultWindow = null;
            }

            if (claim.HasValue) image.TryReleaseAlgorithmInvocation(claim.Value);
            TryCancel(cancellation);
            Close(resultWindow);
        }

        public static void ObserveClaim(ImageProcessingContext image, AlgorithmInvocationClaim observedClaim)
        {
            State state = States.GetOrCreateValue(image);

            CancellationTokenSource? cancellation = null;
            Window? resultWindow = null;
            lock (state.Sync)
            {
                if (observedClaim.Sequence < state.Sequence) return;
                state.Sequence = observedClaim.Sequence;
                if (state.Claim.HasValue && state.Claim.Value != observedClaim)
                {
                    state.InvocationId = Guid.Empty;
                    state.Claim = null;
                    cancellation = state.Cancellation;
                    state.Cancellation = null;
                    resultWindow = state.ResultWindow;
                    state.ResultWindow = null;
                }
            }

            TryCancel(cancellation);
            Close(resultWindow);
        }

        public static void Invalidate(ImageProcessingContext image)
            => DetachForDocumentMutation(image)();

        internal static Action DetachForDocumentMutation(ImageProcessingContext image)
            => DetachForDocumentMutation(image, image.DocumentInstanceId, long.MaxValue);

        internal static Action DetachForDocumentMutation(
            ImageProcessingContext image,
            Guid documentInstanceId,
            long revisionExclusive)
        {
            State state = States.GetOrCreateValue(image);
            CancellationTokenSource? cancellation = null;
            Window? resultWindow = null;
            lock (state.Sync)
            {
                if (state.Claim is AlgorithmInvocationClaim claim
                    && claim.Scope.DocumentInstanceId == documentInstanceId
                    && claim.Scope.SourceRevision < revisionExclusive)
                {
                    state.InvocationId = Guid.Empty;
                    state.Claim = null;
                    cancellation = state.Cancellation;
                    state.Cancellation = null;
                    resultWindow = state.ResultWindow;
                    state.ResultWindow = null;
                }
            }

            return () =>
            {
                TryCancel(cancellation);
                Close(resultWindow);
            };
        }

        private static bool IsClaimCurrent(ImageProcessingContext image, State state, Guid invocationId)
        {
            AlgorithmInvocationClaim? claim;
            lock (state.Sync)
            {
                claim = state.InvocationId == invocationId ? state.Claim : null;
            }
            return claim.HasValue && image.IsCurrentAlgorithmInvocation(claim.Value);
        }

        private static StateSnapshot Snapshot(State state)
            => new(state.Sequence, state.InvocationId, state.Claim, state.Cancellation, state.ResultWindow);

        private static void Restore(State state, StateSnapshot snapshot)
        {
            state.Sequence = snapshot.Sequence;
            state.InvocationId = snapshot.InvocationId;
            state.Claim = snapshot.Claim;
            state.Cancellation = snapshot.Cancellation;
            state.ResultWindow = snapshot.ResultWindow;
        }

        private static void Close(Window? window)
        {
            if (window == null) return;
            void CloseCore()
            {
                try { window.Close(); }
                catch
                {
                    if (window is IDisposable disposable)
                    {
                        try { disposable.Dispose(); }
                        catch { }
                    }
                }
            }

            if (window.Dispatcher.CheckAccess()) CloseCore();
            else _ = window.Dispatcher.BeginInvoke(CloseCore);
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
            Closed += (_, _) =>
            {
                // WPF closes owned windows directly when their owner closes. That path
                // raises Closed without raising this window's cancellable Closing event.
                if (!_completed) Cancel();
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
