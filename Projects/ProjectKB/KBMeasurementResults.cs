using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProjectKB;

internal static class KBMeasurementResults
{
    private const string PixelCountProperty = "PixNumber";

    internal static IReadOnlyList<KBItem> ApplyLuminance(
        IReadOnlyList<KBItem> keys,
        IEnumerable<(ViewResultAlgType Type, PoiPointResultModel Point)> points,
        double luminanceScale)
    {
        var keysByIdentity = new Dictionary<(string Name, int? Width), KBItem>();
        foreach (KBItem key in keys)
            keysByIdentity.TryAdd((key.Name, key.KBKeyRect.Width), key);

        var measured = new HashSet<KBItem>();
        foreach ((ViewResultAlgType type, PoiPointResultModel point) in points)
        {
            if (point.PoiName == null || !keysByIdentity.TryGetValue((point.PoiName, point.PoiWidth), out KBItem? key))
                continue;

            LuminanceReading? reading = ReadLuminance(type, point.Value);
            KBKey? settings = key.KBKeyRect.KBKey;
            if (reading == null || reading.PixelCount <= 0 || settings == null)
                continue;

            double luminance = reading.Y * reading.PixelCount;
            if (settings.Area != 0)
                luminance /= settings.Area;
            luminance = settings.KeyScale * luminance * luminanceScale;
            if (!double.IsFinite(luminance))
                continue;

            key.Lv = luminance;
            measured.Add(key);
        }

        // Keep measurement presence separate from the numeric value, because zero is a valid reading.
        return keys.Where(key => !measured.Contains(key)).ToArray();
    }

    private static LuminanceReading? ReadLuminance(ViewResultAlgType type, string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            JToken value = JToken.Parse(payload);
            JObject? measurement = type switch
            {
                ViewResultAlgType.POI_Y => value as JObject,
                ViewResultAlgType.POI_Y_V2 when value is JArray { Count: 2 } pair => pair[0] as JObject,
                _ => null,
            };
            // Json.NET rounds floating-point JSON numbers when converting them to Int32.
            if (measurement?.Properties().Any(property =>
                property.Value.Type == JTokenType.Float
                && string.Equals(property.Name, PixelCountProperty, StringComparison.OrdinalIgnoreCase)
                && !double.IsInteger(property.Value.Value<double>())) == true)
            {
                return null;
            }
            return measurement?.ToObject<LuminanceReading>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class LuminanceReading
    {
        [JsonProperty(Required = Required.Always)]
        public double Y { get; set; }

        [JsonProperty(PixelCountProperty)]
        public int PixelCount { get; set; } = 1;
    }
}
