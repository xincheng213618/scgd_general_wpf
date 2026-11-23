using ProjectARVRPro.Process;
using System.IO;

namespace ProjectARVRPro.Services;

// A write can finish on a Socket worker while the window is already preparing another run.
internal sealed class SwitchPgAcknowledgement
{
    internal sealed record Request(Stream Stream, int StepIndex, ProcessMeta Process, string FlowTemplate);

    private readonly object _gate = new();
    private Request? _request;
    private DateTime? _sentAt;

    internal Request Prepare(Stream stream, int stepIndex, ProcessMeta process)
    {
        lock (_gate)
        {
            _sentAt = null;
            return _request = new(stream, stepIndex, process, process.FlowTemplate);
        }
    }

    internal void MarkSent(Request request, DateTime sentAt)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_request, request))
                _sentAt ??= sentAt;
        }
    }

    internal bool IsAwaiting(Stream? activeStream)
    {
        lock (_gate)
            return _sentAt.HasValue && ReferenceEquals(_request?.Stream, activeStream);
    }

    internal DateTime? TryConsume(Stream stream, Stream? activeStream, int nextStepIndex, ProcessMeta process)
    {
        lock (_gate)
        {
            if (!_sentAt.HasValue || _request == null || !ReferenceEquals(stream, _request.Stream)
                || !ReferenceEquals(stream, activeStream) || _request.StepIndex != nextStepIndex
                || !ReferenceEquals(_request.Process, process)
                || !string.Equals(_request.FlowTemplate, process.FlowTemplate, StringComparison.OrdinalIgnoreCase))
                return null;

            DateTime sentAt = _sentAt.Value;
            _request = null;
            _sentAt = null;
            return sentAt;
        }
    }

    internal void Clear()
    {
        lock (_gate)
        {
            _request = null;
            _sentAt = null;
        }
    }
}
