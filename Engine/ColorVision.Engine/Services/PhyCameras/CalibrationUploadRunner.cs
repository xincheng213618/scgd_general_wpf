using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.PhyCameras
{
    internal sealed class CalibrationUploadRunner
    {
        private int _isRunning;

        public bool IsRunning => Volatile.Read(ref _isRunning) != 0;

        public event EventHandler? RunningStateChanged;

        public async Task<bool> TryRunAsync(Func<Task> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            {
                return false;
            }

            try
            {
                RunningStateChanged?.Invoke(this, EventArgs.Empty);
                await operation();
                return true;
            }
            finally
            {
                Volatile.Write(ref _isRunning, 0);
                RunningStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
