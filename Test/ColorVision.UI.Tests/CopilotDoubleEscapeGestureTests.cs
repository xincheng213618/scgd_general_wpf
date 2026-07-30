using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotDoubleEscapeGestureTests
{
    [Fact]
    public void SecondEscapeWithinIntervalCompletesGesture()
    {
        var gesture = new CopilotDoubleEscapeGesture(TimeSpan.FromMilliseconds(500));
        var startedAt = new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

        Assert.False(gesture.Register(startedAt));
        Assert.True(gesture.Register(startedAt.AddMilliseconds(499)));
        Assert.False(gesture.Register(startedAt.AddMilliseconds(500)));
    }

    [Fact]
    public void ExpiredOrBackwardsTimestampStartsANewGesture()
    {
        var gesture = new CopilotDoubleEscapeGesture(TimeSpan.FromMilliseconds(500));
        var startedAt = new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

        Assert.False(gesture.Register(startedAt));
        Assert.False(gesture.Register(startedAt.AddMilliseconds(501)));
        Assert.False(gesture.Register(startedAt));
    }

    [Fact]
    public void ResetDiscardsTheArmedEscape()
    {
        var gesture = new CopilotDoubleEscapeGesture(TimeSpan.FromMilliseconds(500));
        var startedAt = new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

        Assert.False(gesture.Register(startedAt));
        gesture.Reset();

        Assert.False(gesture.Register(startedAt.AddMilliseconds(100)));
    }
}
