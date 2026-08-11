using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes;
using ColorVision.UI;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
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

    [Fact]
    public void RunTemplateBarrierAllowsOnlyOneSessionBeforeFlowStarts() => RunOnSta(() =>
    {
        Application application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.ForceApplyTheme(Theme.Light);
        ConfigHandler.GetInstance("ProjectLUXTests");
        var configA = new ViewResultManagerConfig { CsvSavePath = "A", IsSaveImageReuslt = true };
        var configB = new ViewResultManagerConfig { CsvSavePath = "B", IsSaveImageReuslt = false };
        using var captured = new ManualResetEventSlim();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int captureCount = 0;
        LUXWindow.LUXFlowRunSession? sessionA = null;
        using var window = new LUXWindow();
        var template = new TemplateModel<FlowParam>("barrier", new FlowParam { Name = "barrier" });
        window.FlowTemplate.ItemsSource = new[] { template };
        window.FlowTemplate.SelectedIndex = 0;
        window.ResultConfigCaptureOverride = () => Interlocked.Increment(ref captureCount) == 1 ? configA : configB;
        window.RunTemplateCaptureBarrier = session =>
        {
            sessionA = session;
            captured.Set();
            return release.Task;
        };

        Task runA = window.RunTemplate();
        Assert.True(captured.Wait(TimeSpan.FromSeconds(5)));
        Task runB = window.RunTemplate();
        Assert.True(runB.Wait(TimeSpan.FromSeconds(5)));

        Assert.Equal(1, captureCount);
        Assert.Same(configA, sessionA!.ResultConfig);
        Assert.Equal("barrier", sessionA.FlowName);

        window.Dispose();
        release.TrySetResult();
        Assert.True(runA.Wait(TimeSpan.FromSeconds(5)));
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
