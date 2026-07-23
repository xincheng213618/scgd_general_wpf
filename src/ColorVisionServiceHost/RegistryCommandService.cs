using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace ColorVisionServiceHost;

/// <summary>
/// Provides generic HKLM mutations for approved ColorVision callers. Key paths are intentionally
/// not restricted to a feature-specific allow-list; authorization is enforced by the pipe caller
/// identity and single-use broker ticket handled before command dispatch.
/// </summary>
internal static class RegistryCommandService
{
    private static readonly HashSet<string> OtherHiveAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "HKEY_CLASSES_ROOT", "HKCR",
        "HKEY_CURRENT_USER", "HKCU",
        "HKEY_USERS", "HKU",
        "HKEY_CURRENT_CONFIG", "HKCC",
        "HKEY_PERFORMANCE_DATA", "HKPD",
    };

    public static ServiceHostResponse SetValues(ServiceHostRequest request)
    {
        LocalMachineRegistryMutation mutation = ParseMutation(request);
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, mutation.RegistryView);
        using RegistryKey key = baseKey.CreateSubKey(mutation.KeyPath, writable: true)
            ?? throw new InvalidOperationException($@"Unable to create registry key: HKLM\{mutation.KeyPath} ({mutation.RegistryView})");

        foreach (string valueName in mutation.DeleteValueNames)
            key.DeleteValue(valueName, throwOnMissingValue: false);
        foreach (ServiceHostRegistryValue value in mutation.Values)
            key.SetValue(value.Name, value.Value, value.Kind);

        ServiceHostLog.Write(
            $@"HKLM registry values updated: HKLM\{mutation.KeyPath}; view={mutation.RegistryView}; " +
            $"set=[{string.Join(",", mutation.Values.Select(value => $"{value.Name}:{value.Kind}"))}]; " +
            $"deleted=[{string.Join(",", mutation.DeleteValueNames)}]");
        return ServiceHostResponse.FromObject(request.RequestId, true, "registry_values_updated", new
        {
            hive = "HKEY_LOCAL_MACHINE",
            registryView = mutation.RegistryView.ToString(),
            mutation.KeyPath,
            setValues = mutation.Values.Select(value => new { value.Name, kind = value.Kind.ToString() }).ToArray(),
            deletedValues = mutation.DeleteValueNames,
        });
    }

    public static ServiceHostResponse DeleteKey(ServiceHostRequest request)
    {
        string keyPath = NormalizeKeyPath(GetRequiredDataValue(request, "keyPath"));
        RegistryView registryView = ParseRegistryView(request);
        bool recursive = request.Data?["recursive"]?.Value<bool?>() ?? false;
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
        if (recursive)
            baseKey.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        else
            baseKey.DeleteSubKey(keyPath, throwOnMissingSubKey: false);

        ServiceHostLog.Write($@"HKLM registry key deleted: HKLM\{keyPath}; view={registryView}; recursive={recursive}");
        return ServiceHostResponse.FromObject(request.RequestId, true, "registry_key_deleted", new
        {
            hive = "HKEY_LOCAL_MACHINE",
            registryView = registryView.ToString(),
            keyPath,
            recursive,
        });
    }

    internal static LocalMachineRegistryMutation ParseMutation(ServiceHostRequest request)
    {
        string keyPath = NormalizeKeyPath(GetRequiredDataValue(request, "keyPath"));
        RegistryView registryView = ParseRegistryView(request);
        var values = new List<ServiceHostRegistryValue>();
        if (request.Data?["values"] is JArray valuesArray)
        {
            foreach (JToken item in valuesArray)
            {
                JToken? nameToken = item["name"];
                JToken? valueToken = item["value"];
                if (nameToken == null)
                    throw new InvalidOperationException("Missing registry value name.");
                if (valueToken == null || valueToken.Type == JTokenType.Null)
                    throw new InvalidOperationException("Missing registry value data.");

                string name = nameToken.ToString();
                ValidateValueName(name);
                string kindText = item["kind"]?.ToString() ?? string.Empty;
                if (!Enum.TryParse(kindText, ignoreCase: true, out RegistryValueKind kind)
                    || kind == RegistryValueKind.Unknown)
                {
                    throw new InvalidOperationException($"Unsupported registry value kind: {kindText}");
                }

                values.Add(new ServiceHostRegistryValue(name, kind, ConvertValue(valueToken, kind)));
            }
        }

        string[] deleteValueNames = request.Data?["deleteValueNames"] is JArray deleteArray
            ? deleteArray.Select(item => item.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        foreach (string valueName in deleteValueNames)
            ValidateValueName(valueName);

        if (values.Count == 0 && deleteValueNames.Length == 0)
            throw new InvalidOperationException("No registry value changes were requested.");

        return new LocalMachineRegistryMutation(keyPath, registryView, values, deleteValueNames);
    }

    internal static string NormalizeKeyPath(string value)
    {
        string keyPath = value.Trim().Replace('/', '\\').Trim('\\');
        const string fullHiveName = "HKEY_LOCAL_MACHINE";
        const string shortHiveName = "HKLM";
        if (keyPath.Equals(fullHiveName, StringComparison.OrdinalIgnoreCase)
            || keyPath.Equals(shortHiveName, StringComparison.OrdinalIgnoreCase))
        {
            keyPath = string.Empty;
        }
        else if (keyPath.StartsWith(fullHiveName + "\\", StringComparison.OrdinalIgnoreCase))
        {
            keyPath = keyPath[(fullHiveName.Length + 1)..];
        }
        else if (keyPath.StartsWith(shortHiveName + "\\", StringComparison.OrdinalIgnoreCase))
        {
            keyPath = keyPath[(shortHiveName.Length + 1)..];
        }
        else
        {
            int separatorIndex = keyPath.IndexOf('\\');
            string firstSegment = separatorIndex >= 0 ? keyPath[..separatorIndex] : keyPath;
            if (OtherHiveAliases.Contains(firstSegment))
                throw new InvalidOperationException("Only HKEY_LOCAL_MACHINE registry paths are supported.");
        }

        keyPath = keyPath.Trim('\\');
        if (string.IsNullOrWhiteSpace(keyPath) || keyPath.Contains('\0'))
            throw new InvalidOperationException("A non-empty HKLM subkey path is required.");
        return keyPath;
    }

    private static object ConvertValue(JToken token, RegistryValueKind kind)
    {
        return kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => token.ToString(),
            RegistryValueKind.DWord => ParseDWord(token),
            RegistryValueKind.QWord => ParseQWord(token),
            RegistryValueKind.MultiString => token.ToObject<string[]>()
                ?? throw new InvalidOperationException("Invalid MultiString registry value."),
            RegistryValueKind.Binary or RegistryValueKind.None => token.Type == JTokenType.String
                ? Convert.FromBase64String(token.ToString())
                : token.ToObject<byte[]>() ?? throw new InvalidOperationException("Invalid Binary registry value."),
            _ => throw new InvalidOperationException($"Unsupported registry value kind: {kind}"),
        };
    }

    private static RegistryView ParseRegistryView(ServiceHostRequest request)
    {
        string? value = request.Data?["registryView"]?.ToString();
        if (string.IsNullOrWhiteSpace(value)) return RegistryView.Default;
        if (!Enum.TryParse(value, ignoreCase: true, out RegistryView registryView)
            || registryView is not (RegistryView.Default or RegistryView.Registry32 or RegistryView.Registry64))
        {
            throw new InvalidOperationException($"Unsupported registry view: {value}");
        }

        return registryView;
    }

    private static int ParseDWord(JToken token)
    {
        string value = token.ToString();
        if (TryParseHex(value, out ulong hexValue) && hexValue <= uint.MaxValue)
            return unchecked((int)(uint)hexValue);
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int signedValue))
            return signedValue;
        if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint unsignedValue))
            return unchecked((int)unsignedValue);
        throw new InvalidOperationException($"Invalid DWord registry value: {value}");
    }

    private static long ParseQWord(JToken token)
    {
        string value = token.ToString();
        if (TryParseHex(value, out ulong hexValue))
            return unchecked((long)hexValue);
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long signedValue))
            return signedValue;
        if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong unsignedValue))
            return unchecked((long)unsignedValue);
        throw new InvalidOperationException($"Invalid QWord registry value: {value}");
    }

    private static bool TryParseHex(string value, out ulong parsedValue)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsedValue);

        parsedValue = 0;
        return false;
    }

    private static void ValidateValueName(string name)
    {
        if (name.Contains('\0'))
            throw new InvalidOperationException("Registry value names cannot contain NUL characters.");
    }

    private static string GetRequiredDataValue(ServiceHostRequest request, string name)
    {
        string? value = request.Data?[name]?.ToString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing request data: {name}");
        return value;
    }
}

internal sealed record ServiceHostRegistryValue(string Name, RegistryValueKind Kind, object Value);

internal sealed record LocalMachineRegistryMutation(
    string KeyPath,
    RegistryView RegistryView,
    IReadOnlyList<ServiceHostRegistryValue> Values,
    IReadOnlyList<string> DeleteValueNames);
