using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace ColorVisionServiceHost;

internal interface IApplicationStartupIntegrityMonitorLifetime : IDisposable
{
    Task Start();

    Task StopAsync();
}

internal sealed class ApplicationStartupIntegrityMonitor : IApplicationStartupIntegrityMonitorLifetime
{
    private static readonly TimeSpan DefaultStartupObservationDelay = TimeSpan.FromSeconds(10);
    private const string ApplicationProcessName = "ColorVision";

    private readonly object _lifecycleSync = new();
    private readonly Dictionary<int, Task> _observations = new();
    private readonly IColorVisionProcessStartSource _processStartSource;
    private readonly ApplicationStartupStatusHub _startupStatusHub;
    private readonly TimeSpan _startupObservationDelay;
    private CancellationTokenSource? _lifetimeCancellation;
    private HashSet<string> _installedExecutablePaths = new(StringComparer.OrdinalIgnoreCase);
    private bool _started;

    public static ApplicationStartupIntegrityMonitor Default { get; } = new();

    private ApplicationStartupIntegrityMonitor()
        : this(
            new WmiColorVisionProcessStartSource(),
            DefaultStartupObservationDelay,
            ApplicationStartupStatusHub.Default)
    {
    }

    internal ApplicationStartupIntegrityMonitor(
        IColorVisionProcessStartSource processStartSource,
        TimeSpan startupObservationDelay,
        ApplicationStartupStatusHub? startupStatusHub = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(startupObservationDelay, TimeSpan.Zero);
        _processStartSource = processStartSource ?? throw new ArgumentNullException(nameof(processStartSource));
        _startupObservationDelay = startupObservationDelay;
        _startupStatusHub = startupStatusHub ?? ApplicationStartupStatusHub.Default;
    }

