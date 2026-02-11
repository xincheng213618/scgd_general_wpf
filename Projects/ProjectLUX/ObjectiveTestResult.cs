using ColorVision.Common.MVVM;
using ProjectLUX.Process;
using ProjectLUX.Process.AR.W51AR;
using ProjectLUX.Process.Blue;
using ProjectLUX.Process.Chessboard;
using ProjectLUX.Process.ChessboardAR;
using ProjectLUX.Process.Distortion;
using ProjectLUX.Process.Green;
using ProjectLUX.Process.MTFHV;
using ProjectLUX.Process.MTFHVAR;
using ProjectLUX.Process.OpticCenter;
using ProjectLUX.Process.Red;
using ProjectLUX.Process.VR.MTFH;
using ProjectLUX.Process.VR.MTFV;
using ProjectLUX.Process.W255;
using ProjectLUX.Process.W255AR;
using SQLitePCL;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace ProjectLUX
{
    public static class ObjectiveTestResultCsvExporter
    {
        public static void CollectRows(object obj, string testScreenName, List<string> rows)
        {
            foreach (var property in obj.GetType().GetProperties())
            {
                var childObj = property.GetValue(obj);
                if (childObj is IList list1 && property.PropertyType.IsGenericType)
                {
                    foreach (var item in list1)
                    {
                        if (item is ObjectiveTestItem objectiveTestItem)
                        {
                            rows.Add(FormatCsvRow(testScreenName, objectiveTestItem));
                        }
                    }
                    continue;
                }
                if (property.PropertyType == typeof(ObjectiveTestItem))
                {
                    var testItem = (ObjectiveTestItem)property.GetValue(obj);
                    if (testItem != null)
                    {
                        rows.Add(FormatCsvRow(testScreenName, testItem));
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
                        var childObj1 = property.GetValue(obj);
                        if (childObj1 != null)
                        {
                            CollectRows(childObj1, testScreenName, rows);
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

        private static string FormatCsvRow(string testScreenName, ObjectiveTestItem testItem)
        {
            string testResult = testItem.TestResult ? "pass" : "fail";
            return $"{testScreenName},{testItem.Name},{testItem.Value},{testItem.Unit},{testItem.LowLimit},{testItem.UpLimit},{testResult}";
        }

        public static void ExportToCsv(ObjectiveTestResult results, string filePath)
        {
            var rows = new List<string> {  "Test_Screen,Test_item,Test_Value,unit,lower_limit,upper_limit,Test_Result" };
            foreach (var prop in results.GetType().GetProperties())
            {

                if (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
                {

                    var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
                    var raw = displayNameAttr?.DisplayName ?? prop.Name;

                    // Recursively process child objects
                    var childObj = prop.GetValue(results);
                    if (childObj == null) continue;
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
        public W51ARTestResult W51ARTestResult { get; set; }
        [DisplayName("W255")]
        public W255ARTestResult W255ARTestResult { get; set; }

        [DisplayName("W255")]
        public W255TestResult W255TestResult { get; set; }

        [DisplayName("R255")]
        public RedTestResult RedTestResult { get; set; }

        [DisplayName("G255")]
        public GreenTestResult GreenTestResult { get; set; }
        [DisplayName("B255")]
        public BlueTestResult BlueTestResult { get; set; }

        [DisplayName("Chessborad_4x4")]
        public ChessboardARTestResult ChessboardARTestResult { get; set; }

        [DisplayName("Chessborad_6x6")]
        public ChessboardTestResult ChessboardTestResult { get; set; }

        [DisplayName("MTF")]
        public MTFHARVTestResult MTFHVARTestResult { get; set; }

        [DisplayName("MTF")]
        public MTFHVTestResult MTFHVTestResult { get; set; }


        [DisplayName("MTF")]
        public VRMTFHTestResult VRMTFHTestResult { get; set; }

        [DisplayName("MTF")]
        public VRMTFVTestResult VRMTFVTestResult { get; set; }

        [DisplayName("Distortion")]
        public DistortionTestResult DistortionARTestResult { get; set; }

        [DisplayName("Optical_Center")]
        public OpticCenterTestResult OpticCenterTestResult { get; set; }

        public bool TotalResult { get => _TotalResult; set { _TotalResult = value; OnPropertyChanged(); } } 
        private bool _TotalResult;
    }
}