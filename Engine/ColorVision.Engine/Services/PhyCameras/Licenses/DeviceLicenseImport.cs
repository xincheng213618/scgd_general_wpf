using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.PhySpectrums;
using ColorVision.Engine.Services.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Services.PhyCameras.Licenses;

internal sealed record LicenseDeviceIdentity(int LicenseType, string Key);

internal static class DeviceLicenseImport
{
    internal static LicenseDeviceIdentity Camera(string? cameraId, string? hardwareMd5)
    {
        string key = hardwareMd5?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(cameraId))
        {
            string calculated = Tool.GetMD5(cameraId.Trim());
            if (key.Length > 0 && !string.Equals(key, calculated, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("相机 CameraID 与硬件 MD5 不一致，请先检查物理相机配置。");
            key = calculated;
        }
        if (!IsCameraKey(key)) throw new InvalidDataException("未取得相机硬件 MD5，请先扫描或选择物理相机后导入许可证。");
        return new(0, key.ToUpperInvariant());
    }

    internal static LicenseDeviceIdentity Spectrum(string sn) => new(1, PhySpectrumStore.NormalizeSerial(sn));

    internal static LicenseModel PreserveAssociation(LicenseModel imported, LicenseModel? existing)
    {
        if (existing == null) return imported;
        if (existing.LiceType != imported.LiceType || !string.Equals(existing.MacAddress, imported.MacAddress, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("许可证设备身份或类型冲突，未覆盖现有记录。");
        imported.Id = existing.Id;
        imported.DevCameraId = existing.DevCameraId;
        imported.DevCaliId = existing.DevCaliId;
        imported.CreateDate = existing.CreateDate;
        return imported;
    }

    private static bool IsCameraKey(string value) => value.Length == 32 && value.All(Uri.IsHexDigit);

    internal static IReadOnlyList<LicenseDeviceIdentity> KnownDevices(IEnumerable<SysResourceModel> resources, IEnumerable<LicenseModel> licenses)
    {
        var identities = new List<LicenseDeviceIdentity>();
        foreach (var resource in resources.Where(resource => !resource.IsDelete))
        {
            if (resource.Type == (int)ServiceTypes.PhyCamera && IsCameraKey(resource.Code ?? string.Empty))
                identities.Add(Camera(null, resource.Code));
            else if (resource.Type == (int)ServiceTypes.PhySpectrums && !string.IsNullOrWhiteSpace(resource.Code))
                identities.Add(Spectrum(resource.Code));
            else if (resource.Type == (int)ServiceTypes.Spectrum && !string.IsNullOrWhiteSpace(resource.Value))
            {
                try
                {
                    string? sn = JObject.Parse(resource.Value).Value<string>("SN");
                    if (!string.IsNullOrWhiteSpace(sn)) identities.Add(Spectrum(sn));
                }
                catch (JsonException) { }
            }
        }
        foreach (var license in licenses)
        {
            if (license.LiceType == 0 && IsCameraKey(license.MacAddress ?? string.Empty)) identities.Add(Camera(null, license.MacAddress));
            else if (license.LiceType == 1 && !string.IsNullOrWhiteSpace(license.MacAddress)) identities.Add(Spectrum(license.MacAddress));
        }
        return identities;
    }

    internal static LicenseModel Parse(string value, LicenseDeviceIdentity identity)
    {
        identity = identity.LicenseType switch
        {
            0 => Camera(null, identity.Key),
            1 => Spectrum(identity.Key),
            _ => throw new InvalidDataException("不支持的设备许可证类型。")
        };
        try
        {
            var payload = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(value.Trim().TrimStart('\uFEFF'))));
            string? model = payload.Value<string>("device_mode");
            if (string.IsNullOrWhiteSpace(model) || !long.TryParse(payload.Value<string>("expiry_date"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long expiry))
                throw new FormatException("许可证缺少有效的型号或到期时间。");
            // This establishes the import target only. Native loading still owns signature/hardware validation.
            return new LicenseModel
            {
                MacAddress = identity.Key, LiceType = identity.LicenseType, LicenseValue = value,
                Model = model, CusTomerName = payload.Value<string>("licensee"),
                ExpiryDate = DateTimeOffset.FromUnixTimeSeconds(expiry).LocalDateTime
            };
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            throw new InvalidDataException(Properties.Resources.SpectrumInvalidLicense, ex);
        }
    }

    internal static LicenseModel ReadFile(string path, LicenseDeviceIdentity identity)
    {
        if (string.Equals(Path.GetExtension(path), ".lic", StringComparison.OrdinalIgnoreCase)) return Parse(File.ReadAllText(path), identity);
        if (!string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(Properties.Resources.UnsupportedLicenseFileExtension);
        using var stream = File.OpenRead(path);
        return ReadArchive(stream, identity);
    }

    internal static LicenseModel ReadArchive(Stream stream, LicenseDeviceIdentity identity)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var entries = archive.Entries.Where(entry => string.Equals(Path.GetExtension(entry.Name), ".lic", StringComparison.OrdinalIgnoreCase)).ToArray();
        var matches = entries.Where(entry => string.Equals(Path.GetFileNameWithoutExtension(entry.Name), identity.Key, StringComparison.OrdinalIgnoreCase)).ToArray();
        var selected = matches.Length == 1 ? matches[0] : matches.Length == 0 && entries.Length == 1 ? entries[0] : null;
        if (selected == null) throw new InvalidDataException("压缩包中的许可证无法唯一匹配目标设备，请单独选择该设备的 LIC 文件。");
        using var reader = new StreamReader(selected.Open(), Encoding.UTF8);
        return Parse(reader.ReadToEnd(), identity);
    }

    internal static IReadOnlyList<LicenseModel> ReadFiles(IEnumerable<string> paths, IReadOnlyList<LicenseDeviceIdentity> devices,
        IReadOnlyList<LicenseModel> existing, int? requiredType = null)
    {
        var result = new List<LicenseModel>();
        void Add(string name, string value)
        {
            var contentMatches = existing.Where(license => SameContent(license.LicenseValue, value))
                .Select(license => new LicenseDeviceIdentity(license.LiceType, license.MacAddress ?? string.Empty))
                .Where(identity => devices.Any(device => SameIdentity(device, identity))).ToArray();
            var candidates = contentMatches.Length > 0 ? contentMatches : devices.Where(device =>
                string.Equals(device.Key, name, StringComparison.OrdinalIgnoreCase)
                || device.LicenseType == 0 && string.Equals(device.Key, Tool.GetMD5(name), StringComparison.OrdinalIgnoreCase)).ToArray();
            var unique = candidates.GroupBy(candidate => $"{candidate.LicenseType}:{candidate.Key.ToUpperInvariant()}").Select(group => group.First()).ToArray();
            // Preserve canonical legacy camera packages; arbitrary renamed files never create a camera identity.
            if (unique.Length == 0 && requiredType != 1 && IsCameraKey(name)) unique = [Camera(null, name)];
            if (unique.Length != 1)
                throw new InvalidDataException($"无法唯一确定许可证“{name}”所属设备，请在物理相机或光谱仪中选择目标设备后导入。");
            if (requiredType.HasValue && unique[0].LicenseType != requiredType.Value)
                throw new InvalidDataException("许可证设备类型不匹配，请使用对应设备的许可证导入入口。");
            result.Add(Parse(value, unique[0]));
        }
        foreach (string path in paths)
        {
            if (string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ZipFile.OpenRead(path);
                var entries = archive.Entries.Where(entry => string.Equals(Path.GetExtension(entry.Name), ".lic", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (entries.Length == 0) throw new InvalidDataException(Properties.Resources.SpectrumInvalidLicense);
                foreach (var entry in entries)
                {
                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                    Add(Path.GetFileNameWithoutExtension(entry.Name), reader.ReadToEnd());
                }
            }
            else if (string.Equals(Path.GetExtension(path), ".lic", StringComparison.OrdinalIgnoreCase))
                Add(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path));
            else throw new InvalidDataException(Properties.Resources.UnsupportedLicenseFileExtension);
        }
        if (result.GroupBy(license => license.MacAddress, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidDataException("本次导入包含同一设备的多个许可证，请保留一份后重试。");
        return result;
    }

    private static bool SameIdentity(LicenseDeviceIdentity left, LicenseDeviceIdentity right) =>
        left.LicenseType == right.LicenseType && string.Equals(left.Key, right.Key, StringComparison.OrdinalIgnoreCase);

    private static bool SameContent(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left)) return false;
        try { return Convert.FromBase64String(left.Trim().TrimStart('\uFEFF')).AsSpan().SequenceEqual(Convert.FromBase64String(right.Trim().TrimStart('\uFEFF'))); }
        catch (FormatException) { return false; }
    }
}
