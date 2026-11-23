using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Exports.Lux;
using System.IO;
using System.Windows;

namespace ProjectARVRPro.Tests;

[Collection(ProcessManagerPersistenceTestGroup.Name)]
public sealed class LuxDeviceReportWriterTests
{
    private static readonly LuxCsvWriteContext Context = new("test", 42, "H03", "Test", "31");

    [Fact]
    public async Task ReportEventsCorrelateTheDestinationWithTheRequestAndOutcome()
    {
        string directory = Directory.CreateTempSubdirectory("LuxReportLogging-").FullName;
        string path = Path.Combine(directory, "D_logged.csv");
        var logger = (Logger)LogManager.GetLogger(typeof(LuxCsvWriteContext).Assembly, typeof(LuxCsvWriteContext)).Logger;
        var hierarchy = (Hierarchy)logger.Repository;
        bool wasConfigured = hierarchy.Configured;
        Level previousLevel = logger.Level;
        var appender = new MemoryAppender();
        try
        {
            hierarchy.Configured = true;
            logger.Level = Level.Info;
            logger.AddAppender(appender);
            var context = new LuxCsvWriteContext("same-sn", 123, "H03", "HS", "31");
            await LuxDeviceReportWriter.WriteAsync(path, target => File.WriteAllText(target, "new"), context, CancellationToken.None);
            await Assert.ThrowsAsync<IOException>(() => LuxDeviceReportWriter.WriteAsync(path,
                _ => throw new IOException("Share unavailable"), context, CancellationToken.None));

            var events = appender.GetEvents().Select(item => (item.Level, Data: JObject.Parse(item.RenderedMessage))).ToArray();
            Assert.Collection(events,
                item => Assert.Equal((Level.Info, "Succeeded"), (item.Level, item.Data.Value<string>("Outcome"))),
                item => Assert.Equal((Level.Error, "Failed"), (item.Level, item.Data.Value<string>("Outcome"))));
            Assert.All(events, item =>
            {
                Assert.Equal("LuxCsvWrite", item.Data.Value<string>("Event"));
                Assert.Equal("same-sn", item.Data.Value<string>("SN"));
                Assert.Equal(123, item.Data.Value<int>("BatchId"));
                Assert.Equal("H03", item.Data.Value<string>("Station"));
                Assert.Equal("HS", item.Data.Value<string>("FlowGroup"));
                Assert.Equal(path, item.Data.Value<string>("Path"));
            });
        }
        finally
        {
            logger.RemoveAppender(appender);
            appender.Close();
            logger.Level = previousLevel;
            hierarchy.Configured = wasConfigured;
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

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
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "previous result");
            ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => { });
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                pending = LuxDeviceReportWriter.WriteAsync(path, target =>
                {
                    Assert.False(Application.Current.Dispatcher.CheckAccess());
                    File.WriteAllText(target, "partial");
                    started.SetResult();
                    release.Wait();
                    File.WriteAllText(target, "measured result");
                }, Context, CancellationToken.None);
            }).Task.WaitAsync(TimeSpan.FromSeconds(10));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Application.Current.Dispatcher.InvokeAsync(() => Assert.False(pending!.IsCompleted)).Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("previous result", File.ReadAllText(path));
            release.Set();
            await pending!;
            Assert.Equal("measured result", File.ReadAllText(path));
            Assert.Equal([path], Directory.GetFiles(directory));
        }
        finally
        {
            release.Set();
            if (pending != null) await pending;
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedOverwriteKeepsThePreviousReportAndRemovesTheStagingFile(bool cancel)
    {
        string directory = Directory.CreateTempSubdirectory("LuxReportTests-").FullName;
        string path = Path.Combine(directory, "D_test.csv");
        using var cancellation = new CancellationTokenSource();
        byte[] previous = [0xef, 0xbb, 0xbf, (byte)'1', (byte)'\r', (byte)'\n'];
        File.WriteAllBytes(path, previous);
        try
        {
            Task pending = LuxDeviceReportWriter.WriteAsync(path, target =>
            {
                File.WriteAllText(target, "partial");
                if (cancel)
                    cancellation.Cancel();
                else
                    throw new IOException("Connection lost during report write.");
            }, Context, cancellation.Token);

            if (cancel)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            else
                await Assert.ThrowsAsync<IOException>(() => pending);

            Assert.Equal(previous, File.ReadAllBytes(path));
            Assert.Equal([path], Directory.GetFiles(directory));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
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
            _ => throw new InvalidOperationException("Canceled writer ran"), Context, cancellation.Token));
        Assert.False(Directory.Exists(directory));
        try
        {
            await Assert.ThrowsAsync<IOException>(() => LuxDeviceReportWriter.WriteAsync(path,
                _ => throw new IOException("Share unavailable"), Context, CancellationToken.None));
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }
}
