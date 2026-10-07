namespace CameraTest.Application;

/// <summary>Counts displayed frames, not native camera callbacks or the configured refresh rate.</summary>
public sealed class VideoRunMetrics
{
    private TimeSpan _windowStart;
    private TimeSpan? _lastFrame;
    private int _frames;
    public double? DisplayFramesPerSecond { get; private set; }
    public double? Sharpness { get; private set; }
    public double? AnalysisMilliseconds { get; private set; }

    public void Reset()
    {
        _windowStart = TimeSpan.Zero;
        _lastFrame = null;
        _frames = 0;
        DisplayFramesPerSecond = Sharpness = AnalysisMilliseconds = null;
    }

    public void Presented(TimeSpan elapsed, double? sharpness, double? analysisMilliseconds)
    {
        _lastFrame = elapsed;
        _frames++;
        Sharpness = sharpness is { } value && double.IsFinite(value) ? value : null;
        AnalysisMilliseconds = analysisMilliseconds;
        if ((elapsed - _windowStart).TotalSeconds >= 1)
        {
            DisplayFramesPerSecond = _frames / (elapsed - _windowStart).TotalSeconds;
            _windowStart = elapsed;
            _frames = 0;
        }
    }

    public bool IsStalled(TimeSpan elapsed, TimeSpan timeout) => elapsed - (_lastFrame ?? TimeSpan.Zero) >= timeout;
}
