using ColorVision.Engine.Services.Devices.Spectrum.Configs;

namespace Spectrum.Tests;

public sealed class SpectrumCalibrationGroupConfigTests
{
    [Fact]
    public void CurrentFiles_UpdateDefaultGroupWithoutCrossingIntoOtherGroups()
    {
        var config = new ConfigSpectrum
        {
            WavelengthFile = "default-wave.dat",
            MaguideFile = "default-magnitude.dat",
        };
        config.EnsureCalibrationGroups();
        SpectrumCalibrationGroup defaultGroup = config.ActiveCalibrationGroup;
        var otherGroup = new SpectrumCalibrationGroup
        {
            GroupName = "ND1",
            WavelengthFile = "nd1-wave.dat",
            MaguideFile = "nd1-magnitude.dat",
        };
        config.CalibrationGroups.Add(otherGroup);

        config.WavelengthFile = "updated-default-wave.dat";
        config.MaguideFile = "updated-default-magnitude.dat";

        Assert.Equal("updated-default-wave.dat", defaultGroup.WavelengthFile);
        Assert.Equal("updated-default-magnitude.dat", defaultGroup.MaguideFile);
        Assert.Equal("nd1-wave.dat", otherGroup.WavelengthFile);
        Assert.Equal("nd1-magnitude.dat", otherGroup.MaguideFile);

        config.ActiveCalibrationGroupName = otherGroup.GroupName;
        config.WavelengthFile = "updated-nd1-wave.dat";
        config.MaguideFile = "updated-nd1-magnitude.dat";

        Assert.Equal("updated-default-wave.dat", defaultGroup.WavelengthFile);
        Assert.Equal("updated-default-magnitude.dat", defaultGroup.MaguideFile);
        Assert.Equal("updated-nd1-wave.dat", otherGroup.WavelengthFile);
        Assert.Equal("updated-nd1-magnitude.dat", otherGroup.MaguideFile);
    }

    [Fact]
    public void SynchronizeActiveCalibrationGroupFiles_RepairsExistingMismatchFromCurrentFiles()
    {
        var config = new ConfigSpectrum
        {
            WavelengthFile = "current-wave.dat",
            MaguideFile = "current-magnitude.dat",
            CalibrationGroups =
            [
                new SpectrumCalibrationGroup
                {
                    GroupName = "Default",
                    WavelengthFile = "stale-wave.dat",
                    MaguideFile = "stale-magnitude.dat",
                },
            ],
        };

        config.SynchronizeActiveCalibrationGroupFiles();

        Assert.Equal("current-wave.dat", config.ActiveCalibrationGroup.WavelengthFile);
        Assert.Equal("current-magnitude.dat", config.ActiveCalibrationGroup.MaguideFile);
    }
}
