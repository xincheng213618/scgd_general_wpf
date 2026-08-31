using ColorVision.Recovery;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class StartupRecoveryWindowRuntimeTests
{
    [Fact]
    public void ConstructingRuntimeRecoveryDoesNotValidateOrChooseARestart()
    {
        WpfTestHost.Invoke(() =>
        {
            int validations = 0;
            using var window = new StartupRecoveryWindow(null, true, true, () => validations++);
            try
            {
                Assert.Equal(0, validations);
                Assert.Equal(StartupRecoveryAction.Exit, window.Result.Action);
                Assert.Equal(StartupMaintenanceText.Get("RuntimeRecoverySubtitle"), window.RecoverySubtitle);
                Assert.Equal(StartupMaintenanceText.Get("RuntimeRecoveryExplanation"), window.RecoveryExplanation);
                Assert.Equal(StartupMaintenanceText.Get("RuntimeNormalAction"), window.NormalActionText);
                Assert.Equal(StartupMaintenanceText.Get("RuntimeExitAction"), window.ExitActionText);
                Assert.Equal(StartupMaintenanceText.Get("RuntimeSafeStartAction"), window.RecommendedRecoveryActionText);
                Assert.Equal(StartupMaintenanceText.Get("RuntimeSkipAllAction"), window.SkipAllActionText);
                Assert.Equal(StartupMaintenanceText.Get("RuntimeSkipSelectedAction"), window.SkipSelectedActionText);
                Assert.Equal(StartupMaintenanceText.Get("RuntimeDisableSelectedAction"), window.DisableSelectedActionText);
                Assert.True(window.CanContinueStartup);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ClosingImmediatelyAfterLoadedCancelsBeforeScanningOrCheckingUpdates()
    {
        WpfTestHost.Invoke(() =>
        {
            using var window = new StartupRecoveryWindow(null, true, true);
            List<Exception> exceptions = [];
            DispatcherUnhandledExceptionEventHandler capture = (_, args) =>
            {
                exceptions.Add(args.Exception);
                args.Handled = true;
            };
            window.Dispatcher.UnhandledException += capture;
            try
            {
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                window.Close();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Empty(exceptions);
                Assert.Empty(window.Plugins);
                Assert.Empty(window.OperationStatusText);
                Assert.Equal(StartupRecoveryAction.Exit, window.Result.Action);
                Assert.Equal(Visibility.Collapsed, window.UpdateProgressVisibility);
            }
            finally
            {
                window.Dispatcher.UnhandledException -= capture;
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData("NormalActionText", StartupRecoveryAction.NormalStart)]
    [InlineData("ExitActionText", StartupRecoveryAction.Exit)]
    [InlineData("SkipAllActionText", StartupRecoveryAction.SkipAllOnce)]
    public void RuntimeButtonsOnlyReturnTheChosenResult(string binding, StartupRecoveryAction expected)
    {
        WpfTestHost.Invoke(() =>
        {
            int validations = 0;
            using var window = new StartupRecoveryWindow(null, true, true, () => validations++);
            int closed = 0;
            window.Closed += (_, _) => closed++;
            try
            {
                Click(FindButton(window, binding));
                Assert.Equal(expected, window.Result.Action);
                Assert.Empty(window.Result.SelectedPlugins);
                Assert.Equal(1, closed);
                Assert.Equal(0, validations); // Only the caller may confirm and perform a restart.
                Assert.False(Application.Current.Dispatcher.HasShutdownStarted);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void RuntimeSkipSelectedCarriesTheExactSelectionWithoutMutatingConfiguration()
    {
        WpfTestHost.Invoke(() =>
        {
            using var window = new StartupRecoveryWindow(null, true, true, () => throw new InvalidOperationException("No operation may run here."));
            StartupRecoveryPluginItem selected = CreatePlugin("selected", true);
            window.Plugins.Add(selected);
            window.Plugins.Add(CreatePlugin("untouched", false));
            try
            {
                Click(FindButton(window, nameof(StartupRecoveryWindow.SkipSelectedActionText)));
                Assert.Equal(StartupRecoveryAction.SkipSelectedOnce, window.Result.Action);
                Assert.Equal(selected.ToSelection(), Assert.Single(window.Result.SelectedPlugins));
                Assert.True(selected.IsEnabled);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void RuntimeWizardButtonReturnsTheWizardResultWithoutRestartingOrRunningInitializers()
    {
        WpfTestHost.Invoke(() =>
        {
            using var window = new StartupRecoveryWindow(null, true, true, () => throw new InvalidOperationException("No operation may run here."));
            try
            {
                Click(FindDescendants(window).OfType<Button>().Single(button => Equals(button.Content, "运行初始化向导")));
                Assert.Equal(StartupRecoveryAction.RunSetupWizard, window.Result.Action);
                Assert.Empty(window.Result.SelectedPlugins);
                Assert.False(Application.Current.Dispatcher.HasShutdownStarted);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void RuntimeDisablePersistsSelectionAndStaysOpenWithNextStartupFeedback()
    {
        WpfTestHost.Invoke(() =>
        {
            using var window = new StartupRecoveryWindow(null, true, true, () => { },
                _ => throw new InvalidOperationException("Disabling for next startup must not close documents."));
            StartupRecoveryPluginItem selected = CreatePlugin("selected", true);
            window.Plugins.Add(selected);
            window.Plugins.Add(CreatePlugin("untouched", false));
            int closed = 0;
            int saves = 0;
            window.Closed += (_, _) => closed++;
            try
            {
                Assert.True(window.DisableSelectedPlugins(items =>
                {
                    saves++;
                    Assert.Same(selected, Assert.Single(items));
                }));
                Assert.Equal(1, saves);
                Assert.Equal(0, closed);
                Assert.Equal(StartupRecoveryAction.Exit, window.Result.Action);
                Assert.Equal(StartupMaintenanceText.Get("RuntimePluginsDisabled"), window.OperationStatusText);
                Assert.True(window.CanContinueStartup);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void RuntimeDisableSaveFailureStaysOpenAndDoesNotChooseAStartupAction()
    {
        WpfTestHost.Invoke(() =>
        {
            using var window = new StartupRecoveryWindow(null, true, true, () => { });
            window.Plugins.Add(CreatePlugin("selected", true));
            int closed = 0;
            window.Closed += (_, _) => closed++;
            try
            {
                Assert.False(window.DisableSelectedPlugins(_ => throw new IOException("test save failure")));
                Assert.Equal(0, closed);
                Assert.Equal(StartupRecoveryAction.Exit, window.Result.Action);
                Assert.Contains("test save failure", window.OperationStatusText);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void PreStartupDisableKeepsItsExistingContinueStartupResult()
    {
        WpfTestHost.Invoke(() =>
        {
            using var window = new StartupRecoveryWindow(null, true);
            StartupRecoveryPluginItem selected = CreatePlugin("selected", true);
            window.Plugins.Add(selected);
            int closed = 0;
            window.Closed += (_, _) => closed++;
            try
            {
                Assert.True(window.DisableSelectedPlugins(_ => { }));
                Assert.Equal(1, closed);
                Assert.Equal(StartupRecoveryAction.DisableSelectedAndStart, window.Result.Action);
                Assert.Equal(selected.ToSelection(), Assert.Single(window.Result.SelectedPlugins));
                Assert.Equal("正常启动", window.NormalActionText);
                Assert.Equal("退出 ColorVision", window.ExitActionText);
                Assert.Equal("安全启动", window.RecommendedRecoveryActionText);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void BlockedRuntimeUpdateNeverInvokesTheUpdaterAndLeavesTheWindowUsable()
    {
        WpfTestHost.Invoke(() =>
        {
            using var window = new StartupRecoveryWindow(null, true, true, () => throw new InvalidOperationException("test running task"));
            bool started = false;
            try
            {
                window.StartApplicationUpdate(() => started = true);
                Assert.False(started);
                Assert.Equal("test running task", window.OperationStatusText);
                Assert.True(window.CanContinueStartup);
                Assert.Equal(StartupRecoveryAction.Exit, window.Result.Action);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void BlockedRuntimeFullRepairStopsBeforeItsConfirmationOrUpdater()
    {
        WpfTestHost.Invoke(() =>
        {
            using var window = new StartupRecoveryWindow(null, true, true, () => throw new InvalidOperationException("test running task"));
            try
            {
                Click(FindDescendants(window).OfType<Button>().Single(button => Equals(button.Content, "完整安装包修复")));
                Assert.Equal("test running task", window.OperationStatusText);
                Assert.True(window.CanContinueStartup);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void MissingRuntimeGuardFailsClosedButStartupRecoveryNeedsNoLiveApplicationGuard()
    {
        WpfTestHost.Invoke(() =>
        {
            using var runtime = new StartupRecoveryWindow(null, true, true);
            using var startup = new StartupRecoveryWindow(null, true, false, () => throw new InvalidOperationException("Startup must not call a live-app guard."));
            try
            {
                Assert.False(runtime.TryValidateRuntimeOperation());
                Assert.Equal(StartupMaintenanceText.Get("ApplicationUnavailable"), runtime.OperationStatusText);
                Assert.True(startup.TryValidateRuntimeOperation());
            }
            finally { runtime.Close(); startup.Close(); }
        });
    }

    [Fact]
    public void RuntimeUpdatePreparesDocumentsBeforeBecomingBusyOrStartingTheUpdater()
    {
        WpfTestHost.Invoke(() =>
        {
            List<string> calls = [];
            StartupRecoveryWindow? window = null;
            window = new StartupRecoveryWindow(null, true, true, () => calls.Add("validate"), dialog =>
            {
                Assert.Same(window, dialog);
                Assert.True(window!.CanContinueStartup);
                calls.Add("prepare");
            });
            try
            {
                window.StartApplicationUpdate(() =>
                {
                    Assert.False(window.CanContinueStartup);
                    calls.Add("update");
                });
                Assert.Equal(new[] { "validate", "prepare", "validate", "update" }, calls);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RuntimeDocumentCancellationOrSaveFailureStopsTheUpdater(bool cancelled)
    {
        WpfTestHost.Invoke(() =>
        {
            using var window = new StartupRecoveryWindow(null, true, true, () => { },
                _ =>
                {
                    if (cancelled) throw new OperationCanceledException("test save cancelled");
                    throw new IOException("test save failed");
                });
            bool started = false;
            try
            {
                window.StartApplicationUpdate(() => started = true);
                Assert.False(started);
                Assert.True(window.CanContinueStartup);
                Assert.Equal(cancelled ? "test save cancelled" : "test save failed", window.OperationStatusText);
                Assert.Equal(StartupRecoveryAction.Exit, window.Result.Action);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void RuntimeUpdateRequiresPreparationAndRevalidatesAfterPreparingDocuments()
    {
        WpfTestHost.Invoke(() =>
        {
            int validations = 0;
            using var missing = new StartupRecoveryWindow(null, true, true, () => { });
            using var changed = new StartupRecoveryWindow(null, true, true, () =>
            {
                if (++validations == 2) throw new InvalidOperationException("test state changed");
            }, _ => { });
            bool started = false;
            try
            {
                missing.StartApplicationUpdate(() => started = true);
                Assert.False(started);
                Assert.Equal(StartupMaintenanceText.Get("ApplicationUnavailable"), missing.OperationStatusText);
                changed.StartApplicationUpdate(() => started = true);
                Assert.False(started);
                Assert.Equal(2, validations);
                Assert.Equal("test state changed", changed.OperationStatusText);
                Assert.True(changed.CanContinueStartup);
            }
            finally { missing.Close(); changed.Close(); }
        });
    }

    [Fact]
    public void BlockedRuntimeDisableDoesNotPersistOrPrepareDocuments()
    {
        WpfTestHost.Invoke(() =>
        {
            using var window = new StartupRecoveryWindow(null, true, true,
                () => throw new InvalidOperationException("test permission changed"),
                _ => throw new InvalidOperationException("Must not prepare documents."));
            window.Plugins.Add(CreatePlugin("selected", true));
            bool saved = false;
            try
            {
                Assert.False(window.DisableSelectedPlugins(_ => saved = true));
                Assert.False(saved);
                Assert.Equal("test permission changed", window.OperationStatusText);
            }
            finally { window.Close(); }
        });
    }

    private static Button FindButton(StartupRecoveryWindow window, string binding)
        => FindDescendants(window).OfType<Button>().Single(button =>
            BindingOperations.GetBinding(button, ContentControl.ContentProperty)?.Path.Path == binding);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static IEnumerable<DependencyObject> FindDescendants(DependencyObject parent)
    {
        foreach (DependencyObject child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (DependencyObject descendant in FindDescendants(child)) yield return descendant;
        }
    }

    private static StartupRecoveryPluginItem CreatePlugin(string key, bool selected) => new()
    {
        PluginKey = key, PluginId = key, DirectoryName = key, DirectoryPath = @"C:\ColorVision\Plugins\" + key,
        DisplayName = key, VersionText = "1.0.0", IsEnabled = true, LastWriteTime = DateTime.MinValue,
        IsLegacy = false, HasInvalidManifest = false, IsSuspected = false, IsSelected = selected,
    };
}
