using ColorVision.Engine.Cache;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services;

public sealed class DeviceDataPathSettings : IConfig
{
    [Browsable(false)]
    public string DataBasePath { get; set; } = FileServerCfg.DefaultDataBasePath;

    internal string PathOrDefault => string.IsNullOrWhiteSpace(DataBasePath) ? FileServerCfg.DefaultDataBasePath : DataBasePath;
    internal static string CurrentPath => ConfigService.Instance?.GetRequiredService<DeviceDataPathSettings>().PathOrDefault
        ?? FileServerCfg.DefaultDataBasePath;
}

internal sealed record DeviceDataPathApplyResult(int UpdatedDevices, int ReloadRequestedDevices,
    string[] ReloadPendingDevices, string[] CameraReconnectDevices);

internal sealed class DeviceDataPathConfiguration
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(DeviceDataPathConfiguration));
    private readonly ConfigHandler configHandler;
    private readonly Func<IEnumerable<DeviceService>> getDevices;
    private readonly Entry[] entries;
    private readonly Func<string, string, IReadOnlyList<string>, Func<bool>, bool> requestReload;
    private readonly HashSet<DeviceService> rejectedReloads = new();
    private readonly HashSet<DeviceService> savedRemoteCameras = new();

    internal DeviceDataPathConfiguration(ConfigHandler configHandler, Func<IEnumerable<DeviceService>> devices,
        Func<string, string, IReadOnlyList<string>, Func<bool>, bool>? requestReload = null)
    {
        this.configHandler = configHandler;
        getDevices = devices;
        entries = PathDevices(devices()).Select(device => new Entry(device, device.CaptureConfigurationTarget(),
            (IFileServerCfg)device.ConfigurationIdentity!)).ToArray();
        this.requestReload = requestReload ?? ((type, service, codes, owns) =>
            MqttRCService.GetInstance().TryReloadServiceDevices(type, service, codes, owns));
    }

    internal string InitialPath => configHandler.GetRequiredService<DeviceDataPathSettings>().PathOrDefault;

    private static IEnumerable<DeviceService> PathDevices(IEnumerable<DeviceService> devices) => devices.Where(device =>
        device.SysResourceModel.Id is not (0 or -1) && device.ConfigurationIdentity is IFileServerCfg);

    internal DeviceDataPathApplyResult Apply(string path)
    {
        path = NormalizePath(path);
        if (!PathDevices(getDevices()).SequenceEqual(entries.Select(entry => entry.Device)))
            throw Invalidated();
        if (entries.Length == 0 && !AccessControl.Evaluate("device.configure", PermissionMode.Administrator).Allowed)
            throw new UnauthorizedAccessException(EngineLocalization.Get("当前权限不足，无法保存设备配置。"));
        foreach (var entry in entries)
        {
            if (!entry.IsCurrent()) throw Invalidated();
            if (!entry.Device.CanConfigure)
                throw new UnauthorizedAccessException(EngineLocalization.Get("当前权限不足，无法保存设备配置。"));
        }

        var settings = configHandler.GetRequiredService<DeviceDataPathSettings>();
        var candidate = new DeviceDataPathSettings { DataBasePath = path };
        if (configHandler.TrySaveAndPublish(candidate, () => settings.DataBasePath = path, out string errorMessage)
            != ConfigSavePublicationStatus.PersistedAndPublished)
            throw new IOException(errorMessage);

        var targets = entries.Select(entry => (entry.Device, entry.Target)).ToArray();
        var configurations = entries.ToDictionary(entry => entry.Device, entry => entry.Configuration);
        IReadOnlyList<DeviceService> changed;
        bool saved;
        try
        {
            saved = DeviceConfigurationTarget.TrySaveBatch(targets,
                (device, previous) => WritePath(previous, configurations[device], path), out changed);
        }
        catch (Exception error)
        {
            Log.Warn("The shared data path could not be saved.", error);
            throw new InvalidOperationException(EngineLocalization.Get("设备配置写入失败，详情请查看日志。"), error);
        }
        if (!saved) throw Invalidated();

        foreach (var entry in entries.Where(entry => entry.IsCurrent()))
        {
            try
            {
                var fileConfig = entry.Configuration.FileServerCfg ??= new FileServerCfg();
                if (!string.Equals(fileConfig.DataBasePath, path, StringComparison.Ordinal)) fileConfig.DataBasePath = path;
            }
            catch (Exception error)
            {
                Log.Warn($"Could not notify a listener of the saved data path for {entry.Device.Code}.", error);
            }
        }

        return ReloadChangedDevices(changed, path);
    }

    private DeviceDataPathApplyResult ReloadChangedDevices(IReadOnlyList<DeviceService> changed, string path)
    {
        var reloads = new List<(Entry Entry, string Type, string Service, string Code)>();
        foreach (var entry in entries.Where(entry => changed.Contains(entry.Device) || rejectedReloads.Contains(entry.Device)))
        {
            var device = entry.Device;
            if (device.SysResourceModel.Id < 0) continue;

            // A camera reload disposes the physical camera. Local captures already read the live path.
            if (entry.Configuration is ConfigCamera)
            {
                if (device is not DeviceCamera camera || !camera.CameraBackend.OpensLocally) savedRemoteCameras.Add(device);
                continue;
            }
            if (device is DeviceSpectrum { IsLocalSpectrumBackend: true })
            {
                rejectedReloads.Remove(device);
                continue;
            }

            string? expectedType = entry.Configuration switch
            {
                ConfigAlgorithm => nameof(CVServiceType.Algorithm),
                ConfigCalibration => nameof(CVServiceType.Calibration),
                ConfigSpectrum => nameof(CVServiceType.Spectrum),
                _ => null
            };
            if (expectedType == null) continue;
            rejectedReloads.Add(device);
            try
            {
                if (entry.Target.TryResolveRestart(device, out var route)
                    && string.Equals(route.TypeCode, expectedType, StringComparison.OrdinalIgnoreCase))
                    reloads.Add((entry, route.TypeCode, route.ParentCode, route.DeviceCode));
            }
            catch (Exception error)
            {
                Log.Warn($"Could not resolve the owning data path reload target for {device.Code}.", error);
            }
        }

        int requested = 0;
        foreach (var service in reloads.GroupBy(item => (item.Type, item.Service)))
        {
            var group = service.ToArray();
            try
            {
                if (!requestReload(service.Key.Type, service.Key.Service, group.Select(item => item.Code).ToArray(),
                    () => group.All(item => item.Entry.IsCurrent()
                        && string.Equals(item.Entry.Configuration.FileServerCfg?.DataBasePath, path, StringComparison.Ordinal)))) continue;
                foreach (var item in group) rejectedReloads.Remove(item.Entry.Device);
                requested += group.Length;
            }
            catch (Exception error)
            {
                Log.Warn($"Could not request a device reload for {service.Key.Service} after saving the data path.", error);
            }
        }

        return new DeviceDataPathApplyResult(changed.Count, requested, DeviceNames(rejectedReloads), DeviceNames(savedRemoteCameras));
    }

    private static string[] DeviceNames(IEnumerable<DeviceService> devices) => devices.Select(device =>
        string.IsNullOrWhiteSpace(device.Name) ? device.Code : device.Name).ToArray();

    private static InvalidOperationException Invalidated() => new(EngineLocalization.Get("DeviceDataPathStale"));

    private static string NormalizePath(string path)
    {
        path = path.Trim();
        if (Path.IsPathFullyQualified(path))
        {
            try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or IOException) { }
        }
        throw new ArgumentException(EngineLocalization.Get("DeviceDataPathInvalid"));
    }

    private static string WritePath(string? previous, IFileServerCfg configuration, string path)
    {
        JObject document;
        if (string.IsNullOrWhiteSpace(previous)) document = new JObject();
        else
        {
            using var reader = new JsonTextReader(new StringReader(previous)) { DateParseHandling = DateParseHandling.None };
            document = JObject.Load(reader);
        }

        var fileSettingsProperty = document.Property(nameof(IFileServerCfg.FileServerCfg), StringComparison.OrdinalIgnoreCase);
        if (fileSettingsProperty?.Value is not JObject settings)
        {
            if (fileSettingsProperty is { Value.Type: not JTokenType.Null })
                throw new InvalidDataException("The persisted file settings are not a JSON object.");
            settings = JObject.FromObject(configuration.FileServerCfg ?? new FileServerCfg());
            settings[nameof(FileServerCfg.DataBasePath)] = path;
            document[fileSettingsProperty?.Name ?? nameof(IFileServerCfg.FileServerCfg)] = settings;
        }
        else
        {
            var pathProperty = settings.Property(nameof(FileServerCfg.DataBasePath), StringComparison.OrdinalIgnoreCase);
            if (pathProperty?.Value is { Type: JTokenType.String } value
                && string.Equals((string?)value, path, StringComparison.Ordinal)) return previous!;
            settings[pathProperty?.Name ?? nameof(FileServerCfg.DataBasePath)] = path;
        }
        return document.ToString(Formatting.None);
    }

    private sealed record Entry(DeviceService Device, DeviceConfigurationTarget Target, IFileServerCfg Configuration)
    {
        internal bool IsCurrent() => Target.IsCurrent(Device);
    }
}
