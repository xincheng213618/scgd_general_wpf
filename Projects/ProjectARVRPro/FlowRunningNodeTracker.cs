namespace ProjectARVRPro;

// Display state only: node callbacks may arrive concurrently on different threads.
internal sealed class FlowRunningNodeTracker
{
    private sealed record Invocation(string NodeId, string Title, string? MessageId);

    private readonly object _sync = new();
    private readonly List<Invocation> _running = [];
    private string? _serialNumber;
    private string _lastStartedNode = string.Empty;

    public void Reset(string? serialNumber)
    {
        lock (_sync)
        {
            _serialNumber = serialNumber;
            _running.Clear();
            _lastStartedNode = string.Empty;
        }
    }

    public string CompleteRun()
    {
        lock (_sync)
        {
            _serialNumber = null;
            _running.Clear();
            return _lastStartedNode;
        }
    }

    public bool NodeStarted(string? serialNumber, string nodeId, string title, string? messageId)
    {
        lock (_sync)
        {
            if (string.IsNullOrEmpty(_serialNumber) || !string.Equals(_serialNumber, serialNumber, StringComparison.Ordinal))
                return false;

            _running.Add(new(nodeId, title, messageId));
            _lastStartedNode = title;
            return true;
        }
    }

    public bool NodeEnded(string? serialNumber, string nodeId, string? messageId)
    {
        lock (_sync)
        {
            if (string.IsNullOrEmpty(_serialNumber) || !string.Equals(_serialNumber, serialNumber, StringComparison.Ordinal))
                return false;

            int index = _running.FindIndex(item => item.NodeId == nodeId
                && (string.IsNullOrEmpty(messageId) || string.Equals(item.MessageId, messageId, StringComparison.Ordinal)));
            // Legacy events may omit a request/response ID; match the oldest unpaired invocation.
            if (index < 0)
                index = _running.FindIndex(item => item.NodeId == nodeId && string.IsNullOrEmpty(item.MessageId));
            if (index < 0)
                return false;

            _running.RemoveAt(index);
            return true;
        }
    }

    public string GetRunningNodeNames()
    {
        lock (_sync)
        {
            // One label per node, even when several invocations of that node overlap.
            return string.Join(", ", _running.DistinctBy(item => item.NodeId).Select(item => item.Title));
        }
    }
}
