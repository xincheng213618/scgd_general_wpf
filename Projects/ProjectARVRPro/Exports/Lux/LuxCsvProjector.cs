using ColorVision.Engine.FlowProcessing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.KeyedResults;
using ProjectARVRPro.Process.MTF.MTFHV048;
using ProjectARVRPro.Process.OpticCenter;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ProjectARVRPro.Exports.Lux;

internal static class LuxCsvProjector
{
    internal static IReadOnlyList<ObjectiveTestCsvRow> CollectRows(ObjectiveTestResult result, LuxCsvFormat format, IReadOnlyList<ProjectARVRReuslt>? flowResults = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var rows = new List<ObjectiveTestCsvRow>();
        Dictionary<string, JObject> chessboards = ReadChessboardSnapshots(flowResults);
        object? white = KeyedTestResultDictionary.TryGetValue(result.LuminanceChromaticityTestResults, "White", out var keyedWhite)
            ? keyedWhite : result.W255TestResult;
        object? fov = KeyedTestResultDictionary.TryGetValue(result.FieldOfViewTestResults, "White", out var keyedFov)
            ? keyedFov : result.W51TestResult;
        if (white != null)
            CollectWhite(white, fov, format, rows);
        else if (fov != null)
            AddObject(fov, "W255", format, rows);

        foreach (var chessboard in chessboards)
            CollectChessboard(chessboard.Key, chessboard.Value, rows);

        foreach (PropertyInfo property in typeof(ObjectiveTestResult).GetProperties())
        {
            if (property.Name is nameof(ObjectiveTestResult.W255TestResult) or nameof(ObjectiveTestResult.W51TestResult))
                continue;
            if (property.Name == nameof(ObjectiveTestResult.ChessboardTestResult)
                && (chessboards.ContainsKey("Chessboard") || KeyedTestResultDictionary.TryGetValue(result.ChessboardTestResults, "Chessboard", out _)))
                continue;
            if (property.Name == nameof(ObjectiveTestResult.DemuraTestResults))
                continue; // File payloads are not measurement rows.

            object? value = property.GetValue(result);
            if (value == null || property.PropertyType.IsValueType || value is string)
                continue;
            string screen = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name;
            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    string key = entry.Key.ToString() ?? string.Empty;
                    if (property.Name == nameof(ObjectiveTestResult.ChessboardTestResults) && chessboards.ContainsKey(key.Trim()))
                        continue;
                    if (KeyedTestResultDictionary.IsKey(key, "White")
                        && property.Name is nameof(ObjectiveTestResult.LuminanceChromaticityTestResults) or nameof(ObjectiveTestResult.FieldOfViewTestResults))
                        continue;
                    AddObject(entry.Value, MapScreen(key), format, rows);
                }
            }
            else if (value is IList list)
            {
                for (int index = 0; index < list.Count; index++)
                    AddObject(list[index], list[index] is MTFHV048TestResult ? "MTF" : $"{screen}{index + 1}", format, rows);
            }
            else
            {
                AddObject(value, value is OpticCenterTestResult center ? center.GetOutputKey() : screen, format, rows);
            }
        }
        return rows;
    }

    private static Dictionary<string, JObject> ReadChessboardSnapshots(IReadOnlyList<ProjectARVRReuslt>? results)
    {
        var snapshots = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
        if (results == null) return snapshots;
        foreach (var result in results.OrderBy(result => result.Id))
        {
            if (result.FlowStatus != FlowStatus.Completed || string.IsNullOrWhiteSpace(result.ViewResultJson)
                || result.ProcessTypeFullName is not ("ProjectARVRPro.Process.Chessboard.ChessboardProcess" or "ProjectARVRPro.Process.Chessboard.ChessboardDynamicProcess"))
                continue;
            try
            {
                var root = JObject.Parse(result.ViewResultJson);
                var data = root["ChessboardViewTestResult"] as JObject ?? root;
                if (data["PoixyuvDatas"] is not JArray { Count: > 0 }) continue;
                var config = string.IsNullOrWhiteSpace(result.ProcessConfigJson) ? new JObject() : JObject.Parse(result.ProcessConfigJson);
                string key = KeyedTestResultDictionary.NormalizeKey(config.Value<string>("Key") ?? config.Value<string>("Name"), "Chessboard");
                snapshots[key] = data;
            }
            catch (JsonException) { /* Older aggregate results remain available below. */ }
        }
        return snapshots;
    }

    private static void CollectChessboard(string key, JObject result, List<ObjectiveTestCsvRow> rows)
    {
        var points = (JArray)result["PoixyuvDatas"]!;
        string screen = key == "Chessboard" ? points.Count switch
        {
            16 => "Chessborad_4x4", 25 => "Chessborad_5x5", 35 or 36 => "Chessborad_6x6", _ => key
        } : key;
        int index = 0;
        foreach (var point in points)
        {
            index++;
            string name = point.Value<string>("Name") ?? $"P{index}";
            rows.Add(ItemRow(new ObjectiveTestItem { Value = point.Value<double?>("Y") ?? 0 }, screen, name + "(Lv)", "cd/m^2", 1));
        }
        ObjectiveTestItem? Read(string name) => result[name]?.ToObject<ObjectiveTestItem>();
        rows.Add(ItemRow(Read("AverageWhiteLuminance") ?? Read("AverageWhiteLunimance"), screen, "Average_White_Lunimance", "cd/m^2", 1));
        rows.Add(ItemRow(Read("AverageBlackLuminance") ?? Read("AverageBlackLunimance"), screen, "Average_Black_Lunimance", "cd/m^2", 1));
        rows.Add(ItemRow(Read("ChessboardContrast"), screen, "Contrast_radio", "", 1));
    }

    private static void CollectWhite(object source, object? fov, LuxCsvFormat format, List<ObjectiveTestCsvRow> rows)
    {
        var points = source.GetType().GetProperty("PoixyuvDatas")?.GetValue(source) as IEnumerable<PoixyuvData>;
        foreach (LuxCsvField field in LuxCsvSchema.White)
        {
            if (format == LuxCsvFormat.Current && !field.CurrentVisible && ReadItem(fov, field.PropertyName) == null)
                continue;
            string name = format == LuxCsvFormat.Legacy ? field.LegacyName : field.CurrentName;
            string unit = format == LuxCsvFormat.Legacy ? field.LegacyUnit : field.CurrentUnit;
            Match pointField = Regex.Match(field.PropertyName, "^P([1-9])(Lv|Cx|Cy|Cu|Cv)$");
            if (pointField.Success)
            {
                PoixyuvData? point = points?.FirstOrDefault(item => item.Name == "P" + pointField.Groups[1].Value);
                double? value = point == null ? null : pointField.Groups[2].Value switch
                {
                    "Lv" => point.Y, "Cx" => point.x, "Cy" => point.y, "Cu" => point.u, "Cv" => point.v, _ => null
                };
                rows.Add(!value.HasValue ? DefaultRow("W255", name, unit)
                    : double.IsFinite(value.Value)
                        ? new("W255", name, Number(value.Value), Number(value.Value), unit, "0", "0", "pass")
                        : InvalidRow("W255", name, unit));
                continue;
            }

            string property = field.PropertyName switch
            {
                "CenterLunimance" when source.GetType().GetProperty("CenterLuminance") != null => "CenterLuminance",
                "Center_Correlated_Color_Temperature" => "CenterCorrelatedColorTemperature",
                _ => field.PropertyName
            };
            ObjectiveTestItem? item = ReadItem(source, property);
            if (field.PropertyName.EndsWith("FieldOfViewAngle", StringComparison.Ordinal))
                item = ReadItem(fov, field.PropertyName) ?? item;
            rows.Add(ItemRow(item, "W255", name, unit, field.PropertyName == "LuminanceUniformity" ? 100 : 1));
        }
    }

    private static void AddObject(object? source, string screen, LuxCsvFormat format, List<ObjectiveTestCsvRow> rows)
    {
        if (source == null) return;
        if (source is MTFHV048TestResult)
        {
            foreach (LuxCsvField field in LuxCsvSchema.ArMtf)
            {
                string name = format == LuxCsvFormat.Legacy ? field.LegacyName : field.CurrentName;
                string unit = format == LuxCsvFormat.Legacy ? field.LegacyUnit : field.CurrentUnit;
                ObjectiveTestItem? item = ReadItem(source, field.PropertyName);
                Match average = Regex.Match(field.PropertyName, "^MTF(0F|0_4F|0_8F)_(Center|LeftUp|RightUp|RightDown|LeftDown)_(horizontal|Vertical)$");
                if (item == null && average.Success)
                {
                    string axis = average.Groups[3].Value == "horizontal" ? "H" : "V";
                    item = ReadItem(source, $"MTF_HV_{axis}_{average.Groups[2].Value}_{average.Groups[1].Value}");
                }
                // Legacy adds a '%' label without rescaling these raw MTF values.
                rows.Add(ItemRow(item, screen, name, unit, 1));
            }
            return;
        }
        if (source is OpticCenterTestResult center)
        {
            rows.Add(ItemRow(center.OptCenterXTilt, screen, "Tilt_x", "degree", 1));
            rows.Add(ItemRow(center.OptCenterYTilt, screen, "Tilt_y", "degree", 1));
            rows.Add(ItemRow(center.OptCenterRotation, screen, "Rotation", "degree", 1));
            return;
        }

        foreach (ObjectiveTestCsvRow row in ObjectiveTestCsvRowCollector.FromObject(source, screen))
        {
            string name = row.TestItem.Trim() switch
            {
                "Center_Lunimance" or "CenterLuminance" or "CenterLunimance" => "Center_Luminance",
                "Conlor_Uniformity(Δu'v'max)" or "ColorUniformity" => "Color_Uniformity(uv_max)",
                "Luminance_Uniformity(min/max*100%)" or "LuminanceUniformity" => "Luminance_Uniformity(min_max_100)",
                "HorizontalTVDistortion" => "SMIA_TV_Distortion_Horizontal",
                "VerticalTVDistortion" => "SMIA_TV_Distortion_Vertical",
                "ChessboardContrast" => "Contrast_radio",
                _ => row.TestItem
            };
            double factor = name == "Luminance_Uniformity(min_max_100)" ? 100 : 1;
            string lower = Scale(row.LowerLimit, factor);
            string upper = Scale(row.UpperLimit, factor);
            string status = row.TestResult;
            if (string.IsNullOrWhiteSpace(status) || status.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                lower = string.IsNullOrWhiteSpace(lower) ? "0" : lower;
                upper = string.IsNullOrWhiteSpace(upper) ? "0" : upper;
                status = double.TryParse(Scale(row.TestValue, factor), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                    && double.IsFinite(value)
                    && new ObjectiveTestItem { Value = value, LowLimit = double.Parse(lower, CultureInfo.InvariantCulture), UpLimit = double.Parse(upper, CultureInfo.InvariantCulture) }.TestResult
                    ? "pass" : "fail";
            }
            rows.Add(row with
            {
                TestItem = name,
                TestValue = Scale(row.TestValue, factor),
                MetricValue = Scale(row.TestValue, factor),
                Unit = row.Unit.Equals("None", StringComparison.OrdinalIgnoreCase) ? string.Empty : row.Unit,
                LowerLimit = lower,
                UpperLimit = upper,
                TestResult = status
            });
        }
    }

    private static string MapScreen(string key) => key.Trim() switch
    {
        "VRMTFH" or "VRMTFV" => "MTF", _ => key
    };

    private static ObjectiveTestItem? ReadItem(object? source, string property) =>
        source?.GetType().GetProperty(property)?.GetValue(source) as ObjectiveTestItem;

    private static ObjectiveTestCsvRow ItemRow(ObjectiveTestItem? item, string screen, string name, string unit, double factor)
    {
        if (item == null)
            return DefaultRow(screen, name, unit);
        if (!double.IsFinite(item.Value * factor) || !double.IsFinite(item.LowLimit * factor) || !double.IsFinite(item.UpLimit * factor))
            return InvalidRow(screen, name, unit);
        return new(screen, name, Number(item.Value * factor), Number(item.Value * factor), unit,
            Number(item.LowLimit * factor), Number(item.UpLimit * factor), item.TestResult ? "pass" : "fail");
    }

    // Archived LUX models initialize unpopulated items and their unconstrained limits to zero.
    private static ObjectiveTestCsvRow DefaultRow(string screen, string name, string unit) =>
        new(screen, name, "0", "0", unit, "0", "0", "pass");

    private static ObjectiveTestCsvRow InvalidRow(string screen, string name, string unit) =>
        new(screen, name, "", "", unit, "0", "0", "fail");

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Scale(string value, double factor) => factor == 1 ? value
        : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? Number(number * factor) : value;
}
