using System.Diagnostics;

namespace ProjectARVRPro;

internal sealed class ResultSqliteWriteTiming
{
    private long _lastStage = Stopwatch.GetTimestamp();

    public double? PrepareMs { get; internal set; }
    public double? GateWaitMs { get; internal set; }
    public double? BeginMs { get; internal set; }
    public double? RowMs { get; internal set; }
    public double? CompressionMs { get; internal set; }
    public double? PayloadMs { get; internal set; }
    public double? CommitMs { get; internal set; }
    public double? PublicationMs { get; internal set; }
    public bool Succeeded { get; internal set; }

    internal double FinishStage()
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = Stopwatch.GetElapsedTime(_lastStage, now).TotalMilliseconds;
        _lastStage = now;
        return Math.Round(elapsed, 3);
    }
}
