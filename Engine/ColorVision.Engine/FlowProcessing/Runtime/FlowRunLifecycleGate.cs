using System;
using System.Threading;

namespace ColorVision.Engine.FlowProcessing;

/// <summary>
/// Owns the mutable start/cancel/finish state for one UI execution session.
/// Keeping this state here prevents refresh and event handlers from each
/// implementing subtly different lifecycle checks.
/// </summary>
internal sealed class FlowRunLifecycleGate
{
    private readonly object _sync = new();
    private string? _serialNumber;
    private CancellationTokenSource? _cancellation;
    private bool _cancelRequested;

    public bool IsActive
    {
        get
        {
            lock (_sync)
                return _serialNumber != null;
        }
    }

    public bool TryBegin(
        string serialNumber,
        CancellationTokenSource cancellation,
        bool engineIsRunning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
        ArgumentNullException.ThrowIfNull(cancellation);

        lock (_sync)
        {
            if (_serialNumber != null || engineIsRunning)
                return false;

            _serialNumber = serialNumber;
            _cancellation = cancellation;
            _cancelRequested = false;
            return true;
        }
    }

    public bool IsActiveRun(string? serialNumber)
    {
        lock (_sync)
        {
            return !string.IsNullOrWhiteSpace(serialNumber)
                && string.Equals(
                    serialNumber,
                    _serialNumber,
                    StringComparison.Ordinal);
        }
    }

    public bool CanContinue(string serialNumber)
    {
        lock (_sync)
        {
            return string.Equals(
                    serialNumber,
                    _serialNumber,
                    StringComparison.Ordinal)
                && !_cancelRequested
                && _cancellation?.IsCancellationRequested != true;
        }
    }

    public CancellationTokenSource? RequestCancellation()
    {
        lock (_sync)
        {
            if (_serialNumber == null)
                return null;

            _cancelRequested = true;
            return _cancellation;
        }
    }

    public void DetachCancellationSource(
        string serialNumber,
        CancellationTokenSource cancellation)
    {
        lock (_sync)
        {
            if (string.Equals(
                    serialNumber,
                    _serialNumber,
                    StringComparison.Ordinal)
                && ReferenceEquals(_cancellation, cancellation))
            {
                _cancellation = null;
            }
        }
    }

    public void Complete(string serialNumber)
    {
        lock (_sync)
        {
            if (!string.Equals(
                    serialNumber,
                    _serialNumber,
                    StringComparison.Ordinal))
            {
                return;
            }

            _serialNumber = null;
            _cancellation = null;
            _cancelRequested = false;
        }
    }
}
