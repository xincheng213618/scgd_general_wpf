using ProjectARVRPro.Process.KeyedResults;
using System.Collections;
using System.Reflection;

namespace ProjectARVRPro.Exports
{
    public sealed class ObjectiveTestResultValueResolver
    {
        private readonly ObjectiveTestResult _result;

        public ObjectiveTestResultValueResolver(ObjectiveTestResult result)
        {
            _result = result;
        }

        public ObjectiveTestItem? Find(string testScreenName, string itemName)
        {
            if (string.IsNullOrWhiteSpace(testScreenName) || string.IsNullOrWhiteSpace(itemName))
                return null;

            ObjectiveTestItem? dynamicItem = FindDynamic(testScreenName, itemName);
            if (dynamicItem != null)
                return dynamicItem;

            ObjectiveTestItem? keyedItem =
                FindKeyedResult(_result.FieldOfViewTestResults, testScreenName, itemName) ??
                FindKeyedResult(_result.LuminanceChromaticityTestResults, testScreenName, itemName) ??
                FindKeyedResult(_result.DynamicMTFHV058TestResults, testScreenName, itemName);
            if (keyedItem != null)
                return keyedItem;

            object? screen = FindScreenObject(testScreenName);
            return FindItem(screen, itemName);
        }

        public ObjectiveTestItem? FindAny(string testScreenName, params string[] itemNames)
        {
            foreach (string itemName in itemNames)
            {
                ObjectiveTestItem? item = Find(testScreenName, itemName);
                if (item != null)
                    return item;
            }

            return null;
        }

        private ObjectiveTestItem? FindDynamic(string testScreenName, string itemName)
        {
            if (_result.DynamicTestResults == null)
                return null;

            if (!_result.DynamicTestResults.TryGetValue(testScreenName, out var items))
                return null;

            return items.FirstOrDefault(item =>
                string.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase));
        }

        private static ObjectiveTestItem? FindKeyedResult<T>(IReadOnlyDictionary<string, T>? results, string testScreenName, string itemName)
        {
            if (!KeyedTestResultDictionary.TryGetValue(results, testScreenName, out T? result))
                return null;

            return FindItem(result, itemName);
        }

        private object? FindScreenObject(string testScreenName)
        {
            foreach (PropertyInfo property in typeof(ObjectiveTestResult).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name == nameof(ObjectiveTestResult.DynamicTestResults) ||
                    property.Name == nameof(ObjectiveTestResult.DynamicPoixyuvDatas) ||
                    property.Name == nameof(ObjectiveTestResult.DynamicScreenDefectResults) ||
                    property.Name == nameof(ObjectiveTestResult.DynamicMTFHV058TestResults) ||
                    property.Name == nameof(ObjectiveTestResult.LuminanceChromaticityTestResults) ||
                    property.Name == nameof(ObjectiveTestResult.FieldOfViewTestResults))
                    continue;

                string displayName = property.GetCustomAttributes(false)
                    .OfType<System.ComponentModel.DisplayNameAttribute>()
                    .FirstOrDefault()?.DisplayName ?? property.Name;

                if (!string.Equals(displayName, testScreenName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(property.Name, testScreenName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                object? value = property.GetValue(_result);
                if (value is IList list && list.Count > 0)
                    return list[0];

                return value;
            }

            return null;
        }

        private static ObjectiveTestItem? FindItem(object? source, string itemName)
        {
            if (source == null)
                return null;

            foreach (PropertyInfo property in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.PropertyType == typeof(ObjectiveTestItem))
                {
                    ObjectiveTestItem? item = property.GetValue(source) as ObjectiveTestItem;
                    if (item == null)
                        continue;

                    if (string.Equals(property.Name, itemName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }
                }
            }

            return null;
        }
    }
}
