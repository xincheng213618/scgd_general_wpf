using ColorVision.Recovery;
using ColorVision.UI.Shell;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class StartupMaintenanceLifecycleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SetupWizard")]
    [InlineData("safe")]
    [InlineData("--recovery")]
    public void UnknownModeDoesNotSelectMaintenance(string? value)
        => Assert.Null(StartupMaintenanceController.ParseMode(value));

    [Theory]
    [InlineData("setup", true)]
    [InlineData("SETUP", true)]
    [InlineData("recovery", false)]
    [InlineData("RECOVERY", false)]
    public void ExplicitModeSelectsOnlyTheRequestedBranch(string value, bool wizard)
        => Assert.Equal(wizard ? StartupMaintenanceMode.SetupWizard : StartupMaintenanceMode.Recovery,
            StartupMaintenanceController.ParseMode(value));

    [Theory]
    [InlineData("safe-start", true)]
    [InlineData("SAFE-START", true)]
    [InlineData("skip-plugins", false)]
    public void PluginRestartModeIsExplicit(string value, bool allPlugins)
        => Assert.Equal(allPlugins ? StartupMaintenanceMode.SafeStart : StartupMaintenanceMode.SkipSelectedPlugins,
            StartupMaintenanceController.ParseMode(value));

    [Fact]
    public void SelectedPluginRestartUsesOneJsonArgumentAndRoundTripsExactKeys()
    {
        string[] keys = ["插件 A", "plugin-with-space and \"quote\"", "legacy\\name"];
        ProcessStartInfo start = StartupMaintenanceController.CreateRestartStartInfo(
            @"C:\Program Files\ColorVision\ColorVision.exe", 1234, StartupMaintenanceMode.SkipSelectedPlugins, keys);
        Assert.Equal("--startup-skip-plugins", start.ArgumentList[^2]);
        var parser = new ArgumentParser();
        parser.AddArgument(StartupMaintenanceController.ArgumentName);
        parser.AddArgument(StartupMaintenanceController.SkipPluginsArgumentName);
        var parsed = parser.ParseSnapshot(start.ArgumentList.Skip(3).ToArray());
        Assert.Equal("skip-plugins", parsed.Values[StartupMaintenanceController.ArgumentName]);
        Assert.Equal(keys, StartupMaintenanceController.ParseSkippedPluginKeys(parsed.Values[StartupMaintenanceController.SkipPluginsArgumentName]));
        Assert.Empty(parsed.PositionalArguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("[null,\"\"]")]
    public void MissingOrInvalidPluginSelectionIsEmptyForRecoveryFallback(string? value)
        => Assert.Empty(StartupMaintenanceController.ParseSkippedPluginKeys(value));

    [Fact]
    public void SkippedPluginKeysAreDeduplicatedWithoutChangingTheFirstKey()
        => Assert.Equal(new[] { "PluginA" }, StartupMaintenanceController.ParseSkippedPluginKeys("[\"PluginA\",\"plugina\"]"));

    [Theory]
    [InlineData("safe-start")]
    [InlineData("skip-plugins")]
    public void PluginRestartDoesNotBypassRealStartupFailure(string value)
    {
        StartupMaintenanceMode? mode = StartupMaintenanceController.ParseMode(value);
        Assert.True(StartupMaintenanceController.ShouldShowRecovery(mode, false));
        Assert.False(StartupMaintenanceController.ShouldShowRecovery(mode, true));
        Assert.False(StartupMaintenanceController.ShouldCompleteCancelledRecovery(mode, false));
    }

    [Fact]
    public void SelectedPluginRestartWithoutASelectionCannotLaunch()
        => Assert.Throws<ArgumentException>(() => StartupMaintenanceController.CreateRestartStartInfo(
            @"C:\Program Files\ColorVision\ColorVision.exe", 1234, StartupMaintenanceMode.SkipSelectedPlugins));

    [Theory]
    [InlineData(null, true, false, false)]
    [InlineData(null, false, true, false)]
    [InlineData("setup", true, false, false)]
    [InlineData("setup", false, true, false)]
    [InlineData("recovery", true, true, true)]
    [InlineData("recovery", false, true, false)]
    public void RecoveryPolicyPreservesRealFailureAndOnlyCompletesHealthyManualCancellation(
        string? value, bool healthy, bool showRecovery, bool completeCancellation)
    {
        var mode = StartupMaintenanceController.ParseMode(value);
        Assert.Equal(showRecovery, StartupMaintenanceController.ShouldShowRecovery(mode, healthy));
        Assert.Equal(completeCancellation, StartupMaintenanceController.ShouldCompleteCancelledRecovery(mode, healthy));
    }

    [Theory]
    [InlineData(true, "setup")]
    [InlineData(false, "recovery")]
    public void RestartArgumentsAreExplicitAndDoNotReplayTheOldDocumentOrChangeConfiguration(bool wizard, string value)
    {
        ProcessStartInfo start = StartupMaintenanceController.CreateRestartStartInfo(
            @"C:\Program Files\ColorVision\ColorVision.exe", 1234,
            wizard ? StartupMaintenanceMode.SetupWizard : StartupMaintenanceMode.Recovery);
        Assert.False(start.UseShellExecute);
        Assert.Equal(@"C:\Program Files\ColorVision", start.WorkingDirectory);
        Assert.Equal(new[] { "-r", "--wait-for-process", "1234", "--startup-maintenance", value }, start.ArgumentList);
        var parser = new ArgumentParser();
        parser.AddArgument(StartupMaintenanceController.ArgumentName);
        // EntryClass strips the process-wait handoff before ordinary application argument parsing.
        var parsed = parser.ParseSnapshot(["--startup-maintenance", value]);
        Assert.Equal(value, parsed.Values[StartupMaintenanceController.ArgumentName]);
        Assert.False(parsed.Values.ContainsKey("input"));
        Assert.Empty(parsed.PositionalArguments);
    }

    [Theory]
    [InlineData("ColorVision.exe")]
    [InlineData(@"C:\Tools\other.exe")]
    public void RestartRejectsUnrelatedOrRelativeExecutables(string path)
        => Assert.Throws<InvalidOperationException>(() => StartupMaintenanceController.CreateRestartStartInfo(path, 12, StartupMaintenanceMode.Recovery));

    [Fact]
    public void ConfirmationCancellationNeverClosesWindowsOrStartsAProcess()
    {
        List<string> calls = [];
        Assert.False(StartupMaintenanceController.RunRequest(() => calls.Add("validate"), () => false,
            () => throw new InvalidOperationException("Restart must not run.")));
        Assert.Equal(new[] { "validate" }, calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BusyOrPermissionChangeBeforeOrDuringConfirmationPreventsRestart(int failAt)
    {
        int validations = 0;
        bool restarted = false;
        Assert.Throws<InvalidOperationException>(() => StartupMaintenanceController.RunRequest(
            () => { if (++validations == failAt) throw new InvalidOperationException("Blocked."); },
            () => true, () => restarted = true));
        Assert.False(restarted);
        Assert.Equal(failAt, validations);
    }

    [Fact]
    public void AcceptedRequestValidatesTwiceThenUsesNormalCloseDecision()
    {
        List<string> calls = [];
        Assert.False(StartupMaintenanceController.RunRequest(() => calls.Add("validate"),
            () => { calls.Add("confirm"); return true; }, () => { calls.Add("close-cancelled"); return false; }));
        Assert.Equal(new[] { "validate", "confirm", "validate", "close-cancelled" }, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedCloseAlwaysEndsTheOldInstanceEvenIfTheReplacementCannotStart(bool failStart)
    {
        List<string> calls = [];
        StartupMaintenanceController.FinishAcceptedClose(
            () => { calls.Add("start"); if (failStart) throw new InvalidOperationException("Cannot launch."); },
            exception => { Assert.Equal("Cannot launch.", exception.Message); calls.Add("report"); },
            () => calls.Add("shutdown"));
        Assert.Equal(failStart ? new[] { "start", "report", "shutdown" } : new[] { "start", "shutdown" }, calls);
    }

    [Fact]
    public void CancelledChildCloseDoesNotCloseTheMainWindowOrSaveAfterCancellation() => OnSta(() =>
    {
        var child = new Window();
        var main = new Window();
        int saved = 0;
        bool mainClosed = false;
        CancelEventHandler cancel = (_, args) => args.Cancel = true;
        child.Closing += cancel;
        main.Closed += (_, _) => mainClosed = true;
        try
        {
            Assert.False(App.TryCloseMaintenanceWindows([child, main], () => saved++));
            Assert.False(mainClosed);
            Assert.Equal(0, saved);
        }
        finally { child.Closing -= cancel; child.Close(); main.Close(); }
    });

    [Fact]
    public void SaveFailureCancelsCloseAndDoesNotAdvanceToOtherWindows() => OnSta(() =>
    {
        var child = new Window();
        var main = new Window();
        bool childClosed = false;
        bool mainClosed = false;
        child.Closed += (_, _) => childClosed = true;
        main.Closed += (_, _) => mainClosed = true;
        try
        {
            Assert.Throws<IOException>(() => App.TryCloseMaintenanceWindows([child, main], () => throw new IOException("Save failed.")));
            Assert.False(childClosed);
            Assert.False(mainClosed);
        }
        finally { child.Close(); main.Close(); }
    });

    [Fact]
    public void EveryWindowGetsItsOwnClosingAndSaveHooksBeforeTheNextWindow() => OnSta(() =>
    {
        List<string> calls = [];
        var child = new Window();
        var main = new Window();
        child.Closing += (_, _) => calls.Add("child-closing");
        child.Closed += (_, _) => calls.Add("child-closed");
        main.Closing += (_, _) => calls.Add("main-closing");
        main.Closed += (_, _) => calls.Add("main-closed");
        Assert.True(App.TryCloseMaintenanceWindows([child, main], () => calls.Add("save")));
        Assert.Equal(new[] { "child-closing", "save", "child-closed", "main-closing", "save", "main-closed" }, calls);
    });

    [Fact]
    public void ClosedHandlerFailureDoesNotAdvanceOrLaunchAndEndsAnAlreadyWindowlessInstance() => OnSta(() =>
    {
        var window = new Window();
        bool actuallyClosed = false;
        bool launched = false;
        List<string> calls = [];
        window.Closed += (_, _) => { actuallyClosed = true; throw new IOException("Close cleanup failed."); };
        var exception = Assert.Throws<IOException>(() =>
        {
            App.TryCloseMaintenanceWindows([window], () => { });
            launched = true;
        });
        StartupMaintenanceController.ReportFailure(exception,
            _ => calls.Add("report"), () => !actuallyClosed, () => calls.Add("shutdown"));
        Assert.False(launched);
        Assert.Equal(new[] { "report", "shutdown" }, calls);
    });

    [Fact]
    public void CloseFailureWithRemainingWindowsReportsWithoutShuttingDown()
    {
        List<string> calls = [];
        StartupMaintenanceController.ReportFailure(new IOException(),
            _ => calls.Add("report"), () => true, () => calls.Add("shutdown"));
        Assert.Equal(new[] { "report" }, calls);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void OnlyCommittedMaintenanceExitSuppressesExitTimePrefetchedUpdate(bool maintenanceExit, bool eligible)
        => Assert.Equal(eligible, App.ShouldApplyPrefetchedUpdateOnExit(maintenanceExit));

    [Fact]
    public void FailedLaunchCannotApplyAnUpdateThatBecomesReadyDuringTheFailureMessage()
    {
        bool updateReady = false;
        bool updateApplied = false;
        StartupMaintenanceController.FinishAcceptedClose(
            () => throw new IOException("Launch failed."),
            _ => updateReady = true,
            () =>
            {
                bool maintenanceExitRequested = true;
                ApplicationExitCleanup.RunSocketBeforePrefetchedUpdate(false,
                    () => new ApplicationExitHandoffState(UpdateIsActive: false, ReplacementIsActive: false),
                    () => true,
                    () => { if (App.ShouldApplyPrefetchedUpdateOnExit(maintenanceExitRequested)) updateApplied = updateReady; },
                    (_, _) => throw new InvalidOperationException("Unexpected cleanup failure."));
            });
        Assert.True(updateReady);
        Assert.False(updateApplied);
    }

    [Fact]
    public void CancelledCloseWithWindowsRemainingDoesNotSuppressLaterOrdinaryExitUpdate()
    {
        bool maintenanceExitRequested = false;
        StartupMaintenanceController.ReportFailure(new IOException("Save failed."),
            _ => { }, () => true, () => maintenanceExitRequested = true);
        Assert.True(App.ShouldApplyPrefetchedUpdateOnExit(maintenanceExitRequested));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecoveryPreparationPreservesTheOperationAndItsOwnerChainWhileOtherWindowsKeepTheirCloseDecision(bool cancelOther)
    {
        WpfTestHost.Invoke(() =>
        {
            Application app = Application.Current;
            Window? previousMain = app.MainWindow;
            var main = CreateInvisibleWindow();
            var recovery = CreateInvisibleWindow();
            var snapshot = CreateInvisibleWindow();
            var otherEditor = CreateInvisibleWindow();
            var sibling = CreateInvisibleWindow();
            var closed = new HashSet<Window>();
            Window[] windows = [main, recovery, snapshot, otherEditor, sibling];
            foreach (Window window in windows) window.Closed += (_, _) => closed.Add(window);
            CancelEventHandler cancel = (_, args) => args.Cancel = cancelOther;
            try
            {
                main.Show();
                recovery.Owner = main;
                recovery.Show();
                snapshot.Owner = recovery;
                snapshot.Show();
                otherEditor.Show();
                sibling.Owner = main;
                sibling.Show();
                otherEditor.Closing += cancel;

                Assert.False(App.ShouldCloseBeforeRecovery(main, main, snapshot));
                Assert.False(App.ShouldCloseBeforeRecovery(recovery, main, snapshot));
                Assert.False(App.ShouldCloseBeforeRecovery(snapshot, main, snapshot));
                Assert.True(App.ShouldCloseBeforeRecovery(otherEditor, main, snapshot));
                Assert.True(App.ShouldCloseBeforeRecovery(sibling, main, snapshot));
                Assert.False(App.ShouldCloseBeforeRecovery(recovery, main, recovery));
                Assert.True(App.ShouldCloseBeforeRecovery(snapshot, main, recovery));

                Window[] closing = windows.Where(window => App.ShouldCloseBeforeRecovery(window, main, snapshot)).ToArray();
                Assert.Equal(!cancelOther, App.TryCloseMaintenanceWindows(closing, () => { }));
                Assert.DoesNotContain(main, closed);
                Assert.DoesNotContain(recovery, closed);
                Assert.DoesNotContain(snapshot, closed);
                Assert.Equal(!cancelOther, closed.Contains(otherEditor));
                Assert.Equal(!cancelOther, closed.Contains(sibling));
                Assert.False(app.Dispatcher.HasShutdownStarted);
            }
            finally
            {
                otherEditor.Closing -= cancel;
                foreach (Window window in windows.Reverse()) if (!closed.Contains(window)) window.Close();
                app.MainWindow = previousMain;
            }
        });
    }

    private static Window CreateInvisibleWindow() => new()
    {
        Width = 80, Height = 80, Left = -10000, Top = -10000,
        ShowActivated = false, ShowInTaskbar = false, Opacity = 0,
        WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.Manual,
    };

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Isolated window lifecycle did not complete.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
