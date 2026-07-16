using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotStreamDeltaBuffer
    {
        public const int DefaultMaximumPendingCharacters = 64 * 1024;
        public const int DefaultMaximumPendingSegments = 64;

        private readonly Action<IReadOnlyList<CopilotStreamDelta>> _applyBatch;
        private readonly Func<bool> _isOnTargetThread;
        private readonly SynchronizationContext? _targetContext;
        private readonly int _maximumPendingCharacters;
        private readonly int _maximumPendingSegments;
        private readonly object _syncRoot = new();
        private readonly List<PendingSegment> _pending = new();
        private Exception? _failure;
        private int _pendingCharacters;
        private bool _completed;
        private bool _flushScheduled;

        public CopilotStreamDeltaBuffer(
            SynchronizationContext? targetContext,
            Action<IReadOnlyList<CopilotStreamDelta>> applyBatch,
            int maximumPendingCharacters = DefaultMaximumPendingCharacters,
            int maximumPendingSegments = DefaultMaximumPendingSegments,
            Func<bool>? isOnTargetThread = null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumPendingCharacters, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumPendingSegments, 1);

            _targetContext = targetContext;
            var targetThreadId = Environment.CurrentManagedThreadId;
            _isOnTargetThread = isOnTargetThread ?? (() => Environment.CurrentManagedThreadId == targetThreadId);
            _applyBatch = applyBatch ?? throw new ArgumentNullException(nameof(applyBatch));
            _maximumPendingCharacters = maximumPendingCharacters;
            _maximumPendingSegments = maximumPendingSegments;
        }

        public void Enqueue(CopilotStreamDelta delta)
        {
            if (!delta.HasAny)
                return;

            if (_targetContext == null || IsOnTargetThread())
            {
                FlushPending(captureFailure: false);
                ThrowIfUnavailable();
                _applyBatch([delta]);
                return;
            }

            var shouldPost = false;
            var requiresBackpressure = false;
            lock (_syncRoot)
            {
                ThrowIfUnavailableNoLock();
                AppendNoLock(delta);
                if (!_flushScheduled)
                {
                    _flushScheduled = true;
                    shouldPost = true;
                }

                requiresBackpressure = _pendingCharacters >= _maximumPendingCharacters
                    || _pending.Count >= _maximumPendingSegments;
            }

            if (shouldPost)
                _targetContext.Post(static state => ((CopilotStreamDeltaBuffer)state!).FlushPending(captureFailure: true), this);
            if (requiresBackpressure)
            {
                _targetContext.Send(static state => ((CopilotStreamDeltaBuffer)state!).FlushPending(captureFailure: true), this);
                ThrowIfUnavailable();
            }
        }

        public Task CompleteAsync()
        {
            lock (_syncRoot)
                _completed = true;

            if (_targetContext == null || IsOnTargetThread())
            {
                FlushPending(captureFailure: false);
                ThrowIfFailed();
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _targetContext.Post(static state =>
            {
                var request = (FlushRequest)state!;
                try
                {
                    request.Owner.FlushPending(captureFailure: false);
                    request.Owner.ThrowIfFailed();
                    request.Completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    request.Completion.TrySetException(exception);
                }
            }, new FlushRequest(this, completion));
            return completion.Task;
        }

        private bool IsOnTargetThread() => _isOnTargetThread();

        private void AppendNoLock(CopilotStreamDelta delta)
        {
            _pendingCharacters += (delta.ReasoningContent?.Length ?? 0) + (delta.Content?.Length ?? 0);
            if (_pending.Count > 0 && _pending[^1].HasSameShape(delta))
            {
                _pending[^1].Append(delta);
                return;
            }

            _pending.Add(new PendingSegment(delta));
        }

        private void FlushPending(bool captureFailure)
        {
            CopilotStreamDelta[] batch;
            lock (_syncRoot)
            {
                if (_pending.Count == 0)
                {
                    _flushScheduled = false;
                    return;
                }

                batch = new CopilotStreamDelta[_pending.Count];
                for (var index = 0; index < _pending.Count; index++)
                    batch[index] = _pending[index].ToDelta();
                _pending.Clear();
                _pendingCharacters = 0;
                _flushScheduled = false;
            }

            if (!captureFailure)
            {
                _applyBatch(batch);
                return;
            }

            try
            {
                _applyBatch(batch);
            }
            catch (Exception exception)
            {
                lock (_syncRoot)
                    _failure ??= exception;
            }
        }

        private void ThrowIfUnavailable()
        {
            lock (_syncRoot)
                ThrowIfUnavailableNoLock();
        }

        private void ThrowIfFailed()
        {
            lock (_syncRoot)
            {
                if (_failure != null)
                    throw new InvalidOperationException("Applying streamed Copilot output failed.", _failure);
            }
        }

        private void ThrowIfUnavailableNoLock()
        {
            if (_failure != null)
                throw new InvalidOperationException("Applying streamed Copilot output failed.", _failure);
            if (_completed)
                throw new InvalidOperationException("The streamed Copilot output buffer is already complete.");
        }

        private sealed record FlushRequest(CopilotStreamDeltaBuffer Owner, TaskCompletionSource Completion);

        private sealed class PendingSegment
        {
            private readonly StringBuilder _reasoning = new();
            private readonly StringBuilder _content = new();

            public PendingSegment(CopilotStreamDelta delta)
            {
                HasReasoning = delta.HasReasoning;
                HasContent = delta.HasContent;
                Append(delta);
            }

            public bool HasReasoning { get; }

            public bool HasContent { get; }

            public void Append(CopilotStreamDelta delta)
            {
                if (delta.ReasoningContent != null)
                    _reasoning.Append(delta.ReasoningContent);
                if (delta.Content != null)
                    _content.Append(delta.Content);
            }

            public bool HasSameShape(CopilotStreamDelta delta) =>
                HasReasoning != HasContent
                && HasReasoning == delta.HasReasoning
                && HasContent == delta.HasContent;

            public CopilotStreamDelta ToDelta() => new(_reasoning.ToString(), _content.ToString());
        }
    }
}
