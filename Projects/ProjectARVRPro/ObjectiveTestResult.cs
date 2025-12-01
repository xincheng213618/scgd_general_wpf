using ColorVision.Common.MVVM;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Process.Blue;
using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Process.Distortion;
using ProjectARVRPro.Process.Green;
using ProjectARVRPro.Process.MTFHV;
using ProjectARVRPro.Process.OpticCenter;
using ProjectARVRPro.Process.Red;
using ProjectARVRPro.Process.W255;
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

        private static string FormatCsvRow(string testScreenName, string propertyName, ObjectiveTestItem testItem)
        {
            string testResult = testItem.TestResult ? "pass" : "fail";
            return $"{testScreenName},{propertyName},{testItem.Value},{testItem.Unit},{testItem.LowLimit},{testItem.UpLimit},{testResult}";
        }

        public static void ExportToCsv(ObjectiveTestResult results, string filePath)
        {
            var rows = new List<string> { "Test_Screen,Test_item,Test_Value,unit,lower_limit,upper_limit,Test_Result" };
            foreach (var prop in results.GetType().GetProperties())
            {

                if (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
                {

                    var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
                    var raw = displayNameAttr?.DisplayName ?? prop.Name;

                    // Recursively process child objects
                    var childObj = prop.GetValue(results);
                    if (childObj != null)
                    {
                        CollectRows(childObj, raw, rows);
                    }
                }
            }

            File.WriteAllLines(filePath, rows);
        }
    }
    public class ObjectiveTestItem
    {
        public string Name { get; set; }         // 项目名称

        //这里有可能添加符号
        public string TestValue { get; set; }    // 测试值

        public double Value { get; set; }    // 测试值

        public double LowLimit { get; set; }     // 下限
        public double UpLimit { get; set; }      // 上限

        public string Unit { get; set; }         // 单位

        public bool TestResult {
            get
            {
                // 判断是否低于下限
                bool isAboveLowLimit = LowLimit == 0 || Value >= LowLimit;
                // 判断是否高于上限
                bool isBelowUpLimit = UpLimit == 0 || Value <= UpLimit;
                // 只有同时满足上下限才返回 true
                return isAboveLowLimit && isBelowUpLimit;
            } 
        }
    }



    /// <summary>
    /// 表示一组客观测试项的测试结果，每个属性对应一个具体的测试项目，包含测试值、上下限、结果等信息。
    /// </summary>
    public class ObjectiveTestResult:ViewModelBase
    {
        [DisplayName("W255")]
        public W255TestResult W255TestResult { get; set; }
        [DisplayName("Black")]
        public BlackTestResult BlackTestResult { get; set; }

        [DisplayName("R255")]
        public RedTestResult RedTestResult { get; set; }

        [DisplayName("G255")]
        public GreenTestResult GreenTestResult { get; set; }
        [DisplayName("B255")]
        public BlueTestResult BlueTestResult { get; set; }

        [DisplayName("Chessborad_7x7")]
        public ChessboardTestResult ChessboardTestResult { get; set; }

        [DisplayName("MTF")]
        public MTFHVTestResult MTFHVTestResult { get; set; }

        [DisplayName("Distortion")]
        public DistortionTestResult DistortionTestResult { get; set; }

        [DisplayName("Optical_Center")]
        public OpticCenterTestResult OpticCenterTestResult { get; set; }

        /// <summary>
        /// 中心点亮度
        /// </summary>
        public ObjectiveTestItem W25CenterLunimance { get; set; }
        /// <summary>
        /// CenterCIE1931ChromaticCoordinatesx
        /// </summary>
        public ObjectiveTestItem W25CenterCIE1931ChromaticCoordinatesx { get; set; }
        /// <summary>
        /// CenterCIE1931ChromaticCoordinatesy
        /// </summary>
        public ObjectiveTestItem W25CenterCIE1931ChromaticCoordinatesy { get; set; }
        /// <summary>
        /// CenterCIE1976ChromaticCoordinatesu
        /// </summary>
        public ObjectiveTestItem W25CenterCIE1976ChromaticCoordinatesu { get; set; }
        /// <summary>
        /// CenterCIE1976ChromaticCoordinatesv
        /// </summary>
        public ObjectiveTestItem W25CenterCIE1976ChromaticCoordinatesv { get; set; }

        /// <summary>
        /// 总体测试结果（true表示通过，false表示不通过）
        /// </summary>
        public bool TotalResult { get => _TotalResult; set { _TotalResult = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalResultString)); } } 
        private bool _TotalResult = false;

        /// <summary>
        /// 总体测试结果字符串（如“pass”或“fail”）
        /// </summary>
        public string TotalResultString => TotalResult?"PASS":"Fail";

    }


}