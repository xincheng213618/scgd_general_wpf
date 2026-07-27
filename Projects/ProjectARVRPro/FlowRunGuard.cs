namespace ProjectARVRPro;

public sealed class FlowRunGuard
{
    private readonly object _sync = new();
    private bool _isStartPending;
    private bool _isLifecycleActive;

    public bool IsBusy
    {
        get
        {
            lock (_sync)
                return _isStartPending || _isLifecycleActive;
        }
    }

    public bool TryBeginStart()
    {
        lock (_sync)
        {
            if (_isStartPending || _isLifecycleActive)
                return false;

            _isStartPending = true;
            return true;
        }
    }

    public void MarkStarted()
    {
        lock (_sync)
        {
            if (!_isStartPending)
                throw new InvalidOperationException("A flow start attempt is not active.");

            _isLifecycleActive = true;
        }
    }

    public void EndStartAttempt()
    {
        lock (_sync)
            _isStartPending = false;
    }

    public void Complete()
    {
        lock (_sync)
        {
            _isStartPending = false;
            _isLifecycleActive = false;
        }
    }
}
