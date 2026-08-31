using ColorVision.Recovery;
using ColorVision.Update;
using System.IO;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class StartupRecoverySnapshotRuntimeTests
{
    [Fact]
    public void ConstructingASnapshotBrowserDoesNotPrepareOrValidateTheApplication()
    {
        WpfTestHost.Invoke(() =>
        {
            int operations = 0;
            var window = new ApplicationSnapshotsWindow
            {
                IsRunningApplication = true,
                ValidateRuntimeOperation = () => operations++,
                PrepareRuntimeOperation = _ => operations++,
            };
            try
            {
                Assert.Equal(0, operations);
                Assert.False(window.IsBusy);
                Assert.Empty(window.Snapshots);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ConfirmedRestorePreparesBeforeBusyAndUsesTheSameSelectedSnapshot()
    {
        WpfTestHost.Invoke(() =>
        {
            List<string> calls = [];
            var window = new ApplicationSnapshotsWindow { IsRunningApplication = true };
            window.ValidateRuntimeOperation = () => calls.Add("validate");
            window.PrepareRuntimeOperation = dialog =>
            {
                Assert.Same(window, dialog);
                Assert.False(window.IsBusy);
                calls.Add("prepare");
            };
            ApplicationSnapshotInfo selected = CreateSnapshot();
            try
            {
                Task operation = window.RestoreConfirmedSnapshotAsync(selected, snapshot =>
                {
                    Assert.Same(selected, snapshot);
                    Assert.True(window.IsBusy);
                    calls.Add("restore");
                    return Task.CompletedTask;
                });
                Assert.True(operation.IsCompletedSuccessfully);
                Assert.Equal(new[] { "validate", "prepare", "validate", "restore" }, calls);
                Assert.False(window.IsBusy);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelledOrFailedDocumentPreparationNeverRunsRestore(bool cancelled)
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new ApplicationSnapshotsWindow
            {
                IsRunningApplication = true,
                ValidateRuntimeOperation = () => { },
                PrepareRuntimeOperation = _ =>
                {
                    if (cancelled) throw new OperationCanceledException("test save cancelled");
                    throw new IOException("test save failed");
                },
            };
            bool restored = false;
            try
            {
                Task operation = window.RestoreConfirmedSnapshotAsync(CreateSnapshot(), _ =>
                {
                    restored = true;
                    return Task.CompletedTask;
                });
                Assert.True(operation.IsCompletedSuccessfully);
                Assert.False(restored);
                Assert.False(window.IsBusy);
                Assert.Equal(cancelled ? "test save cancelled" : "test save failed", window.StatusText);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RuntimeGuardBlocksRestoreBeforeOrAfterDocumentPreparation(int failAt)
    {
        WpfTestHost.Invoke(() =>
        {
            int validations = 0;
            int preparations = 0;
            var window = new ApplicationSnapshotsWindow
            {
                IsRunningApplication = true,
                ValidateRuntimeOperation = () =>
                {
                    if (++validations == failAt) throw new InvalidOperationException("test state changed");
                },
                PrepareRuntimeOperation = _ => preparations++,
            };
            bool restored = false;
            try
            {
                window.RestoreConfirmedSnapshotAsync(CreateSnapshot(), _ =>
                {
                    restored = true;
                    return Task.CompletedTask;
                }).GetAwaiter().GetResult();
                Assert.False(restored);
                Assert.False(window.IsBusy);
                Assert.Equal(failAt - 1, preparations);
                Assert.Equal("test state changed", window.StatusText);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RuntimeRestoreRequiresBothCallbacks(bool hasValidation)
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new ApplicationSnapshotsWindow
            {
                IsRunningApplication = true,
                ValidateRuntimeOperation = hasValidation ? () => { } : null,
                PrepareRuntimeOperation = hasValidation ? null : _ => { },
            };
            bool restored = false;
            try
            {
                window.RestoreConfirmedSnapshotAsync(CreateSnapshot(), _ =>
                {
                    restored = true;
                    return Task.CompletedTask;
                }).GetAwaiter().GetResult();
                Assert.False(restored);
                Assert.False(window.IsBusy);
                Assert.Equal(StartupMaintenanceText.Get("ApplicationUnavailable"), window.StatusText);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void PreStartupRestoreDoesNotRequireLiveDocumentPreparation()
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new ApplicationSnapshotsWindow
            {
                ValidateRuntimeOperation = () => throw new InvalidOperationException("No live application."),
                PrepareRuntimeOperation = _ => throw new InvalidOperationException("No live documents."),
            };
            bool restored = false;
            try
            {
                window.RestoreConfirmedSnapshotAsync(CreateSnapshot(), _ =>
                {
                    restored = true;
                    return Task.CompletedTask;
                }).GetAwaiter().GetResult();
                Assert.True(restored);
                Assert.False(window.IsBusy);
            }
            finally { window.Close(); }
        });
    }

    private static ApplicationSnapshotInfo CreateSnapshot() => new()
    {
        FilePath = @"C:\Snapshots\test.cvbackup", FileName = "test.cvbackup", Version = "1.0.0", VersionTarget = "",
        CreatedAt = DateTime.MinValue, SizeBytes = 0, IsDefault = false, IsUpdate = false, IsAutomatic = false,
    };
}
