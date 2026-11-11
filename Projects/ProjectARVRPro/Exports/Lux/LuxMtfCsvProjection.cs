using ColorVision.Engine.FlowProcessing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.MTF.MTFHV048;
using ProjectARVRPro.Process.MTF.MTFHV058;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ProjectARVRPro.Exports.Lux;

internal sealed record LuxMtfCsvSnapshot(object Config, JArray Points);

internal static class LuxMtfCsvProjection
{
    internal static Dictionary<(Type ResultType, string? OutputKey, int Position), LuxMtfCsvSnapshot> ReadSnapshots(IReadOnlyList<ProjectARVRReuslt>? results)
    {
        var snapshots = new Dictionary<(Type, string?, int), LuxMtfCsvSnapshot>();
        if (results == null) return snapshots;
        int unnamed058Index = 0, index048 = 0;
        foreach (var result in results.OrderBy(result => result.Id))
        {
            bool is058 = result.ProcessTypeFullName == typeof(MTFHV058Process).FullName;
            bool is048 = result.ProcessTypeFullName == typeof(MTFHV048Process).FullName;
            if (result.FlowStatus != FlowStatus.Completed || (!is058 && !is048))
                continue;
            try
            {
                object config;
                (Type, string?, int) key;
                if (is058)
                {
                    var typedConfig = string.IsNullOrWhiteSpace(result.ProcessConfigJson) ? new MTFHV058ProcessConfig()
                        : JsonConvert.DeserializeObject<MTFHV058ProcessConfig>(result.ProcessConfigJson) ?? new();
                    config = typedConfig;
                    string? outputKey = typedConfig.Name?.Trim();
                    key = string.IsNullOrEmpty(outputKey)
                        ? (typeof(MTFHV058TestResult), null, ++unnamed058Index)
                        : (typeof(MTFHV058TestResult), outputKey, 0);
                }
                else
                {
                    config = string.IsNullOrWhiteSpace(result.ProcessConfigJson) ? new MTFHV048ProcessConfig()
                        : JsonConvert.DeserializeObject<MTFHV048ProcessConfig>(result.ProcessConfigJson) ?? new();
                    key = (typeof(MTFHV048TestResult), null, ++index048);
                }
                snapshots.Remove(key); // A newer snapshot must never reuse older raw components.
                var data = string.IsNullOrWhiteSpace(result.ViewResultJson) ? new JObject() : JObject.Parse(result.ViewResultJson);
                var points = data["MTFDetailViewReslut"] is JObject detail && detail["MTFResult"] is JObject mtf
                    && mtf["resultChild"] is JArray children ? children : new JArray();
                snapshots[key] = new(config, points);
            }
            catch (JsonException) { /* Aggregate averages remain available for older or invalid snapshots. */ }
        }
        return snapshots;
    }

    internal static IEnumerable<(LuxCsvField Field, ObjectiveTestItem? Item)> Project(object result, LuxMtfCsvSnapshot? snapshot)
    {
        foreach (LuxCsvField field in LuxCsvSchema.ArMtf)
        {
            Match match = Regex.Match(field.PropertyName, "^MTF(0F|0_4F|0_8F)_(Center|LeftUp|RightUp|RightDown|LeftDown)_(H1|V1|H2|V2|horizontal|Vertical)$");
            string fieldOfView = result is MTFHV058TestResult ? match.Groups[1].Value.Replace("0_4F", "0_5F", StringComparison.Ordinal) : match.Groups[1].Value;
            string position = match.Groups[2].Value;
            string component = match.Groups[3].Value;
            if (component is "horizontal" or "Vertical")
            {
                string axis = component == "horizontal" ? "H" : "V";
                yield return (field, result.GetType().GetProperty($"MTF_HV_{axis}_{position}_{fieldOfView}")?.GetValue(result) as ObjectiveTestItem);
                continue;
            }

            // Read the actual process's point keys; CSV naming never changes which point is sampled.
            // Archived LUX raw child order is V1, H1, V2, H2.
            string? parseKey = snapshot?.Config.GetType().GetProperty($"Key_{position}_{fieldOfView}")?.GetValue(snapshot.Config) as string;
            var point = snapshot?.Points.OfType<JObject>().FirstOrDefault(point => point.Value<string>("name") == parseKey);
            int index = component switch { "V1" => 0, "H1" => 1, "V2" => 2, "H2" => 3, _ => -1 };
            JToken? value = point?["childRects"] is JArray rects && index >= 0 && index < rects.Count && rects[index] is JObject rect
                ? rect["mtfValue"] : null;
            yield return (field, double.TryParse(Convert.ToString((value as JValue)?.Value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                ? new ObjectiveTestItem { Value = number } : null);
        }
    }
}
