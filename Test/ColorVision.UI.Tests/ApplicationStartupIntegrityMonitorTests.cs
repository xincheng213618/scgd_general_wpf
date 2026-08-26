using ColorVisionServiceHost;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class ApplicationStartupIntegrityMonitorTests : IDisposable
{
    private readonly string _applicationDirectory = Path.Combine(
        Path.GetTempPath(),
        $"ColorVisionStartupIntegrityTests-{Guid.NewGuid():N}");

    public ApplicationStartupIntegrityMonitorTests()
    {
        Directory.CreateDirectory(_applicationDirectory);
    }

    [Fact]
    public void DependencyInspectorReportsMissingManagedAndCurrentWindowsNativeFiles()
    {
        File.WriteAllText(Path.Combine(_applicationDirectory, "ColorVision.runtimeconfig.json"), "{}");
        File.WriteAllText(Path.Combine(_applicationDirectory, "ColorVision.dll"), "main");
        File.WriteAllText(Path.Combine(_applicationDirectory, "Present.Package.dll"), "managed");
        string nativeDirectory = Path.Combine(_applicationDirectory, "runtimes", "win-x64", "native");
        Directory.CreateDirectory(nativeDirectory);
        File.WriteAllText(Path.Combine(nativeDirectory, "NativePresent.dll"), "native");
        File.WriteAllText(
            Path.Combine(_applicationDirectory, "ColorVision.deps.json"),
            """
            {
              "runtimeTarget": { "name": ".NETCoreApp,Version=v10.0" },
              "targets": {
                ".NETCoreApp,Version=v10.0": {
                  "ColorVision/1.0.0": {
                    "runtime": {
                      "ColorVision.dll": {},
                      "ColorVision.Engine.dll": {},
                      "lib/net10.0/Present.Package.dll": {}
                    },
                    "runtimeTargets": {
                      "runtimes/win-x64/native/NativePresent.dll": {
                        "rid": "win-x64",
                        "assetType": "native"
                      },
                      "runtimes/win-x64/native/NativeMissing.dll": {
                        "rid": "win-x64",
                        "assetType": "native"
                      },
                      "runtimes/linux-x64/native/IgnoreOnWindows.so": {
                        "rid": "linux-x64",
                        "assetType": "native"
                      }
                    }
                  }
                }
              }
            }
            """);

        IReadOnlyList<string> missing =
            ApplicationRuntimeDependencyInspector.FindMissingDependencies(_applicationDirectory);

        Assert.Equal(
            ["ColorVision.Engine.dll", Path.Combine("runtimes", "win-x64", "native", "NativeMissing.dll")],
            missing);
    }

    [Fact]
    public void DependencyInspectorReportsUnreadableControlFileWithoutGuessingDependencies()
    {
        File.WriteAllText(Path.Combine(_applicationDirectory, "ColorVision.runtimeconfig.json"), "{}");

        IReadOnlyList<string> missing =
            ApplicationRuntimeDependencyInspector.FindMissingDependencies(_applicationDirectory);

        Assert.Equal(["ColorVision.deps.json"], missing);
    }

    [Fact]
    public void WindowsProcessStartSourceBuildsAValidTraceQuery()
    {
        using WmiColorVisionProcessStartSource source = new();
    }

    [Fact]
    public async Task StartupStatusHubCompletesObservationForHandledFailure()
    {
        ApplicationStartupStatusHub hub = new();
        Task<ApplicationStartupStatusReport> terminalStatus = hub.WaitForTerminalStatusAsync(1234);
        ApplicationStartupStatusReport report = new(
            1234,
            "failed-handled",
            "DispatcherUnhandledException",
            "ColorVision.Common.dll",
            typeof(FileNotFoundException).FullName!,
            "missing",
            true);

        Assert.True(hub.Report(report));
        Assert.Same(report, await terminalStatus);
        hub.Forget(1234);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void MissingDependencyMessageIsOnlyShownAfterProcessExitedBeforeStartupCompleted(
        bool processExitedBeforeObservationDeadline,
        bool terminalStatusReceived,
        bool expected)
    {
        Assert.Equal(
            expected,
            ApplicationStartupIntegrityMonitor.ShouldShowMissingDependencyMessage(
                processExitedBeforeObservationDeadline,
                terminalStatusReceived));
    }

    [Fact]
    public void StartupFailurePresentationRecognizesMissingAssemblyButNotDataFile()
    {
        FileNotFoundException missingAssembly = new(
            "missing",
            "ColorVision.Common, Version=1.5.7.0, Culture=neutral");

        Assert.True(global::ColorVision.StartupFailureGuard.TryCreateFailurePresentation(
            missingAssembly,
            out global::ColorVision.StartupFailurePresentation? presentation));
        Assert.Equal("ColorVision.Common.dll", presentation!.Component);
        Assert.Contains("请重新安装 ColorVision", presentation.Message, StringComparison.Ordinal);

        Assert.False(global::ColorVision.StartupFailureGuard.TryCreateFailurePresentation(
            new FileNotFoundException("missing", @"C:\Data\measurement.json"),
            out _));
    }

    public void Dispose()
    {
        Directory.Delete(_applicationDirectory, recursive: true);
    }
}
