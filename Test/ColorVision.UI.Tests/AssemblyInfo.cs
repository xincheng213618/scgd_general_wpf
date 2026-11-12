using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Messages;
using ColorVision.SocketProtocol;
using ColorVision.UI;
using ColorVision.UI.Desktop.TimedButtons;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

// Several production registries reflect over newly loaded assemblies while holding process-wide locks.
// Keep test collections serial until those AssemblyLoad callbacks are made lock-free.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

internal static class TestEnvironment
{
    private static readonly string HostDirectory = Directory.CreateTempSubdirectory("ColorVision-UiTests-").FullName;
    private static readonly string DiagnosticsDirectory = Path.Combine(HostDirectory, "Diagnostics") + Path.DirectorySeparatorChar;
    private static int nextConfiguration;

    [ModuleInitializer]
    internal static void InitializeConfiguration()
    {
        // The first flow view can open a process-wide diagnostic writer during startup recovery.
        // Establish its testhost paths before WPF fixtures or replacement configurations are published.
        Environments.DirAppData = Directory.CreateDirectory(Path.Combine(HostDirectory, "Roaming")).FullName + Path.DirectorySeparatorChar;
        Environments.DirLocalAppData = Directory.CreateDirectory(Path.Combine(HostDirectory, "Local")).FullName + Path.DirectorySeparatorChar;
        Environments.DirLog = Path.Combine(HostDirectory, "Log", "ColorVision.log");
        Directory.CreateDirectory(DiagnosticsDirectory);
        SocketMessageManager.DirectoryPath = DiagnosticsDirectory;
        SocketMessageManager.SqliteDbPath = Path.Combine(DiagnosticsDirectory, "SocketMessages.db");
        TimedButtonOperationStatsSqliteRepository.DirectoryPath = Environments.DirStateDesktop;
        TimedButtonOperationStatsSqliteRepository.DbPath = Path.Combine(Environments.DirStateDesktop, "TimedButtonOperationStats.db");

        var handler = new ConfigHandler { IsAutoSave = false };
        SetConfig(handler);
        // Timed button telemetry and other older code also call the concrete singleton.
        typeof(ConfigHandler).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, handler);
    }

    internal static void SetConfig(IConfigService configuration)
    {
        if (configuration is ConfigHandler handler && string.IsNullOrWhiteSpace(handler.ConfigFilePath))
            handler.ConfigFilePath = Path.Combine(HostDirectory, $"config-{Interlocked.Increment(ref nextConfiguration)}.json");

        var flow = configuration.GetRequiredService<FlowNodeRecordConfig>();
        flow.DirectoryPath = GetDiagnosticDirectory(flow.DirectoryPath);
        var messages = configuration.GetRequiredService<MsgRecordManagerConfig>();
        messages.DirectoryPath = GetDiagnosticDirectory(messages.DirectoryPath);
        ConfigService.SetInstance(configuration);
    }

    private static string GetDiagnosticDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return DiagnosticsDirectory;
        string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
        string tempDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        return directory.StartsWith(tempDirectory, StringComparison.OrdinalIgnoreCase) ? directory : DiagnosticsDirectory;
    }
}
