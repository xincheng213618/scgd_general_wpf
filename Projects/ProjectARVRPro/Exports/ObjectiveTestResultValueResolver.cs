using ProjectARVRPro.Process;
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

            ObjectiveTestItem? mtf058Item = FindDynamicMTFHV058(testScreenName, itemName);
            if (mtf058Item != null)
                return mtf058Item;

            ObjectiveTestItem? luminanceChromaticityItem = FindLuminanceChromaticity(testScreenName, itemName);
            if (luminanceChromaticityItem != null)
                return luminanceChromaticityItem;

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

        private ObjectiveTestItem? FindDynamicMTFHV058(string testScreenName, string itemName)
        {
            if (_result.DynamicMTFHV058TestResults == null)
                return null;

            if (!_result.DynamicMTFHV058TestResults.TryGetValue(testScreenName, out var result))
                return null;

            return FindItem(result, itemName);
        }

        private ObjectiveTestItem? FindLuminanceChromaticity(string testScreenName, string itemName)
        {
            if (_result.LuminanceChromaticityTestResults == null)
                return null;

            if (!_result.LuminanceChromaticityTestResults.TryGetValue(testScreenName, out var result))
                return null;

            return FindItem(result, itemName);
        }

        private object? FindScreenObject(string testScreenName)
        {
            foreach (PropertyInfo property in typeof(ObjectiveTestResult).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name == nameof(ObjectiveTestResult.DynamicTestResults) ||
                    property.Name == nameof(ObjectiveTestResult.DynamicPoixyuvDatas) ||
                    property.Name == nameof(ObjectiveTestResult.DynamicMTFHV058TestResults) ||
                    property.Name == nameof(ObjectiveTestResult.LuminanceChromaticityTestResults))
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
