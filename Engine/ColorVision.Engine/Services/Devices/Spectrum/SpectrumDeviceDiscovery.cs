using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    internal sealed record SpectrumDiscoveryResult(SpectrometerType Type, int ComPort, int? NativeResult, IReadOnlyList<string> SerialNumbers, string? Error);

    internal static class SpectrumDeviceDiscovery
    {
        // USB enumeration must remain available even when an old serial-port setting is present.
        // Gaolitong only supports USB enumeration; its COM port is used when connecting.
        internal static IReadOnlyList<SpectrumDiscoveryResult> Discover(int configuredComPort, Func<int, int, StringBuilder, int, int> query)
        {
            var results = new List<SpectrumDiscoveryResult>();
            foreach (SpectrometerType type in Enum.GetValues<SpectrometerType>())
            {
                Query(type, 0);
                if (configuredComPort > 0 && type != SpectrometerType.Gaolitong)
                    Query(type, configuredComPort);
            }
            return results;

            void Query(SpectrometerType type, int port)
            {
                int? nativeResult = null;
                try
                {
                    var buffer = new StringBuilder(4096);
                    nativeResult = query((int)type, port, buffer, buffer.Capacity);
                    results.Add(nativeResult == 1
                        ? new(type, port, nativeResult, ParseSerialNumbers(buffer.ToString()), null)
                        : new(type, port, nativeResult, Array.Empty<string>(), string.Format(Properties.Resources.SpectrumDiscoveryFailed, nativeResult)));
                }
                catch (Exception ex)
                {
                    // A missing or failing vendor driver must not hide devices from the other drivers.
                    results.Add(new(type, port, nativeResult, Array.Empty<string>(), ex.Message));
                }
            }
        }

        internal static IReadOnlyList<string> ParseSerialNumbers(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            JToken token = JToken.Parse(raw);
            // The native contract is { "number": n, "ID": ["SN", ...] }.
            // The count is metadata, never a serial number.
            if (token is JObject obj)
                token = obj.GetValue("ID", StringComparison.OrdinalIgnoreCase) ?? new JArray();

            IEnumerable<JToken> values = token is JArray array ? array : new[] { token };
            return values.Where(value => value.Type == JTokenType.String)
                .Select(value => value.ToString().Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static string FormatResults(IReadOnlyList<SpectrumDiscoveryResult> results)
        {
            var text = new StringBuilder(Properties.Resources.SpectrumDiscoveryHint);
            foreach (SpectrumDiscoveryResult result in results)
            {
                text.AppendLine().AppendLine();
                text.Append(result.Type).Append(" · ").AppendLine(result.ComPort == 0 ? "USB" : $"COM{result.ComPort}");
                if (result.Error != null)
                    text.Append(result.Error);
                else if (result.SerialNumbers.Count == 0)
                    text.Append(Properties.Resources.NoDeviceDetected);
                else
                    text.AppendJoin(Environment.NewLine, result.SerialNumbers.Select(sn => string.Format(Properties.Resources.DeviceSerialNumber, sn)));
            }
            return text.ToString();
        }
    }
}