    public Task Start()
    {
        lock (_lifecycleSync)
        {
            if (_started)
                return Task.CompletedTask;

            _installedExecutablePaths = InstalledColorVisionLocator.FindExecutablePaths()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (_installedExecutablePaths.Count == 0)
            {
                ServiceHostLog.Write("Application startup integrity monitor found no registered ColorVision installation.");
                return Task.CompletedTask;
            }

            _lifetimeCancellation = new CancellationTokenSource();
            _processStartSource.ProcessStarted += ProcessStartSource_ProcessStarted;
            try
            {
                _processStartSource.Start();
                _started = true;
            }
            catch (Exception ex)
            {
                _processStartSource.ProcessStarted -= ProcessStartSource_ProcessStarted;
                _lifetimeCancellation.Dispose();
                _lifetimeCancellation = null;
                ServiceHostLog.Write($"Application startup integrity monitoring could not subscribe to process-start events: {ex}");
                return Task.CompletedTask;
            }
        }

        ServiceHostLog.Write(
            $"Application startup integrity monitor subscribed for {_installedExecutablePaths.Count} installation(s).");
        ObserveExistingProcesses();
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cancellation;
        Task[] observations;
        lock (_lifecycleSync)
        {
            if (!_started)
                return;

            _started = false;
            _processStartSource.ProcessStarted -= ProcessStartSource_ProcessStarted;
            cancellation = _lifetimeCancellation;
            _lifetimeCancellation = null;
            observations = _observations.Values.ToArray();
        }

        try
        {
            _processStartSource.Stop();
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Application startup integrity process subscription could not stop cleanly: {ex}");
        }
        cancellation?.Cancel();

        try
        {
            await Task.WhenAll(observations).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _processStartSource.Dispose();
    }

    private void ProcessStartSource_ProcessStarted(object? sender, ColorVisionProcessStartedEventArgs e)
    {
        TryObserveProcess(e.ProcessId, e.SessionId);
    }

    private void ObserveExistingProcesses()
    {
        foreach (Process process in Process.GetProcessesByName(ApplicationProcessName))
        {
            using (process)
            {
                try
                {
                    TryObserveProcess(process.Id, process.SessionId);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
    }

    private void TryObserveProcess(int processId, int reportedSessionId)
    {
        if (!TryResolveProcess(processId, reportedSessionId, out string executablePath, out int sessionId))
            return;

        string applicationDirectory = Path.GetDirectoryName(executablePath)!;
        Task observation;
        lock (_lifecycleSync)
        {
            if (!_started
                || _lifetimeCancellation == null
                || _observations.ContainsKey(processId))
            {
                return;
            }

            CancellationToken token = _lifetimeCancellation.Token;
            observation = Task.Run(
                () => ObserveStartupAsync(processId, sessionId, applicationDirectory, token),
                CancellationToken.None);
            _observations.Add(processId, observation);
        }

        _ = observation.ContinueWith(
            static (_, state) =>
            {
                (ApplicationStartupIntegrityMonitor Monitor, int ProcessId) context =
                    ((ApplicationStartupIntegrityMonitor, int))state!;
                lock (context.Monitor._lifecycleSync)
                    context.Monitor._observations.Remove(context.ProcessId);
            },
            (this, processId),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private bool TryResolveProcess(
        int processId,
        int reportedSessionId,
        out string executablePath,
        out int sessionId)
    {
        executablePath = string.Empty;
        sessionId = reportedSessionId;
        try
        {
            using Process process = Process.GetProcessById(processId);
            string? processPath = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(processPath))
                executablePath = Path.GetFullPath(processPath);
            sessionId = process.SessionId;
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }

        if (!string.IsNullOrWhiteSpace(executablePath))
            return _installedExecutablePaths.Contains(executablePath);

        if (_installedExecutablePaths.Count != 1 || sessionId < 0)
            return false;

        executablePath = _installedExecutablePaths.Single();
        return true;
    }

    private async Task ObserveStartupAsync(
        int processId,
        int sessionId,
        string applicationDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            Task timeoutTask = Task.Delay(_startupObservationDelay, cancellationToken);
            Task exitTask = WaitForProcessExitAsync(processId, cancellationToken);
            Task<ApplicationStartupStatusReport> terminalStatusTask = _startupStatusHub.WaitForTerminalStatusAsync(processId);
            Task completedTask = await Task.WhenAny(timeoutTask, exitTask, terminalStatusTask).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (completedTask == terminalStatusTask || terminalStatusTask.IsCompletedSuccessfully)
            {
                ApplicationStartupStatusReport report = await terminalStatusTask.ConfigureAwait(false);
                ServiceHostLog.Write(
                    $"ColorVision startup observation completed by application report. Process={processId}; " +
                    $"State={report.State}; Stage={report.Stage}; Component={report.Component}");
                return;
            }

            IReadOnlyList<string> missingFiles = ApplicationRuntimeDependencyInspector.FindMissingDependencies(applicationDirectory);
            if (missingFiles.Count == 0)
                return;

            ServiceHostLog.Write(
                $"ColorVision startup integrity failure detected. Process={processId}; Session={sessionId}; " +
                $"Application={applicationDirectory}; Missing={string.Join(", ", missingFiles)}");
            InteractiveSessionMessageService.TryShowMissingDependencyMessage(sessionId, missingFiles);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"ColorVision startup integrity observation failed for process {processId}: {ex}");
        }
        finally
        {
            _startupStatusHub.Forget(processId);
        }
    }

    private static async Task WaitForProcessExitAsync(int processId, CancellationToken cancellationToken)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!process.HasExited)
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}

internal sealed record ApplicationStartupStatusReport(
    int ProcessId,
    string State,
    string Stage,
    string Component,
    string ExceptionType,
    string Detail,
    bool PromptShown);

internal sealed class ApplicationStartupStatusHub
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<ApplicationStartupStatusReport>> _terminalStatuses = new();

    public static ApplicationStartupStatusHub Default { get; } = new();

    public Task<ApplicationStartupStatusReport> WaitForTerminalStatusAsync(int processId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        return _terminalStatuses.GetOrAdd(processId, static _ => CreateCompletionSource()).Task;
    }

    public bool Report(ApplicationStartupStatusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.ProcessId <= 0 || !IsKnownState(report.State))
            return false;

        ServiceHostLog.Write(
            $"ColorVision startup status received. Process={report.ProcessId}; State={report.State}; " +
            $"Stage={report.Stage}; Component={report.Component}; PromptShown={report.PromptShown}");
        if (!IsTerminalState(report.State))
            return true;

        return _terminalStatuses
            .GetOrAdd(report.ProcessId, static _ => CreateCompletionSource())
            .TrySetResult(report);
    }

    public void Forget(int processId)
    {
        if (processId > 0)
            _terminalStatuses.TryRemove(processId, out _);
    }

