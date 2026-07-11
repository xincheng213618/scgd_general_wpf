using System;
using System.Threading;

namespace ColorVision.Copilot
{
    public sealed class CopilotRequestSession : IDisposable
    {
        private CancellationTokenSource? _current;

        public bool IsActive => _current != null;

        public CancellationToken Begin()
        {
            Cancel();
            _current = new CancellationTokenSource();
            return _current.Token;
        }

        public CancellationToken GetRequiredToken()
        {
            return _current?.Token ?? throw new InvalidOperationException("Request context has not been initialized.");
        }

        public void Cancel()
        {
            var current = _current;
            _current = null;
            if (current == null)
                return;

            current.Cancel();
            current.Dispose();
        }

        public void Complete()
        {
            var current = _current;
            _current = null;
            current?.Dispose();
        }

        public void Dispose()
        {
            Cancel();
        }
    }
}
