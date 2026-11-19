using Newtonsoft.Json;
using ProjectARVRPro.Process;
using System.Buffers;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ProjectARVRPro;

public sealed record ObjectiveTestCsvRow(
    string TestScreen,
    string TestItem,
    string TestValue,
    string MetricValue,
    string Unit,
    string LowerLimit,
    string UpperLimit,
    string TestResult)
{
    private static readonly SearchValues<char> CsvSpecialCharacters = SearchValues.Create(",\"\r\n");

    public string MetricKey => TestScreen + ObjectiveTestResultMetricCollector.KeySeparator + TestItem;
    public string MetricHeader => $"{TestScreen}_{TestItem}";

    public string ToCsvLine() => FormatCsvLine(TestScreen, TestItem, TestValue, Unit, LowerLimit, UpperLimit, TestResult);

    internal static string FormatCsvLine(params string?[] fields) => string.Join(",", fields.Select(EscapeCsvField));

    internal static string EscapeCsvField(string? field)
    {
        if (field == null)
            return string.Empty;

        return field.AsSpan().IndexOfAny(CsvSpecialCharacters) < 0 ? field : $"\"{field.Replace("\"", "\"\"")}\"";
    }
}

public static class ObjectiveTestCsvRowCollector
{
    public static IReadOnlyList<ObjectiveTestCsvRow> FromJson<TResult>(string? json, string testScreen)
        where TResult : class
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(testScreen))
            return Array.Empty<ObjectiveTestCsvRow>();

        TResult? result;
        try
        {
            result = JsonConvert.DeserializeObject<TResult>(json);
        }
        catch (JsonException)
        {
            return Array.Empty<ObjectiveTestCsvRow>();
        }

        return FromObject(result, testScreen);
    }

    internal static IReadOnlyList<ObjectiveTestCsvRow> FromReader<TResult>(TextReader text, string testScreen)
        where TResult : class
    {
        if (JsonConvert.DefaultSettings != null)
            return FromJson<TResult>(text.ReadToEnd(), testScreen);

        using var json = new JsonTextReader(text) { CloseInput = false };
        var serializer = JsonSerializer.Create();
        serializer.CheckAdditionalContent = true;
        TResult? result;
        try
        {
            result = serializer.Deserialize<TResult>(json);
        }
        catch (JsonException)
        {
            result = null;
        }

        // A JSON error or early parser exit must still surface corruption in the compressed tail.
        Span<char> buffer = stackalloc char[1024];
        while (text.Read(buffer) > 0) { }
        return FromObject(result, testScreen);
    }

    internal static IReadOnlyList<ObjectiveTestCsvRow> FromObject(object? result, string testScreen)
    {
        if (result == null || string.IsNullOrWhiteSpace(testScreen))
            return Array.Empty<ObjectiveTestCsvRow>();

        var rows = new List<ObjectiveTestCsvRow>();
        Collect(result, testScreen.Trim(), rows);
        return rows;
    }

    private static void Collect(object source, string testScreen, ICollection<ObjectiveTestCsvRow> rows)
    {
        if (source is IEnumerable<ObjectiveTestItem> rootItems)
        {
            foreach (ObjectiveTestItem? item in rootItems)
            {
                if (item != null)
                    rows.Add(CreateItemRow(testScreen, item.Name, item));
            }
            return;
        }

        if (source is IEnumerable<PoixyuvData> rootPois)
        {
            foreach (PoixyuvData? poi in rootPois)
            {
                if (poi != null)
                    AddPoiRows(testScreen, poi, rows);
            }
            return;
        }

        // Dynamic processes persist the exact objective output in Items. Prefer it over
        // walking compatibility/detail properties so the same metric is not exported twice.
        PropertyInfo? itemsProperty = source.GetType().GetProperty("Items", BindingFlags.Public | BindingFlags.Instance);
        if (itemsProperty?.GetValue(source) is IEnumerable<ObjectiveTestItem> directItems)
        {
            foreach (ObjectiveTestItem? item in directItems)
            {
                if (item != null)
                    rows.Add(CreateItemRow(testScreen, item.Name, item));
            }
            return;
        }

        ResultPropertySet properties = ResultPropertySet.For(source.GetType());
        foreach (PropertyInfo property in properties.OrderedProperties)
        {
            object? value;
            try
            {
                value = property.GetValue(source);
            }
            catch
            {
                continue;
            }

            if (value is ObjectiveTestItem item)
            {
                rows.Add(CreateItemRow(testScreen, property.Name, item));
            }
            else if (value is IEnumerable<ObjectiveTestItem> items)
            {
                foreach (ObjectiveTestItem? collectionItem in items)
                {
                    if (collectionItem != null)
                        rows.Add(CreateItemRow(testScreen, collectionItem.Name, collectionItem));
                }
            }
            else if (value is IEnumerable<PoixyuvData> pois)
            {
                string poiTestScreen = properties.HasMultiplePoiCollections
                    ? $"{testScreen}_{GetPoiGroupName(property.Name)}"
                    : testScreen;
                foreach (PoixyuvData? poi in pois)
                {
                    if (poi != null)
                        AddPoiRows(poiTestScreen, poi, rows);
                }
            }
            else if (value != null &&
                     !property.PropertyType.IsValueType &&
                     property.PropertyType != typeof(string) &&
                     value is not IEnumerable)
            {
                Collect(value, testScreen, rows);
            }
        }
    }

    private static ObjectiveTestCsvRow CreateItemRow(string testScreen, string fallbackItemName, ObjectiveTestItem item)
    {
        string itemName = string.IsNullOrWhiteSpace(item.Name) ? fallbackItemName : item.Name;
        string formattedValue = item.Value.ToString("R", CultureInfo.InvariantCulture);
        string metricValue = string.IsNullOrWhiteSpace(item.TestValue)
            ? formattedValue
            : item.TestValue;
        return new ObjectiveTestCsvRow(
            testScreen,
            itemName,
            formattedValue,
            metricValue,
            item.Unit ?? string.Empty,
            item.LowLimit.ToString("R", CultureInfo.InvariantCulture),
            item.UpLimit.ToString("R", CultureInfo.InvariantCulture),
            item.TestResult ? "pass" : "fail");
    }

    private static void AddPoiRows(string testScreen, PoixyuvData poi, ICollection<ObjectiveTestCsvRow> rows)
    {
        string poiName = string.IsNullOrWhiteSpace(poi.Name) ? "POI" : poi.Name;
        AddPoiRow(testScreen, $"{poiName}(Lv)", poi.Y, "cd/m2", rows);
        AddPoiRow(testScreen, $"{poiName}(Cx)", poi.x, "None", rows);
        AddPoiRow(testScreen, $"{poiName}(Cy)", poi.y, "None", rows);
        AddPoiRow(testScreen, $"{poiName}(u')", poi.u, "None", rows);
        AddPoiRow(testScreen, $"{poiName}(v')", poi.v, "None", rows);
    }

    private static void AddPoiRow(string testScreen, string itemName, double value, string unit, ICollection<ObjectiveTestCsvRow> rows)
    {
        string formattedValue = value.ToString("R", CultureInfo.InvariantCulture);
        rows.Add(new ObjectiveTestCsvRow(testScreen, itemName, formattedValue, formattedValue, unit, "0", "0", "None"));
    }

    private static string GetPoiGroupName(string propertyName)
    {
        const string prefix = "PoixyuvDatas";
        return propertyName.StartsWith(prefix, StringComparison.Ordinal)
            ? propertyName[prefix.Length..]
            : propertyName;
    }
}

internal sealed class ResultPropertySet
{
    private static readonly ConditionalWeakTable<Type, ResultPropertySet> Cache = new();

    internal PropertyInfo[] OrderedProperties { get; }
    internal bool HasMultiplePoiCollections { get; }

    private ResultPropertySet(Type type)
    {
        OrderedProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(property => property.MetadataToken)
            .ToArray();
        HasMultiplePoiCollections = OrderedProperties.Count(property =>
            typeof(IEnumerable<PoixyuvData>).IsAssignableFrom(property.PropertyType)) > 1;
    }

    internal static ResultPropertySet For(Type type) => Cache.GetValue(type, static key => new ResultPropertySet(key));
}
