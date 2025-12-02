using System.IO;
using System.Reflection;
using System.Text;

namespace ProjectARVRPro.Process
{
    public static class TestResultCsvHelper
    {
        /// <summary>
        /// Exports a TestResult object to CSV format
        /// Each ObjectiveTestItem property becomes a row
        /// </summary>
        public static void ExportToCsv<T>(T testResult, string filePath) where T : class
        {
            if (testResult == null) return;

            var rows = new List<string> { "Test_item,Test_Value,Value,Unit,Lower_Limit,Upper_Limit,Test_Result" };
            CollectRows(testResult, rows);
            
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllLines(filePath, rows, Encoding.UTF8);
        }

        private static void CollectRows(object obj, List<string> rows)
        {
            if (obj == null) return;

            foreach (var property in obj.GetType().GetProperties())
            {
                if (property.PropertyType == typeof(ObjectiveTestItem))
                {
                    var testItem = (ObjectiveTestItem)property.GetValue(obj);
                    if (testItem != null)
                    {
                        string testResult = testItem.TestResult ? "pass" : "fail";
                        rows.Add($"{testItem.Name},{testItem.TestValue},{testItem.Value},{testItem.Unit},{testItem.LowLimit},{testItem.UpLimit},{testResult}");
                    }
                }
            }
        }
    }
}
