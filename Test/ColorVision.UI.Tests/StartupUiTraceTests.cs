using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class StartupUiTraceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("true")]
    public void DisabledTraceDoesNotBecomeCurrentOrWriteAReport(string? enabled)
    {
        using var environment = new TraceEnvironment(enabled);
        WpfTestHost.Invoke(() =>
        {
            Assert.Null(StartupUiTrace.Current);
            Assert.Null(StartupUiTrace.Start(Dispatcher.CurrentDispatcher));
            Assert.Null(StartupUiTrace.Current);
        });
        Assert.False(File.Exists(environment.OutputPath));
    }

    [Fact]
    public void RealContentRenderedCapturesInputOperationWritesJsonAndReleasesHooksWhileWindowStaysOpen()
    {
        using var environment = new TraceEnvironment("1");
        (Window window, WeakReference traceReference) = WpfTestHost.Invoke(CreateCapturedWindow);
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(environment.OutputPath));
            JsonElement report = document.RootElement;
            JsonElement markers = report.GetProperty("Markers");
            Assert.True(markers.GetProperty("FactoryReturnedMs").GetDouble() >= 0);
            Assert.True(markers.GetProperty("LoadedMs").GetDouble() >= 0);
            Assert.True(markers.GetProperty("ContentRenderedMs").GetDouble() >= markers.GetProperty("ShowReturnedMs").GetDouble());
            JsonElement current = report.GetProperty("ContentRenderedOperation");
            Assert.Equal("Input", current.GetProperty("Priority").GetString());
            Assert.Equal(current.GetProperty("Id").GetInt32(), report.GetProperty("ContentRenderedOperationId").GetInt32());
            Assert.True(current.GetProperty("ActiveAtCapture").GetBoolean());
            Assert.Equal(JsonValueKind.Null, current.GetProperty("CompletedMs").ValueKind);
            Assert.True(current.GetProperty("ExecutionMs").GetDouble() >= 0);
            Assert.Contains(report.GetProperty("Priorities").EnumerateArray(), priority => priority.GetProperty("Aborted").GetInt32() > 0);
            JsonElement coverage = report.GetProperty("TailCoverage");
            Assert.Equal(coverage.GetProperty("DurationMs").GetDouble(),
                coverage.GetProperty("CoveredExecutionMs").GetDouble() + coverage.GetProperty("UncoveredMs").GetDouble(), 6);

            AssertCollected(traceReference);
        }
        finally
        {
            WpfTestHost.Invoke(window.Close);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbortOrWindowClosedClearsCurrentAndReleasesHooks(bool closeWindow)
    {
        using var environment = new TraceEnvironment("1");
        WeakReference traceReference = WpfTestHost.Invoke(() => CreateStoppedTrace(closeWindow));
        AssertCollected(traceReference);
        Assert.False(File.Exists(environment.OutputPath));
    }

    [Fact]
    public void ReportWriteFailureDoesNotEscapeAfterCaptureCleanup()
    {
        using var environment = new TraceEnvironment("1");
        Environment.SetEnvironmentVariable("COLORVISION_STARTUP_TRACE_FILE", environment.DirectoryPath);
        WpfTestHost.Invoke(() =>
        {
            StartupUiTrace trace = Assert.IsType<StartupUiTrace>(StartupUiTrace.Start(Dispatcher.CurrentDispatcher));
            trace.CaptureAndStop();
            Assert.Null(StartupUiTrace.Current);
            Assert.Null(Record.Exception(trace.WriteReport));
            trace.Abort();
        });
        Assert.True(Directory.Exists(environment.DirectoryPath));
    }

    [Fact]
    public void PostedDeliveredAfterStartedProducesUnknownQueueForTheActualCurrentOperation()
    {
        using var environment = new TraceEnvironment("1");
        WpfTestHost.Invoke(CaptureOperationWithLatePostedNotification);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(environment.OutputPath));
        JsonElement current = document.RootElement.GetProperty("ContentRenderedOperation");
        Assert.Equal("Input", current.GetProperty("Priority").GetString());
        Assert.True(current.GetProperty("PostedMs").GetDouble() > current.GetProperty("StartedMs").GetDouble());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("QueueMs").ValueKind);
        Assert.True(current.GetProperty("ActiveAtCapture").GetBoolean());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("CompletedMs").ValueKind);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Window Window, WeakReference Trace) CreateCapturedWindow()
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        Window? previousMainWindow = Application.Current.MainWindow;
        StartupUiTrace trace = Assert.IsType<StartupUiTrace>(StartupUiTrace.Start(dispatcher));
        var window = CreateWindow();
        bool rendered = false;
        bool abortedCallbackRan = false;
        Exception? captureFailure = null;
        var frame = new DispatcherFrame();
        EventHandler onRendered = (_, _) =>
        {
            try
            {
                trace.CaptureAndStop();
                trace.WriteReport();
                rendered = true;
            }
            catch (Exception ex)
            {
                captureFailure = ex;
            }
            finally
            {
                frame.Continue = false;
            }
        };
        try
        {
            trace.Observe(window);
            window.ContentRendered += onRendered;
            DispatcherOperation aborted = dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(() => abortedCallbackRan = true));
            Assert.True(aborted.Abort());
            window.Show();
            trace.MarkShowReturned();
            RunFrameWithTimeout(frame);
            Assert.Null(captureFailure);
            Assert.True(rendered, "The real WPF ContentRendered event must arrive before the timeout.");
            Assert.False(abortedCallbackRan);
            Assert.Null(StartupUiTrace.Current);
            return (window, new WeakReference(trace));
        }
        catch
        {
            trace.Abort();
            window.Close();
            throw;
        }
        finally
        {
            window.ContentRendered -= onRendered;
            Application.Current.MainWindow = previousMainWindow;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateStoppedTrace(bool closeWindow)
    {
        Window? previousMainWindow = Application.Current.MainWindow;
        StartupUiTrace trace = Assert.IsType<StartupUiTrace>(StartupUiTrace.Start(Dispatcher.CurrentDispatcher));
        var window = CreateWindow();
        try
        {
            trace.Observe(window);
            if (closeWindow)
            {
                window.Show();
                window.Close();
            }
            else
            {
                trace.Abort();
            }
            Assert.Null(StartupUiTrace.Current);
            trace.WriteReport();
            return new WeakReference(trace);
        }
        finally
        {
            StartupUiTrace.Current?.Abort();
            window.Close();
            Application.Current.MainWindow = previousMainWindow;
        }
    }

    private static void CaptureOperationWithLatePostedNotification()
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        using var releasePosted = new ManualResetEventSlim();
        using var postedReturned = new ManualResetEventSlim();
        int postingThreadId = 0;
        DispatcherOperation? operation = null;
        Exception? callbackFailure = null;
        var frame = new DispatcherFrame();
        // WPF raises Posted on the posting thread after making the operation available.
        // An earlier subscriber holds that notification until its actual Input callback starts.
        DispatcherHookEventHandler delayPosted = (_, args) =>
        {
            if (Environment.CurrentManagedThreadId != Volatile.Read(ref postingThreadId) || args.Operation.Priority != DispatcherPriority.Input)
                return;
            operation = args.Operation;
            releasePosted.Wait(TimeSpan.FromSeconds(5));
        };
        dispatcher.Hooks.OperationPosted += delayPosted;
        StartupUiTrace? trace = null;
        Task? postingTask = null;
        try
        {
            trace = Assert.IsType<StartupUiTrace>(StartupUiTrace.Start(dispatcher));
            postingTask = Task.Run(() =>
            {
                Volatile.Write(ref postingThreadId, Environment.CurrentManagedThreadId);
                dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    try
                    {
                        releasePosted.Set();
                        if (!postedReturned.Wait(TimeSpan.FromSeconds(5)))
                            throw new TimeoutException("The posting thread did not finish delivering Posted.");
                        trace.CaptureAndStop();
                        trace.WriteReport();
                    }
                    catch (Exception ex)
                    {
                        callbackFailure = ex;
                    }
                    finally
                    {
                        frame.Continue = false;
                    }
                }));
                postedReturned.Set();
            });
            RunFrameWithTimeout(frame);
            Assert.Null(callbackFailure);
            Assert.True(postingTask.Wait(TimeSpan.FromSeconds(5)));
            Assert.Null(StartupUiTrace.Current);
        }
        finally
        {
            releasePosted.Set();
            try { postingTask?.Wait(TimeSpan.FromSeconds(5)); }
            finally
            {
                operation?.Abort();
                trace?.Abort();
                dispatcher.Hooks.OperationPosted -= delayPosted;
            }
        }
    }

    private static Window CreateWindow() => new()
    {
        Width = 160,
        Height = 100,
        Left = -10000,
        Top = -10000,
        WindowStartupLocation = WindowStartupLocation.Manual,
        ShowActivated = false,
        ShowInTaskbar = false,
        Content = new System.Windows.Controls.TextBlock { Text = "Dispatcher trace test" }
    };

    private static void RunFrameWithTimeout(DispatcherFrame frame)
    {
        var timeout = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(5) };
        timeout.Tick += (_, _) => frame.Continue = false;
        timeout.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timeout.Stop(); }
    }

    private static void AssertCollected(WeakReference traceReference)
    {
        for (int i = 0; i < 3 && traceReference.IsAlive; i++)
        {
            WpfTestHost.Invoke(() => { });
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(traceReference.IsAlive, "The live Dispatcher and observed window must not retain a stopped trace.");
    }

    private sealed class TraceEnvironment : IDisposable
    {
        private readonly string? previousEnabled = Environment.GetEnvironmentVariable("COLORVISION_STARTUP_TRACE");
        private readonly string? previousOutput = Environment.GetEnvironmentVariable("COLORVISION_STARTUP_TRACE_FILE");
        internal string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "ColorVisionStartupUiTraceTests", Guid.NewGuid().ToString("N"));
        internal string OutputPath => Path.Combine(DirectoryPath, "trace.json");

        internal TraceEnvironment(string? enabled)
        {
            Directory.CreateDirectory(DirectoryPath);
            Environment.SetEnvironmentVariable("COLORVISION_STARTUP_TRACE", enabled);
            Environment.SetEnvironmentVariable("COLORVISION_STARTUP_TRACE_FILE", OutputPath);
        }

        public void Dispose()
        {
            WpfTestHost.Invoke(() => StartupUiTrace.Current?.Abort());
            Environment.SetEnvironmentVariable("COLORVISION_STARTUP_TRACE", previousEnabled);
            Environment.SetEnvironmentVariable("COLORVISION_STARTUP_TRACE_FILE", previousOutput);
            if (File.Exists(OutputPath))
                File.Delete(OutputPath);
            Directory.Delete(DirectoryPath);
        }
    }
}
