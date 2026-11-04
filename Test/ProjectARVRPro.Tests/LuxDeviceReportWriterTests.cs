using ProjectARVRPro.Exports.Lux;
using System.IO;
using System.Windows;

namespace ProjectARVRPro.Tests;

public sealed class LuxDeviceReportWriterTests
{
    // Protect the UI/lifecycle boundary: an unresponsive share must not freeze the
    // dispatcher, and the request must remain pending until its report is saved.
    [Fact]
    public async Task BlockedReportWriteLeavesDispatcherResponsiveAndRequestPending()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LuxReportTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "D_test.csv");
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? pending = null;
        try
        {
            ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => { });
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                pending = LuxDeviceReportWriter.WriteAsync(path, target =>
                {
                    Assert.False(Application.Current.Dispatcher.CheckAccess());
                    started.SetResult();
                    release.Wait();
                    File.WriteAllText(target, "measured result");
                }, CancellationToken.None);
            }).Task.WaitAsync(TimeSpan.FromSeconds(10));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Application.Current.Dispatcher.InvokeAsync(() => Assert.False(pending!.IsCompleted)).Task.WaitAsync(TimeSpan.FromSeconds(10));
            release.Set();
            await pending!;
            Assert.Equal("measured result", File.ReadAllText(path));
        }
        finally
        {
            release.Set();
            if (pending != null) await pending;
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task ReportFailurePropagatesAndCanceledRequestDoesNotWrite()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LuxReportTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "B_test.csv");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LuxDeviceReportWriter.WriteAsync(path,
            _ => throw new InvalidOperationException("Canceled writer ran"), cancellation.Token));
        Assert.False(Directory.Exists(directory));
        try
        {
            await Assert.ThrowsAsync<IOException>(() => LuxDeviceReportWriter.WriteAsync(path,
                _ => throw new IOException("Share unavailable"), CancellationToken.None));
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }
}
