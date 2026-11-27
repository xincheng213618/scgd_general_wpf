using ProjectARVRPro.Process;
using ProjectARVRPro.Process.OpticCenter;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace ProjectARVRPro
{
    public sealed record ObjectiveTestResultMetric(string Key, string Header, string Value);

    public static class ObjectiveTestResultMetricCollector
    {
        public const string KeySeparator = "\u001F";
        private static readonly PropertyInfo[] ObjectiveProperties = ResultPropertySet.For(typeof(ObjectiveTestResult)).OrderedProperties;

        public static IReadOnlyList<ObjectiveTestResultMetric> Collect(ObjectiveTestResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            var metrics = new List<ObjectiveTestResultMetric>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            VisitMetrics(result, (testName, itemName, value) =>
            {
                string key = testName + KeySeparator + itemName;
                if (keys.Add(key))
                    metrics.Add(new ObjectiveTestResultMetric(key, $"{testName}_{itemName}", value));
            });
            return metrics;
        }

        internal static void CollectValues(ObjectiveTestResult result, Dictionary<string, string> values)
        {
            VisitMetrics(result, (testName, itemName, value) =>
                values.TryAdd(testName + KeySeparator + itemName, value));
        }

        private static void VisitMetrics(ObjectiveTestResult result, Action<string, string, string> addMetric)
        {
            foreach (PropertyInfo property in ObjectiveProperties)
            {
                if (IsSeparatelyCollectedProperty(property.Name))
                    continue;

                if (property.Name == nameof(ObjectiveTestResult.W255TestResult) &&
                    ContainsKey(result.LuminanceChromaticityTestResults, "White"))
                {
                    continue;
                }

                if (property.Name == nameof(ObjectiveTestResult.W51TestResult) &&
                    ContainsKey(result.FieldOfViewTestResults, "White"))
                {
                    continue;
                }

                if (property.Name == nameof(ObjectiveTestResult.ChessboardTestResult) &&
                    ContainsKey(result.ChessboardTestResults, "Chessboard"))
                {
                    continue;
                }

                object? value = property.GetValue(result);
                if (value == null || property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                    continue;

                string testName = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name;
                if (value is OpticCenterTestResult center)
                    testName = center.GetOutputKey();
                if (value is IList list)
                {
                    for (int index = 0; index < list.Count; index++)
                    {
                        if (list[index] is object item)
                            CollectObject(item, $"{testName}{index + 1}", addMetric);
                    }
                }
                else
                {
                    CollectObject(value, testName, addMetric);
                }
            }

            CollectDynamicItems(result.DynamicTestResults, addMetric);
            CollectKeyedObjects(result.DynamicMTFHV058TestResults, addMetric);
            CollectKeyedObjects(result.MTFH07TestResults, addMetric);
            CollectKeyedObjects(result.MTFV07TestResults, addMetric);
            CollectKeyedObjects(result.LuminanceChromaticityTestResults, addMetric);
            CollectKeyedObjects(result.LuminanceChromaticityYWTestResults, addMetric);
            CollectKeyedObjects(result.FieldOfViewTestResults, addMetric);
            CollectKeyedObjects(result.RingGhostTestResults, addMetric);
            CollectKeyedObjects(result.ChessboardTestResults, addMetric);
            CollectDynamicPois(result.DynamicPoixyuvDatas, addMetric);
            if (result.DynamicRgbCrossResults != null)
                foreach (var pair in result.DynamicRgbCrossResults)
                    if (pair.Value != null)
                        foreach (var point in pair.Value.Points)
                        {
                            if (point.Comparisons.Count == 0) RgbCrossResultParser.PopulateComparisons(point);
                            foreach (var comparison in point.Comparisons)
                                AddMetric(pair.Key, point.Id + "_" + comparison.Key,
                                    (comparison.Value.JudgedValue ?? comparison.Value.Value)?.ToString("R", CultureInfo.InvariantCulture) ?? "", addMetric);
                        }
        }

        private static bool IsSeparatelyCollectedProperty(string propertyName)
        {
            return propertyName == nameof(ObjectiveTestResult.DynamicTestResults) ||
                   propertyName == nameof(ObjectiveTestResult.DemuraTestResults) ||
                   propertyName == nameof(ObjectiveTestResult.DynamicPoixyuvDatas) ||
                   propertyName == nameof(ObjectiveTestResult.DynamicScreenDefectResults) ||
                   propertyName == nameof(ObjectiveTestResult.DynamicRgbCrossResults) ||
                   propertyName == nameof(ObjectiveTestResult.DynamicMTFHV058TestResults) ||
                   propertyName == nameof(ObjectiveTestResult.MTFH07TestResults) ||
                   propertyName == nameof(ObjectiveTestResult.MTFV07TestResults) ||
                   propertyName == nameof(ObjectiveTestResult.LuminanceChromaticityTestResults) ||
                   propertyName == nameof(ObjectiveTestResult.LuminanceChromaticityYWTestResults) ||
                   propertyName == nameof(ObjectiveTestResult.FieldOfViewTestResults) ||
                   propertyName == nameof(ObjectiveTestResult.RingGhostTestResults) ||
                   propertyName == nameof(ObjectiveTestResult.ChessboardTestResults);
        }

        private static bool ContainsKey<T>(IReadOnlyDictionary<string, T>? results, string key)
        {
            return results?.Keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase)) == true;
        }

        private static void CollectDynamicItems<TCollection>(
            IReadOnlyDictionary<string, TCollection>? results,
            Action<string, string, string> addMetric)
            where TCollection : IEnumerable<ObjectiveTestItem>
        {
            if (results == null)
                return;

            foreach (KeyValuePair<string, TCollection> result in results)
            {
                if (result.Value == null)
                    continue;

                foreach (ObjectiveTestItem? item in result.Value)
                {
                    if (item != null)
                        AddItem(result.Key, item.Name, item, addMetric);
                }
            }
        }

        private static void CollectKeyedObjects<T>(
            IReadOnlyDictionary<string, T>? results,
            Action<string, string, string> addMetric)
        {
            if (results == null)
                return;

            foreach (KeyValuePair<string, T> result in results)
            {
                if (result.Value is object value)
                    CollectObject(value, result.Key, addMetric);
            }
        }

        private static void CollectDynamicPois<TCollection>(
            IReadOnlyDictionary<string, TCollection>? results,
            Action<string, string, string> addMetric)
            where TCollection : IEnumerable<PoixyuvData>
        {
            if (results == null)
                return;

            foreach (KeyValuePair<string, TCollection> result in results)
            {
                if (result.Value == null)
                    continue;

                foreach (PoixyuvData? poi in result.Value)
                {
                    if (poi != null)
                        AddPoi(result.Key, poi, addMetric);
                }
            }
        }

        private static void CollectObject(
            object source,
            string testName,
            Action<string, string, string> addMetric)
        {
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
                    AddItem(testName, property.Name, item, addMetric);
                }
                else if (value is IEnumerable<PoixyuvData> pois)
                {
                    string poiTestName = properties.HasMultiplePoiCollections
                        ? $"{testName}_{GetPoiGroupName(property.Name)}"
                        : testName;
                    foreach (PoixyuvData? poi in pois)
                    {
                        if (poi != null)
                            AddPoi(poiTestName, poi, addMetric);
                    }
                }
                else if (value != null &&
                         !property.PropertyType.IsValueType &&
                         property.PropertyType != typeof(string) &&
                         value is not IEnumerable)
                {
                    CollectObject(value, testName, addMetric);
                }
            }
        }

        private static string GetPoiGroupName(string propertyName)
        {
            const string prefix = "PoixyuvDatas";
            return propertyName.StartsWith(prefix, StringComparison.Ordinal)
                ? propertyName[prefix.Length..]
                : propertyName;
        }

        private static void AddItem(
            string testName,
            string fallbackItemName,
            ObjectiveTestItem item,
            Action<string, string, string> addMetric)
        {
            string itemName = string.IsNullOrWhiteSpace(item.Name) ? fallbackItemName : item.Name;
            string value = string.IsNullOrWhiteSpace(item.TestValue)
                ? item.Value.ToString("R", CultureInfo.InvariantCulture)
                : item.TestValue;
            AddMetric(testName, itemName, value, addMetric);
        }

        private static void AddPoi(
            string testName,
            PoixyuvData poi,
            Action<string, string, string> addMetric)
        {
            string poiName = string.IsNullOrWhiteSpace(poi.Name) ? "POI" : poi.Name;
            AddMetric(testName, $"{poiName}(Lv)", poi.Y.ToString("R", CultureInfo.InvariantCulture), addMetric);
            AddMetric(testName, $"{poiName}(Cx)", poi.x.ToString("R", CultureInfo.InvariantCulture), addMetric);
            AddMetric(testName, $"{poiName}(Cy)", poi.y.ToString("R", CultureInfo.InvariantCulture), addMetric);
            AddMetric(testName, $"{poiName}(u')", poi.u.ToString("R", CultureInfo.InvariantCulture), addMetric);
            AddMetric(testName, $"{poiName}(v')", poi.v.ToString("R", CultureInfo.InvariantCulture), addMetric);
        }

        private static void AddMetric(
            string testName,
            string itemName,
            string value,
            Action<string, string, string> addMetric)
        {
            string normalizedTestName = string.IsNullOrWhiteSpace(testName) ? "Result" : testName.Trim();
            string normalizedItemName = string.IsNullOrWhiteSpace(itemName) ? "Item" : itemName.Trim();
            addMetric(normalizedTestName, normalizedItemName, value ?? string.Empty);
        }
    }
}
