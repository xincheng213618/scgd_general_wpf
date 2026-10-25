using System.Threading;

namespace ColorVision.Copilot
{
    internal sealed class CopilotToolDispatchWindow
    {
        private const int Pending = 0;
        private const int Entered = 1;
        private const int Closed = 2;
        private int _state;

        public bool HasEntered => Volatile.Read(ref _state) == Entered;

        public bool TryEnter() => Interlocked.CompareExchange(ref _state, Entered, Pending) == Pending;

        public bool TryClose() => Interlocked.CompareExchange(ref _state, Closed, Pending) == Pending;
    }
}