    internal static bool IsKnownState(string state) =>
        state.Equals("begin", StringComparison.OrdinalIgnoreCase)
        || state.Equals("progress", StringComparison.OrdinalIgnoreCase)
        || IsTerminalState(state);

    internal static bool IsTerminalState(string state) =>
        state.Equals("ready", StringComparison.OrdinalIgnoreCase)
        || state.Equals("failed-handled", StringComparison.OrdinalIgnoreCase);

    private static TaskCompletionSource<ApplicationStartupStatusReport> CreateCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal static class ApplicationRuntimeDependencyInspector
{
    private static readonly string[] RequiredControlFiles =
    [
        "ColorVision.deps.json",
        "ColorVision.runtimeconfig.json",
    ];

    public static IReadOnlyList<string> FindMissingDependencies(string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        string fullApplicationDirectory = Path.GetFullPath(applicationDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        SortedSet<string> missing = new(StringComparer.OrdinalIgnoreCase);
        foreach (string controlFile in RequiredControlFiles)
        {
            if (!File.Exists(Path.Combine(fullApplicationDirectory, controlFile)))
                missing.Add(controlFile);
        }

        string dependencyFile = Path.Combine(fullApplicationDirectory, "ColorVision.deps.json");
        if (!File.Exists(dependencyFile))
            return missing.ToArray();

        try
        {
            JObject document = JObject.Parse(File.ReadAllText(dependencyFile));
            JObject? targets = document["targets"] as JObject;
            JObject? target = targets?.Properties().Select(property => property.Value).OfType<JObject>().FirstOrDefault();
            if (target == null)
            {
                missing.Add("ColorVision.deps.json");
                return missing.ToArray();
            }

            foreach (JObject library in target.Properties().Select(property => property.Value).OfType<JObject>())
            {
                if (library["runtime"] is JObject runtimeAssets)
                {
                    foreach (JProperty asset in runtimeAssets.Properties())
                    {
                        string fileName = Path.GetFileName(asset.Name.Replace('/', Path.DirectorySeparatorChar));
                        if (!string.IsNullOrWhiteSpace(fileName)
                            && !File.Exists(Path.Combine(fullApplicationDirectory, fileName)))
                        {
                            missing.Add(fileName);
                        }
                    }
                }

                if (library["runtimeTargets"] is not JObject runtimeTargets)
                    continue;

                foreach (JProperty asset in runtimeTargets.Properties())
                {
                    if (asset.Value is not JObject metadata
                        || !string.Equals((string?)metadata["assetType"], "native", StringComparison.OrdinalIgnoreCase)
                        || !IsCurrentWindowsRuntimeIdentifier((string?)metadata["rid"])
                        || !TryResolveAssetPath(fullApplicationDirectory, asset.Name, out string candidatePath)
                        || File.Exists(candidatePath))
                    {
                        continue;
                    }

                    missing.Add(asset.Name.Replace('/', Path.DirectorySeparatorChar));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {
            ServiceHostLog.Write($"Unable to inspect ColorVision runtime dependencies at '{dependencyFile}': {ex.Message}");
            missing.Add("ColorVision.deps.json");
        }

        return missing.ToArray();
    }

    private static bool IsCurrentWindowsRuntimeIdentifier(string? runtimeIdentifier)
    {
        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => string.Empty,
        };
        return string.Equals(runtimeIdentifier, "win", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(architecture)
                && string.Equals(runtimeIdentifier, $"win-{architecture}", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryResolveAssetPath(string applicationDirectory, string assetPath, out string fullPath)
    {
        fullPath = string.Empty;
        string[] segments = assetPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." || segment.Contains(':')))
            return false;

        string candidatePath = Path.GetFullPath(Path.Combine([applicationDirectory, .. segments]));
        string directoryPrefix = applicationDirectory + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        fullPath = candidatePath;
        return true;
    }
}

internal static class InteractiveSessionMessageService
{
    private const uint MessageBoxIconError = 0x00000010;
    private const uint MessageBoxSetForeground = 0x00010000;
    private const uint MessageBoxTopMost = 0x00040000;

    public static bool TryShowMissingDependencyMessage(int sessionId, IReadOnlyList<string> missingFiles)
    {
        if (sessionId < 0 || missingFiles.Count == 0)
            return false;

        string title = "ColorVision 无法启动";
        string fileList = string.Join(Environment.NewLine, missingFiles.Take(6).Select(path => $"• {path}"));
        if (missingFiles.Count > 6)
            fileList += $"{Environment.NewLine}• 以及其他 {missingFiles.Count - 6} 个文件";
        string message =
            $"检测到 ColorVision 安装文件不完整，程序无法正常启动。{Environment.NewLine}{Environment.NewLine}" +
            $"缺少文件：{Environment.NewLine}{fileList}{Environment.NewLine}{Environment.NewLine}" +
            "请重新安装 ColorVision 后再试。重新安装不会删除现有配置。";

        bool sent = WTSSendMessage(
            IntPtr.Zero,
            sessionId,
            title,
            System.Text.Encoding.Unicode.GetByteCount(title),
            message,
            System.Text.Encoding.Unicode.GetByteCount(message),
            MessageBoxIconError | MessageBoxSetForeground | MessageBoxTopMost,
            0,
            out _,
            false);
        if (!sent)
        {
            ServiceHostLog.Write(
                $"Unable to show startup integrity message in session {sessionId}. Win32Error={Marshal.GetLastWin32Error()}");
        }
        return sent;
    }

    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSSendMessage(
        IntPtr server,
        int sessionId,
        string title,
        int titleLength,
        string message,
        int messageLength,
        uint style,
        uint timeout,
        out uint response,
        [MarshalAs(UnmanagedType.Bool)] bool wait);
}

internal interface IColorVisionProcessStartSource : IDisposable
{
    event EventHandler<ColorVisionProcessStartedEventArgs>? ProcessStarted;

    void Start();

    void Stop();
}

internal sealed record ColorVisionProcessStartedEventArgs(int ProcessId, int SessionId);

internal sealed class WmiColorVisionProcessStartSource : IColorVisionProcessStartSource
{
    private readonly ManagementEventWatcher _watcher = new(new WqlEventQuery(
        "SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName = 'ColorVision.exe'"));

    public event EventHandler<ColorVisionProcessStartedEventArgs>? ProcessStarted;

    public void Start()
    {
        _watcher.EventArrived += Watcher_EventArrived;
        _watcher.Start();
    }

    public void Stop()
    {
        _watcher.Stop();
        _watcher.EventArrived -= Watcher_EventArrived;
    }

    public void Dispose()
    {
        _watcher.EventArrived -= Watcher_EventArrived;
        _watcher.Dispose();
    }

    private void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            int processId = Convert.ToInt32(e.NewEvent["ProcessID"]);
            int sessionId = Convert.ToInt32(e.NewEvent["SessionID"]);
            if (processId > 0)
                ProcessStarted?.Invoke(this, new ColorVisionProcessStartedEventArgs(processId, sessionId));
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Unable to read a ColorVision process-start event: {ex.Message}");
        }
    }
}

internal static class InstalledColorVisionLocator
{
    private const string UninstallRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static IReadOnlyList<string> FindExecutablePaths()
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using RegistryKey? uninstallKey = baseKey.OpenSubKey(UninstallRegistryPath);
                if (uninstallKey == null)
                    continue;

                foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                {
                    using RegistryKey? productKey = uninstallKey.OpenSubKey(subKeyName);
                    if (productKey == null
                        || !string.Equals(productKey.GetValue("DisplayName") as string, "ColorVision", StringComparison.OrdinalIgnoreCase)
                        || productKey.GetValue("InstallLocation") is not string installLocation
                        || string.IsNullOrWhiteSpace(installLocation))
                    {
                        continue;
                    }

                    string executablePath = Path.GetFullPath(Path.Combine(installLocation, ApplicationExecutableName));
                    if (File.Exists(executablePath))
                        paths.Add(executablePath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                ServiceHostLog.Write($"Unable to inspect registered ColorVision installations in {view}: {ex.Message}");
            }
        }

        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private const string ApplicationExecutableName = "ColorVision.exe";
}

internal sealed class NullApplicationStartupIntegrityMonitor : IApplicationStartupIntegrityMonitorLifetime
{
    public static NullApplicationStartupIntegrityMonitor Instance { get; } = new();

    private NullApplicationStartupIntegrityMonitor()
    {
    }

    public Task Start() => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }
}
