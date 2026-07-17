#pragma warning disable CS0168
using ProjectARVRPro.Process;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace ProjectARVRPro
{
    public static class ObjectiveTestResultCsvExporter
    {
        private static void CollectRows(object obj, string testScreenName, List<string> rows)
        {
            foreach (var property in obj.GetType().GetProperties())
            {
                if (property.PropertyType == typeof(ObjectiveTestItem))
                {
                    var testItem = (ObjectiveTestItem)property.GetValue(obj);
                    if (testItem != null)
                    {
                        rows.Add(FormatCsvRow(testScreenName, property.Name, testItem));
                    }
                }
                else if (property.PropertyType == typeof(List<PoixyuvData>))
                {
                    try
                    {
                        var list = (List<PoixyuvData>)property.GetValue(obj);
                        if (list != null)
                        {
                            foreach (var item in list) 
                            {
                                rows.Add($"{testScreenName},{item.Name}(Lv),{item.Y},cd/m2,0,0,None");
                                rows.Add($"{testScreenName},{item.Name}(Cx),{item.x},None,0,0,None");
                                rows.Add($"{testScreenName},{item.Name}(Cy),{item.y},None,0,0,None");
                                rows.Add($"{testScreenName},{item.Name}(u'),{item.u},None,0,0,None");
                                rows.Add($"{testScreenName},{item.Name}(v'),{item.v},None,0,0,None");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
                else if (!property.PropertyType.IsValueType && property.PropertyType != typeof(string))
                {
                    try
                    {                    // Recursively process child objects
                        var childObj = property.GetValue(obj);
                        if (childObj != null)
                        {
                            CollectRows(childObj, testScreenName, rows);
                        }

                    }
                    catch (Exception ex)
                    {

                    }
                }

            }
        }
        /// <summary>
        /// 处理集合类型，为每个元素添加序号后缀
        /// </summary>
        private static void CollectRowsFromList(IList list, string baseTestScreenName, List<string> rows)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item != null)
                {
                    // 生成带序号的名称，如 MTF1, MTF2, MTF3... 
                    string indexedName = $"{baseTestScreenName}{i + 1}";
                    CollectRows(item, indexedName, rows);
                }
            }
        }


        private static string FormatCsvRow(string testScreenName, string propertyName, ObjectiveTestItem testItem)
        {
            string testResult = testItem.TestResult ? "pass" : "fail";
            string testItemName = string.IsNullOrWhiteSpace(testItem.Name) ? propertyName : testItem.Name;
            return $"{testScreenName},{testItemName},{testItem.Value},{testItem.Unit},{testItem.LowLimit},{testItem.UpLimit},{testResult}";
        }

        public static void ExportToCsv(ObjectiveTestResult results, string filePath)
        {
            var rows = new List<string> { "Test_Screen,Test_item,Test_Value,unit,lower_limit,upper_limit,Test_Result" };
            foreach (var prop in results.GetType().GetProperties())
            {
                // Skip dynamic dictionaries - handled separately below
                if (prop.Name == nameof(ObjectiveTestResult.DynamicTestResults) ||
                    prop.Name == nameof(ObjectiveTestResult.DynamicPoixyuvDatas) ||
                    prop.Name == nameof(ObjectiveTestResult.DynamicScreenDefectResults) ||
                    prop.Name == nameof(ObjectiveTestResult.DynamicMTFHV058TestResults) ||
                    prop.Name == nameof(ObjectiveTestResult.LuminanceChromaticityTestResults))
                    continue;

                if (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
                {

                    var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
                    var raw = displayNameAttr?.DisplayName ?? prop.Name;

                    // Recursively process child objects
                    var childObj = prop.GetValue(results);
                    if (childObj != null)
                    {
                        // 检查是否为泛型集合类型 (如 List<T>)
                        if (childObj is IList list && prop.PropertyType.IsGenericType)
                        {
                            CollectRowsFromList(list, raw, rows);
                        }
                        else
                        {
                            // 非集合类型，保持原有逻辑
                            CollectRows(childObj, raw, rows);
                        }
                    }
                }
            }

            // Export dynamic test results from dictionary
            if (results.DynamicTestResults != null)
            {
                foreach (var kvp in results.DynamicTestResults)
                {
                    string testScreenName = kvp.Key;
                    foreach (var testItem in kvp.Value)
                    {
                        if (testItem != null)
                        {
                            rows.Add(FormatCsvRow(testScreenName, testItem.Name, testItem));
                        }
                    }
                }
            }

            if (results.DynamicMTFHV058TestResults != null)
            {
                foreach (var kvp in results.DynamicMTFHV058TestResults)
                {
                    if (kvp.Value != null)
                        CollectRows(kvp.Value, kvp.Key, rows);
                }
            }

            if (results.LuminanceChromaticityTestResults != null)
            {
                foreach (var kvp in results.LuminanceChromaticityTestResults)
                {
                    if (kvp.Value != null)
                        CollectRows(kvp.Value, kvp.Key, rows);
                }
            }

            if (results.DynamicPoixyuvDatas != null)
            {
                foreach (var kvp in results.DynamicPoixyuvDatas)
                {
                    string testScreenName = kvp.Key;
                    foreach (var item in kvp.Value)
                    {
                        rows.Add($"{testScreenName},{item.Name}(Lv),{item.Y},cd/m2,0,0,None");
                        rows.Add($"{testScreenName},{item.Name}(Cx),{item.x},None,0,0,None");
                        rows.Add($"{testScreenName},{item.Name}(Cy),{item.y},None,0,0,None");
                        rows.Add($"{testScreenName},{item.Name}(u'),{item.u},None,0,0,None");
                        rows.Add($"{testScreenName},{item.Name}(v'),{item.v},None,0,0,None");
                    }
                }
            }

            File.WriteAllLines(filePath, rows);
        }
    }


}
