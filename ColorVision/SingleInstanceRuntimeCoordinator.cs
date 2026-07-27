using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision
{
    internal sealed class SingleInstanceRuntimeCoordinator
    {
        private readonly Func<Task<int>> _closeOtherInstancesAsync;
        private readonly Func<bool> _tryAcquireMutex;
        private readonly Action _saveConfiguration;
        private int _isTransitionInProgress;

        public SingleInstanceRuntimeCoordinator(
            Func<Task<int>> closeOtherInstancesAsync,
            Func<bool> tryAcquireMutex,
            Action saveConfiguration)
        {
            ArgumentNullException.ThrowIfNull(closeOtherInstancesAsync);
            ArgumentNullException.ThrowIfNull(tryAcquireMutex);
            ArgumentNullException.ThrowIfNull(saveConfiguration);

            _closeOtherInstancesAsync = closeOtherInstancesAsync;
            _tryAcquireMutex = tryAcquireMutex;
            _saveConfiguration = saveConfiguration;
        }

        public async Task<int?> EnforceSingleInstanceAsync()
        {
            if (Interlocked.Exchange(ref _isTransitionInProgress, 1) != 0)
                return null;

            try
            {
                _saveConfiguration();
                int closedInstanceCount = await _closeOtherInstancesAsync();
                if (!_tryAcquireMutex())
                {
                    throw new InvalidOperationException(
                        "The current ColorVision instance could not acquire the single-instance mutex.");
                }

                _saveConfiguration();
                return closedInstanceCount;
            }
            finally
            {
                Volatile.Write(ref _isTransitionInProgress, 0);
            }
        }
    }
}
