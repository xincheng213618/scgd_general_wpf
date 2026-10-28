using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI.Maintenance;
using ColorVision.UI.Authorizations;
using ColorVision.Themes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Windows;

namespace ColorVision.Engine.Services.Storage;

internal sealed record EngineStorageLocation(string CategoryId, StorageUsageRule Rule, bool AllowCleanup = true);

/// <summary>Publishes only configured module-owned locations; constructing the provider never starts services or opens storage.</summary>
public sealed class EngineStorageUsageProvider : IStorageUsageProvider, IFileMaintenanceProvider
{
    internal const string CameraResources = "Engine.CameraResources";
    internal const string CameraImages = "Engine.CameraImages";
    internal const string AlgorithmImages = "Engine.AlgorithmImages";
    internal const string PreProcessFiles = "Engine.PreProcessFiles";
    private static readonly string[] CategoryIds = [CameraResources, CameraImages, AlgorithmImages, PreProcessFiles];
    private static readonly ResourceManager Text = new("ColorVision.Engine.Services.Storage.StorageSourceResources", typeof(EngineStorageUsageProvider).Assembly);
    private readonly Func<IEnumerable<EngineStorageLocation>> _capture;
    private readonly Func<bool>? _injectedActivity;
    private EngineStorageLocation[] _locations = [];
    private string[] _protectedRoots = [];
    private Func<bool> _isActive = () => false;

    public EngineStorageUsageProvider() : this(CaptureLocations) { }
    internal EngineStorageUsageProvider(Func<IEnumerable<EngineStorageLocation>> capture, Func<bool>? isActive = null)
    {
        _capture = capture;
        _injectedActivity = isActive;
    }

