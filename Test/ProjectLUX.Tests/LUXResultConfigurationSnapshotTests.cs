using ColorVision.UI;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using Xunit;

namespace ProjectLUX.Tests;

public sealed class LUXResultConfigurationSnapshotTests
{
    [Fact]
    public void RunningFlowKeepsSaveFlagsDelayAndPathFromGenerationA() => RunOnSta(() =>
    {
        var configA = new ViewResultManagerConfig
        {
            CsvSavePath = Path.Combine("root", "A"),
            SaveByDate = true,
            IsSaveImageReuslt = true,
            SaveImageReusltDelay = 321,
        };
        var configB = new ViewResultManagerConfig
        {
            CsvSavePath = Path.Combine("root", "B"),
            SaveByDate = false,
            IsSaveImageReuslt = false,
            SaveImageReusltDelay = 999,
        };
        ViewResultManagerConfig current = configA;
        using var owner = new RuntimeConfigOwner<ViewResultManagerConfig>(
            () => current,
            snapshotFactory: Clone);

        ViewResultManagerConfig runningFlow = owner.Capture();
        current = configB;
        Assert.True(owner.Reload());

        var result = new ProjectLUXReuslt { SN = "SN-1", Model = "MTF" };
        DateTime saveTime = new(2026, 8, 12, 10, 20, 30);
        string savePath = LUXWindow.BuildAutomaticImageSavePath(runningFlow, result, saveTime);

        Assert.True(runningFlow.IsSaveImageReuslt);
        Assert.Equal(321, runningFlow.SaveImageReusltDelay);
        Assert.Equal(Path.Combine("root", "A", "2026-08-12", "SN-1", "MTF.png"), savePath);
        Assert.False(owner.Current.IsSaveImageReuslt);
        Assert.Equal(999, owner.Current.SaveImageReusltDelay);
        Assert.Equal(Path.Combine("root", "B"), owner.Current.CsvSavePath);
    });

    private static ViewResultManagerConfig Clone(ViewResultManagerConfig config)
    {
        return new ViewResultManagerConfig
        {
            CsvSavePath = config.CsvSavePath,
            SaveByDate = config.SaveByDate,
            IsSaveImageReuslt = config.IsSaveImageReuslt,
            SaveImageReusltDelay = config.SaveImageReusltDelay,
        };
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        Assert.True(thread.TrySetApartmentState(ApartmentState.STA));
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA snapshot test did not finish.");

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
