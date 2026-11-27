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
    internal static bool IsSnapshotSource(ProjectARVRReuslt result) => result.FlowStatus == FlowStatus.Completed
        && (result.ProcessTypeFullName == typeof(MTFHV058Process).FullName || result.ProcessTypeFullName == typeof(MTFHV048Process).FullName);

    internal static Dictionary<(Type ResultType, string? OutputKey, int Position), LuxMtfCsvSnapshot> ReadSnapshots(IReadOnlyList<ProjectARVRReuslt>? results)
    {
        var snapshots = new Dictionary<(Type, string?, int), LuxMtfCsvSnapshot>();
        if (results == null) return snapshots;
        int unnamed058Index = 0, index048 = 0;
        foreach (var result in results.OrderBy(result => result.Id))
        {
            if (!IsSnapshotSource(result))
                continue;
            try
            {
                object config;
                (Type, string?, int) key;
                if (result.ProcessTypeFullName == typeof(MTFHV058Process).FullName)
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
        bool is058 = result is MTFHV058TestResult;
        Type resultType = result.GetType();
        string? currentPointProperty = null;
        JArray? currentRects = null;
        foreach (ProjectionField binding in ProjectionFields.Fields)
        {
            string property = is058 ? binding.Property058 : binding.Property048;
            if (binding.ComponentIndex < 0)
            {
                yield return (binding.Field, resultType.GetProperty(property)?.GetValue(result) as ObjectiveTestItem);
                continue;
            }

            if (snapshot != null && property != currentPointProperty)
            {
                currentPointProperty = property;
                // All four components use the first archived point matching the process's configured key.
                string? parseKey = snapshot.Config.GetType().GetProperty(property)?.GetValue(snapshot.Config) as string;
                var point = snapshot.Points.OfType<JObject>().FirstOrDefault(point => point.Value<string>("name") == parseKey);
                currentRects = point?["childRects"] as JArray;
            }

            int index = binding.ComponentIndex;
            JToken? value = currentRects is { } rects && index < rects.Count && rects[index] is JObject rect
                ? rect["mtfValue"] : null;
            yield return (binding.Field, double.TryParse(Convert.ToString((value as JValue)?.Value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                ? new ObjectiveTestItem { Value = number } : null);
        }
    }

    private readonly record struct ProjectionField(LuxCsvField Field, string Property048, string Property058, int ComponentIndex);

    private static class ProjectionFields
    {
        internal static readonly ProjectionField[] Fields = LuxCsvSchema.ArMtf.Select(Create).ToArray();

        private static ProjectionField Create(LuxCsvField field)
        {
            Match match = Regex.Match(field.PropertyName, "^MTF(0F|0_4F|0_8F)_(Center|LeftUp|RightUp|RightDown|LeftDown)_(H1|V1|H2|V2|horizontal|Vertical)$");
            string fieldOfView = match.Groups[1].Value;
            string position = match.Groups[2].Value;
            string component = match.Groups[3].Value;
            // Archived LUX raw child order is V1, H1, V2, H2.
            int index = component switch { "V1" => 0, "H1" => 1, "V2" => 2, "H2" => 3, _ => -1 };
            string property = index >= 0
                ? $"Key_{position}_{fieldOfView}"
                : $"MTF_HV_{(component == "horizontal" ? "H" : "V")}_{position}_{fieldOfView}";
            return new(field, property, property.Replace("0_4F", "0_5F", StringComparison.Ordinal), index);
        }
    }
}
