using ProjectARVRPro.Process;
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

        public static IReadOnlyList<ObjectiveTestResultMetric> Collect(ObjectiveTestResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            var metrics = new List<ObjectiveTestResultMetric>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (PropertyInfo property in typeof(ObjectiveTestResult)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(item => item.MetadataToken))
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
                if (value is IList list)
                {
                    for (int index = 0; index < list.Count; index++)
                    {
                        if (list[index] is object item)
                            CollectObject(item, $"{testName}{index + 1}", metrics, keys);
                    }
                }
                else
                {
                    CollectObject(value, testName, metrics, keys);
                }
            }

            CollectDynamicItems(result.DynamicTestResults, metrics, keys);
            CollectKeyedObjects(result.DynamicMTFHV058TestResults, metrics, keys);
            CollectKeyedObjects(result.MTFH07TestResults, metrics, keys);
            CollectKeyedObjects(result.MTFV07TestResults, metrics, keys);
            CollectKeyedObjects(result.LuminanceChromaticityTestResults, metrics, keys);
            CollectKeyedObjects(result.FieldOfViewTestResults, metrics, keys);
            CollectKeyedObjects(result.ChessboardTestResults, metrics, keys);
            CollectDynamicPois(result.DynamicPoixyuvDatas, metrics, keys);

            return metrics;
        }

        private static bool IsSeparatelyCollectedProperty(string propertyName)
        {
            return propertyName == nameof(ObjectiveTestResult.DynamicTestResults) ||
                   propertyName == nameof(ObjectiveTestResult.DynamicPoixyuvDatas) ||
                   propertyName == nameof(ObjectiveTestResult.DynamicScreenDefectResults) ||
                   propertyName == nameof(ObjectiveTestResult.DynamicMTFHV058TestResults) ||
                   propertyName == nameof(ObjectiveTestResult.MTFH07TestResults) ||
                   propertyName == nameof(ObjectiveTestResult.MTFV07TestResults) ||
                   propertyName == nameof(ObjectiveTestResult.LuminanceChromaticityTestResults) ||
                   propertyName == nameof(ObjectiveTestResult.FieldOfViewTestResults) ||
                   propertyName == nameof(ObjectiveTestResult.ChessboardTestResults);
        }

        private static bool ContainsKey<T>(IReadOnlyDictionary<string, T>? results, string key)
        {
            return results?.Keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase)) == true;
        }

        private static void CollectDynamicItems<TCollection>(
            IReadOnlyDictionary<string, TCollection>? results,
            ICollection<ObjectiveTestResultMetric> metrics,
            ISet<string> keys)
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
                        AddItem(result.Key, item.Name, item, metrics, keys);
                }
            }
        }

        private static void CollectKeyedObjects<T>(
            IReadOnlyDictionary<string, T>? results,
            ICollection<ObjectiveTestResultMetric> metrics,
            ISet<string> keys)
        {
            if (results == null)
                return;

            foreach (KeyValuePair<string, T> result in results)
            {
                if (result.Value is object value)
                    CollectObject(value, result.Key, metrics, keys);
            }
        }

        private static void CollectDynamicPois<TCollection>(
            IReadOnlyDictionary<string, TCollection>? results,
            ICollection<ObjectiveTestResultMetric> metrics,
            ISet<string> keys)
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
                        AddPoi(result.Key, poi, metrics, keys);
                }
            }
        }

        private static void CollectObject(
            object source,
            string testName,
            ICollection<ObjectiveTestResultMetric> metrics,
            ISet<string> keys)
        {
            foreach (PropertyInfo property in source.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(item => item.MetadataToken))
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
                    AddItem(testName, property.Name, item, metrics, keys);
                }
                else if (value is IEnumerable<PoixyuvData> pois)
                {
                    foreach (PoixyuvData? poi in pois)
                    {
                        if (poi != null)
                            AddPoi(testName, poi, metrics, keys);
                    }
                }
                else if (value != null &&
                         !property.PropertyType.IsValueType &&
                         property.PropertyType != typeof(string) &&
                         value is not IEnumerable)
                {
                    CollectObject(value, testName, metrics, keys);
                }
            }
        }

        private static void AddItem(
            string testName,
            string fallbackItemName,
            ObjectiveTestItem item,
            ICollection<ObjectiveTestResultMetric> metrics,
            ISet<string> keys)
        {
            string itemName = string.IsNullOrWhiteSpace(item.Name) ? fallbackItemName : item.Name;
            string value = string.IsNullOrWhiteSpace(item.TestValue)
                ? item.Value.ToString("R", CultureInfo.InvariantCulture)
                : item.TestValue;
            AddMetric(testName, itemName, value, metrics, keys);
        }

        private static void AddPoi(
            string testName,
            PoixyuvData poi,
            ICollection<ObjectiveTestResultMetric> metrics,
            ISet<string> keys)
        {
            string poiName = string.IsNullOrWhiteSpace(poi.Name) ? "POI" : poi.Name;
            AddMetric(testName, $"{poiName}(Lv)", poi.Y.ToString("R", CultureInfo.InvariantCulture), metrics, keys);
            AddMetric(testName, $"{poiName}(Cx)", poi.x.ToString("R", CultureInfo.InvariantCulture), metrics, keys);
            AddMetric(testName, $"{poiName}(Cy)", poi.y.ToString("R", CultureInfo.InvariantCulture), metrics, keys);
            AddMetric(testName, $"{poiName}(u')", poi.u.ToString("R", CultureInfo.InvariantCulture), metrics, keys);
            AddMetric(testName, $"{poiName}(v')", poi.v.ToString("R", CultureInfo.InvariantCulture), metrics, keys);
        }

        private static void AddMetric(
            string testName,
            string itemName,
            string value,
            ICollection<ObjectiveTestResultMetric> metrics,
            ISet<string> keys)
        {
            string normalizedTestName = string.IsNullOrWhiteSpace(testName) ? "Result" : testName.Trim();
            string normalizedItemName = string.IsNullOrWhiteSpace(itemName) ? "Item" : itemName.Trim();
            string key = normalizedTestName + KeySeparator + normalizedItemName;
            if (!keys.Add(key))
                return;

            metrics.Add(new ObjectiveTestResultMetric(
                key,
                $"{normalizedTestName}_{normalizedItemName}",
                value ?? string.Empty));
        }
    }
}
