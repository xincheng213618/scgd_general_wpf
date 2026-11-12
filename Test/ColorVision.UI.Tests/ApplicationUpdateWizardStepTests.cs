using ColorVision.ServiceHost;
using ColorVision.UI.Desktop.Wizards;
using ColorVision.Update;
using ColorVision.Wizards;
using System.IO;

namespace ColorVision.UI.Tests;

public class ApplicationUpdateWizardStepTests
{
    private static AutoUpdatePlan Plan => AutoUpdater.BuildUpdatePlan(new Version(1, 4, 17, 1), new Version(1, 4, 17, 2))!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckAndNextNeverStartUpdatesOrChangeTheResumeMarker(bool updateAvailable)
    {
        var step = new ApplicationUpdateWizardStep(
            _ => Task.FromResult(new AutoUpdatePlanCheckResult(updateAvailable ? Plan : null, UpdateServerCheckStatus.Success)),
            () => throw new InvalidOperationException("Checking/skipping must not save configuration."),
            (_, _) => throw new InvalidOperationException("Checking/skipping must not start updates."));

        Assert.True(step.Order < new ColorVisionServiceHostWizardStep().Order);
        Assert.True(step.RunsBeforeInitializers);
        Assert.False(step.IsRequired);
        await step.RefreshAsync();
        Assert.True(step.CanContinue);
        Assert.False(step.HasError);
        Assert.True(await step.ApplyAsync());
        Assert.True(step.ConfigurationStatus);
    }

    [Theory]
    [InlineData((int)UpdateServerCheckStatus.NoInternetConnection)]
    [InlineData((int)UpdateServerCheckStatus.ServerUnavailable)]
    public async Task FailedCheckIgnoresCachedPlanAndAllowsNext(int status)
    {
        int checks = 0;
        var step = new ApplicationUpdateWizardStep(
            _ => { checks++; return Task.FromResult(new AutoUpdatePlanCheckResult(Plan, (UpdateServerCheckStatus)status)); },
            () => throw new InvalidOperationException("No confirmed plan."),
            (_, _) => throw new InvalidOperationException("No confirmed plan."));

        await step.RefreshAsync();
        Assert.True(step.HasError);
        Assert.True(step.CanContinue);
        await step.ExecuteAsync(); // Retry checks metadata rather than applying the cached plan.
        Assert.Equal(2, checks);
        Assert.True(await step.ApplyAsync());
        Assert.True(step.ConfigurationStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitUpdatePersistsUnfinishedWizardBeforeHandoffAndBlocksFurtherSetup(bool wasComplete)
    {
        var config = new WizardWindowConfig { WizardCompletionKey = wasComplete };
        List<string> calls = [];
        var step = new ApplicationUpdateWizardStep(
            _ => Task.FromResult(new AutoUpdatePlanCheckResult(Plan, UpdateServerCheckStatus.Success)),
            () => ApplicationUpdateWizardStep.PrepareWizardResume(config, () =>
            {
                Assert.False(config.WizardCompletionKey);
                calls.Add("save");
            }),
            (plan, _) =>
            {
                Assert.Equal(new Version(1, 4, 17, 2), plan.TargetVersion);
                Assert.False(config.WizardCompletionKey);
                calls.Add("start");
            });

        await step.RefreshAsync();
        await step.ExecuteAsync();
        Assert.Equal(new[] { "save", "start" }, calls);
        Assert.True(step.IsBusy);
        Assert.False(step.CanContinue);
        Assert.False(await step.ApplyAsync());
        await step.RefreshAsync();
        await step.ExecuteAsync();
        Assert.Equal(2, calls.Count);
        Assert.False(config.WizardCompletionKey);
    }

    [Fact]
    public async Task SaveFailurePreventsHandoffAndRestoresThePriorCompletionMarker()
    {
        var config = new WizardWindowConfig { WizardCompletionKey = true };
        var step = new ApplicationUpdateWizardStep(
            _ => Task.FromResult(new AutoUpdatePlanCheckResult(Plan, UpdateServerCheckStatus.Success)),
            () => ApplicationUpdateWizardStep.PrepareWizardResume(config, () => throw new IOException("Save failed.")),
            (_, _) => throw new InvalidOperationException("Must not start before persistence succeeds."));

        await step.RefreshAsync();
        await step.ExecuteAsync();
        Assert.True(config.WizardCompletionKey);
        Assert.Equal("Save failed.", step.ErrorMessage);
        Assert.True(step.CanContinue);
        Assert.True(await step.ApplyAsync());
    }

    [Fact]
    public async Task DownloadFailureAllowsRetryOrSkippingWithoutLosingWizardResume()
    {
        var config = new WizardWindowConfig { WizardCompletionKey = true };
        Action? failed = null;
        int starts = 0;
        var step = new ApplicationUpdateWizardStep(
            _ => Task.FromResult(new AutoUpdatePlanCheckResult(Plan, UpdateServerCheckStatus.Success)),
            () => ApplicationUpdateWizardStep.PrepareWizardResume(config, () => { }),
            (_, onFailure) => { starts++; failed = onFailure; });

        await step.RefreshAsync();
        await step.ExecuteAsync();
        failed!();
        Assert.True(step.HasError);
        Assert.True(step.CanContinue);
        Assert.False(config.WizardCompletionKey);
        await step.ExecuteAsync();
        Assert.Equal(2, starts);
        failed!();
        Assert.True(await step.ApplyAsync());
        Assert.True(step.ConfigurationStatus);
    }

    [Fact]
    public async Task CheckExceptionDoesNotPreventSkipping()
    {
        var step = new ApplicationUpdateWizardStep(
            _ => throw new IOException("Update service failed."),
            () => throw new InvalidOperationException("Must not save."),
            (_, _) => throw new InvalidOperationException("Must not update."));
        await step.RefreshAsync();
        Assert.Equal("Update service failed.", step.ErrorMessage);
        Assert.True(step.CanContinue);
        Assert.True(await step.ApplyAsync());
    }
}
