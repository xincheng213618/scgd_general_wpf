using ColorVision.Engine.Services.Devices.Spectrum.Calibration;

namespace Spectrum.Tests;

public sealed class SpectrumCalibrationGroupChangeGuardTests
{
    [Fact]
    public void ProgrammaticApply_DoesNotStartReverseUserSwitch()
    {
        var guard = new SpectrumCalibrationGroupChangeGuard();

        using (guard.EnterApply())
            Assert.False(guard.TryBeginUserSwitch());

        Assert.True(guard.TryBeginUserSwitch());
        guard.CompleteUserSwitch();
    }

    [Fact]
    public void PendingUserSwitch_RejectsRepeatedSelectionUntilCompleted()
    {
        var guard = new SpectrumCalibrationGroupChangeGuard();

        Assert.True(guard.TryBeginUserSwitch());
        Assert.False(guard.TryBeginUserSwitch());

        using (guard.EnterApply())
            Assert.False(guard.TryBeginUserSwitch());

        Assert.False(guard.TryBeginUserSwitch());
        guard.CompleteUserSwitch();
        Assert.True(guard.TryBeginUserSwitch());
    }
}
