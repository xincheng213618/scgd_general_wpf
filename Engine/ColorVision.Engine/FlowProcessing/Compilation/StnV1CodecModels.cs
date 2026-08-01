using System;

namespace ColorVision.Engine.FlowProcessing.Compilation;

public sealed class StnV1CodecOptions
{
    public const int DefaultMaximumNodeCount = 10_000;
    public const int DefaultMaximumConnectionCount = 100_000;

    public int MaximumNodeCount { get; init; } = DefaultMaximumNodeCount;

    public int MaximumConnectionCount { get; init; } =
        DefaultMaximumConnectionCount;
}

public enum FlowCompilationError
{
    InvalidCanvas,
    UnknownNodeType,
    NodeLimitExceeded,
    ConnectionLimitExceeded,
}

public sealed class FlowCompilationException : Exception
{
    public FlowCompilationException(
        FlowCompilationError error,
        string message)
        : base(message)
    {
        Error = error;
    }

    public FlowCompilationException(
        FlowCompilationError error,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Error = error;
    }

    public FlowCompilationError Error { get; }
}
