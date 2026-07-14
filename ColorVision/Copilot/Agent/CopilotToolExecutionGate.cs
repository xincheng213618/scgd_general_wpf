#pragma warning disable CA1001
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    /// <summary>
    /// Allows bounded parallel reads while preserving a global write barrier and
    /// mutual exclusion for calls that resolve to the same resource key.
    /// </summary>
    internal sealed class CopilotToolExecutionGate
    {
        public const int DefaultMaximumConcurrentReads = 4;
        private const int ResourceStripeCount = 64;

        private readonly SemaphoreSlim _readCapacity;
        private readonly SemaphoreSlim _readerMutex = new(1, 1);
        private readonly SemaphoreSlim _roomEmpty = new(1, 1);
        private readonly SemaphoreSlim _turnstile = new(1, 1);
        private readonly SemaphoreSlim[] _resourceStripes;
        private int _activeReaders;

        public CopilotToolExecutionGate(int maximumConcurrentReads = DefaultMaximumConcurrentReads)
        {
            var capacity = Math.Max(1, maximumConcurrentReads);
            _readCapacity = new SemaphoreSlim(capacity, capacity);
            _resourceStripes = new SemaphoreSlim[ResourceStripeCount];
            for (var index = 0; index < _resourceStripes.Length; index++)
                _resourceStripes[index] = new SemaphoreSlim(1, 1);
        }

        public async ValueTask<IDisposable> AcquireAsync(CopilotToolConcurrencyMode mode, string concurrencyKey, CancellationToken cancellationToken)
        {
            return mode == CopilotToolConcurrencyMode.Exclusive
                ? await AcquireWriterAsync(cancellationToken)
                : await AcquireReaderAsync(concurrencyKey, cancellationToken);
        }

        private async ValueTask<IDisposable> AcquireReaderAsync(string concurrencyKey, CancellationToken cancellationToken)
        {
            var capacityHeld = false;
            var resourceHeld = false;
            var readerRegistered = false;
            var resource = GetResourceStripe(concurrencyKey);
            try
            {
                await resource.WaitAsync(cancellationToken);
                resourceHeld = true;
                await _readCapacity.WaitAsync(cancellationToken);
                capacityHeld = true;

                await _turnstile.WaitAsync(cancellationToken);
                _turnstile.Release();

                await _readerMutex.WaitAsync(cancellationToken);
                try
                {
                    if (_activeReaders == 0)
                        await _roomEmpty.WaitAsync(cancellationToken);
                    _activeReaders++;
                    readerRegistered = true;
                }
                finally
                {
                    _readerMutex.Release();
                }

                return new Lease(() => ReleaseReader(resource));
            }
            catch
            {
                if (readerRegistered)
                    ReleaseReader(resource);
                else
                {
                    if (resourceHeld)
                        resource.Release();
                    if (capacityHeld)
                        _readCapacity.Release();
                }
                throw;
            }
        }

        private async ValueTask<IDisposable> AcquireWriterAsync(CancellationToken cancellationToken)
        {
            var turnstileHeld = false;
            try
            {
                await _turnstile.WaitAsync(cancellationToken);
                turnstileHeld = true;
                await _roomEmpty.WaitAsync(cancellationToken);
                return new Lease(() =>
                {
                    _roomEmpty.Release();
                    _turnstile.Release();
                });
            }
            catch
            {
                if (turnstileHeld)
                    _turnstile.Release();
                throw;
            }
        }

        private void ReleaseReader(SemaphoreSlim resource)
        {
            _readerMutex.Wait();
            try
            {
                _activeReaders--;
                if (_activeReaders == 0)
                    _roomEmpty.Release();
            }
            finally
            {
                _readerMutex.Release();
                resource.Release();
                _readCapacity.Release();
            }
        }

        private SemaphoreSlim GetResourceStripe(string concurrencyKey)
        {
            var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(concurrencyKey ?? string.Empty) & int.MaxValue;
            return _resourceStripes[hash % _resourceStripes.Length];
        }

        private sealed class Lease(Action release) : IDisposable
        {
            private Action? _release = release;

            public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
        }
    }
}
