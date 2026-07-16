using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal interface ICopilotPendingBatch<T>
    {
        int Count { get; }

        int Size { get; }

        void Add(T item);

        IReadOnlyList<T> Drain();
    }

    internal sealed class CopilotUiBatchBuffer<T>
    {
        private readonly Action<IReadOnlyList<T>> _applyBatch;
        private readonly Func<bool> _isOnTargetThread;
        private readonly int _maximumPendingItems;
        private readonly int _maximumPendingSize;
        private readonly ICopilotPendingBatch<T> _pending;
        private readonly bool _postOnTargetThread;
        private readonly object _syncRoot = new();
        private readonly SynchronizationContext? _targetContext;
        private Exception? _failure;
        private bool _completed;
        private bool _flushScheduled;

        public CopilotUiBatchBuffer(
            SynchronizationContext? targetContext,
            Action<IReadOnlyList<T>> applyBatch,
            ICopilotPendingBatch<T> pending,
            int maximumPendingSize,
            int maximumPendingItems,
            bool postOnTargetThread,
            Func<bool>? isOnTargetThread = null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumPendingSize, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumPendingItems, 1);

            _targetContext = targetContext;
            var targetThreadId = Environment.CurrentManagedThreadId;
            _isOnTargetThread = isOnTargetThread ?? (() => Environment.CurrentManagedThreadId == targetThreadId);
            _applyBatch = applyBatch ?? throw new ArgumentNullException(nameof(applyBatch));
            _pending = pending ?? throw new ArgumentNullException(nameof(pending));
            _maximumPendingSize = maximumPendingSize;
            _maximumPendingItems = maximumPendingItems;
            _postOnTargetThread = postOnTargetThread;
        }

        public void Enqueue(T item)
        {
            var isOnTargetThread = IsOnTargetThread();
            if (_targetContext == null || isOnTargetThread && !_postOnTargetThread)
            {
                FlushPending(captureFailure: false);
                ThrowIfUnavailable();
                _applyBatch([item]);
                return;
            }

            var shouldPost = false;
            var requiresBackpressure = false;
            lock (_syncRoot)
            {
                ThrowIfUnavailableNoLock();
                _pending.Add(item);
                if (!_flushScheduled)
                {
                    _flushScheduled = true;
                    shouldPost = true;
                }

                requiresBackpressure = _pending.Size >= _maximumPendingSize
                    || _pending.Count >= _maximumPendingItems;
            }

            if (shouldPost)
                _targetContext.Post(static state => ((CopilotUiBatchBuffer<T>)state!).FlushPending(captureFailure: true), this);
            if (!requiresBackpressure)
                return;

            if (isOnTargetThread)
            {
                FlushPending(captureFailure: false);
            }
            else
            {
                _targetContext.Send(static state => ((CopilotUiBatchBuffer<T>)state!).FlushPending(captureFailure: true), this);
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

        private void FlushPending(bool captureFailure)
        {
            IReadOnlyList<T> batch;
            lock (_syncRoot)
            {
                if (_pending.Count == 0)
                {
                    _flushScheduled = false;
                    return;
                }

                batch = _pending.Drain();
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
                    throw new InvalidOperationException("Applying buffered Copilot UI updates failed.", _failure);
            }
        }

        private void ThrowIfUnavailableNoLock()
        {
            if (_failure != null)
                throw new InvalidOperationException("Applying buffered Copilot UI updates failed.", _failure);
            if (_completed)
                throw new InvalidOperationException("The Copilot UI update buffer is already complete.");
        }

        private sealed record FlushRequest(CopilotUiBatchBuffer<T> Owner, TaskCompletionSource Completion);
    }
}