    public void PrepareStorageScan()
    {
        _locations = _capture().ToArray();
        _protectedRoots = _locations.Where(location => location.CategoryId == CameraResources)
            .Select(location => NormalizePath(location.Rule.RootPath)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _isActive = _injectedActivity ?? CaptureActivity();
    }

    public void PrepareScan() => PrepareStorageScan();
    public IEnumerable<StorageUsageCategory> GetStorageCategories() => CategoryIds.Select(id => new UsageCategory(this, id));
    public IEnumerable<FileMaintenanceCategory> GetCategories() => CategoryIds.Where(id => id != CameraResources).Select(id => new CleanupCategory(this, id));

    private StorageUsageRule[] Rules(string id, bool cleanupOnly = false) => _locations.Where(location => location.CategoryId == id && (!cleanupOnly || location.AllowCleanup)).Select(location => location.Rule).ToArray();
    private string[] Directories(string id, bool cleanupOnly = false) => Rules(id, cleanupOnly).Select(rule => NormalizePath(rule.RootPath)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static string Label(string id, string suffix) => Text.GetString(id[(id.IndexOf('.') + 1)..] + suffix, CultureInfo.CurrentUICulture) ?? id;
    private bool IsProtected(string path) => _isActive() || _protectedRoots.Any(root => IsWithin(root, path));

    private sealed class UsageCategory(EngineStorageUsageProvider owner, string id) : StorageUsageCategory
    {
        public override string Id => id;
        public override string Title => Label(id, "Title");
        public override string Description => Label(id, "Description");
        public override IReadOnlyList<string> SourceDirectories => owner.Directories(id);
        public override bool IsProtected => id == CameraResources;
        public override string? ManagementCategoryId => IsProtected || owner.Rules(id, cleanupOnly: true).Length == 0 ? null : id;
        public override Action? OpenManager => IsProtected ? OpenCameraManager : null;
        public override StorageUsageSnapshot Scan(CancellationToken cancellationToken) => StorageUsageScanner.Scan(owner.Rules(id), cancellationToken);
    }

    private static void OpenCameraManager()
    {
        AccessControl.Demand("physical-camera.manage", PermissionMode.Administrator);
        new PhyCameraManagerWindow { Owner = WindowHelpers.GetActiveWindow() }.Show();
    }

    private sealed class CleanupCategory(EngineStorageUsageProvider owner, string id) : FileMaintenanceCategory
    {
        public override string Id => id;
        public override string Title => Label(id, "Title");
        public override string Description => Label(id, "Description");
        public override int? DefaultRetentionDays => 30;
        public override IReadOnlyList<string> SourceDirectories => owner.Directories(id, cleanupOnly: true);
        public override string ProtectionNotice => Text.GetString("CleanupNotice", CultureInfo.CurrentUICulture) ?? string.Empty;
        public override FileMaintenanceSnapshot Scan(int retentionDays, FileMaintenanceSnapshot? previous, CancellationToken cancellationToken)
        {
            var rules = owner.Rules(id, cleanupOnly: true).Select(rule => new MaintenanceFileCleanupRule(id, rule.RootPath, rule.SearchPattern, rule.Recursive,
                retentionDays, path => owner.IsProtected(path) || rule.IncludeFile?.Invoke(path) == false));
            return FileMaintenanceSnapshot.FromFiles(MaintenanceFileCleanup.Scan(rules, cancellationToken, previous?.Token as MaintenanceFileCleanupScanResult));
        }
        public override FileMaintenanceCleanupResult Cleanup(FileMaintenanceSnapshot confirmed, CancellationToken cancellationToken)
        {
            if (confirmed.Token is not MaintenanceFileCleanupScanResult files) throw new InvalidOperationException("Invalid module storage scan.");
            return FileMaintenanceCleanupResult.FromFiles(MaintenanceFileCleanup.Cleanup(files, cancellationToken));
        }
    }

    private static IEnumerable<EngineStorageLocation> CaptureLocations()
    {
        DeviceService[] devices = ServiceManager.Current?.DeviceServices.ToArray() ?? [];
        foreach (DeviceCamera device in devices.OfType<DeviceCamera>())
            if (OwnedDeviceDirectory(device.Config.FileServerCfg.DataBasePath, device.Code, "Data") is { } directory)
                yield return new(CameraImages, new(directory, Recursive: true));
        foreach (DeviceAlgorithm device in devices.OfType<DeviceAlgorithm>())
            if (OwnedDeviceDirectory(device.Config.FileServerCfg.DataBasePath, device.Config.Code, "Data") is { } directory)
                yield return new(AlgorithmImages, new(directory, Recursive: true));

        IEnumerable<PhyCamera> cameras = (PhyCameraManager.Current?.PhyCameras ?? [])
            .Concat(devices.OfType<DeviceCamera>().Select(device => device.PhyCamera).OfType<PhyCamera>()).Distinct();
        foreach (PhyCamera camera in cameras)
            if (OwnedDeviceDirectory(camera.Config.FileServerCfg.FileBasePath, camera.Code, "cfg") is { } directory)
                yield return new(CameraResources, new(directory, Recursive: true));

        foreach (FolderSizePreProcessorConfig config in CapturePreProcessFolders())
        {
            HashSet<string> extensions = ParseExtensions(config.FileExtensions);
            foreach (string directory in config.FolderPaths?.Where(path => !string.IsNullOrWhiteSpace(path)) ?? [])
                yield return new(PreProcessFiles, new(directory, Recursive: config.IncludeSubfolders,
                    IncludeFile: path => extensions.Count == 0 || extensions.Contains(Path.GetExtension(path))));
        }
        // Contributors are the configured action instances, never freshly constructed provider defaults.
        foreach (IStorageUsageContributor contributor in PreProcessManager.Current?.Processes.Select(action => action.Process).OfType<IStorageUsageContributor>() ?? [])
            foreach (StorageUsageRule rule in contributor.GetStorageUsageRules())
                yield return new(PreProcessFiles, rule, AllowCleanup: false);
    }

    private static IEnumerable<FolderSizePreProcessorConfig> CapturePreProcessFolders()
    {
        if (PreProcessManager.Current is { } manager)
            return manager.Processes.Select(action => action.Process.GetConfig()).OfType<FolderSizePreProcessorConfig>().ToArray();
        // The storage page must not initialize preprocessors merely to inspect already saved locations.
        if (!File.Exists(PreProcessManager.PersistFilePath)) return [];
        var persisted = JsonConvert.DeserializeObject<PreProcessPersist[]>(File.ReadAllText(PreProcessManager.PersistFilePath)) ?? [];
        return persisted.Where(item => item.ProcessTypeFullName == typeof(FolderSizePreProcessor).FullName && !string.IsNullOrWhiteSpace(item.ConfigJson))
            .Select(item => JsonConvert.DeserializeObject<FolderSizePreProcessorConfig>(item.ConfigJson!)).OfType<FolderSizePreProcessorConfig>().ToArray();
    }

    private static Func<bool> CaptureActivity()
    {
        DeviceService[] devices = ServiceManager.Current?.DeviceServices.ToArray() ?? [];
        DeviceCamera[] cameras = devices.OfType<DeviceCamera>().ToArray();
        DeviceAlgorithm[] algorithms = devices.OfType<DeviceAlgorithm>().ToArray();
        return () => FlowRuntimeActivityRegistry.Capture().EngineRunning
            || cameras.Any(camera => camera.CameraBackend.VideoOwned || camera.CameraBackend.Status is DeviceStatusType.Busy or DeviceStatusType.LiveOpened or DeviceStatusType.Opening or DeviceStatusType.Closing)
            || algorithms.Any(algorithm => algorithm.DService.DeviceStatus == DeviceStatusType.Busy);
    }

    internal static string? OwnedDeviceDirectory(string? root, string? code, string child)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) || string.IsNullOrWhiteSpace(code)
            || code is "." or ".." || code.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        return NormalizePath(Path.Combine(root, code, child));
    }

    private static string? NormalizePath(string path)
    {
        try { return Path.IsPathFullyQualified(path) ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) : null; }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }
    private static bool IsWithin(string root, string path) => path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static HashSet<string> ParseExtensions(string value) => new((value ?? string.Empty).Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(extension => extension.StartsWith('.') ? extension : "." + extension), StringComparer.OrdinalIgnoreCase);
}
