#pragma warning disable CS0168,CS8603
using ColorVision.Common.MVVM;
using ProjectLUX.Process;
using ProjectLUX.Process.AR.W51AR;
using ProjectLUX.Process.Blue;
using ProjectLUX.Process.Chessboard;
using ProjectLUX.Process.Chessboard55;
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
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace ProjectLUX
{
    public enum ObjectiveTestResultCsvExportProfile
    {
        Current,
        Legacy
    }

    internal static class ObjectiveTestResultCsvExportProjector
    {
        public static object CreateExportRoot(ObjectiveTestResult results, ObjectiveTestResultCsvExportProfile profile)
        {
            if (profile != ObjectiveTestResultCsvExportProfile.Legacy)
            {
                return results;
            }

            return new ObjectiveTestResultCsvExportModel
            {
                W51ARTestResult = results.W51ARTestResult,
                W255ARTestResult = results.W255ARTestResult,
                W255TestResult = CloneWithDefaults<W255LegacyCsvExportResult>(results.W255TestResult),
                RedTestResult = results.RedTestResult,
                GreenTestResult = results.GreenTestResult,
                BlueTestResult = results.BlueTestResult,
                ChessboardARTestResult = results.ChessboardARTestResult,
                Chessboard55TestResult = results.Chessboard55TestResult,
                ChessboardTestResult = results.ChessboardTestResult,
                MTFHVARTestResult = CloneWithDefaults<MTFHARVLegacyCsvExportResult>(results.MTFHVARTestResult),
                MTFHVTestResult = results.MTFHVTestResult,
                VRMTFHTestResult = results.VRMTFHTestResult,
                VRMTFVTestResult = results.VRMTFVTestResult,
                DistortionARTestResult = results.DistortionARTestResult,
                OpticCenterTestResult = results.OpticCenterTestResult,
            };
        }

        private static TTarget CloneWithDefaults<TTarget>(object source) where TTarget : class, new()
        {
            if (source == null)
            {
                return null;
            }

            TTarget target = new();
            CopyObjectiveTestItems(source, target);
            return target;
        }

        private static void CopyObjectiveTestItems(object source, object target)
        {
            foreach (var targetProperty in target.GetType().GetProperties())
            {
                if (targetProperty.PropertyType != typeof(ObjectiveTestItem))
                {
                    continue;
                }

                var sourceProperty = source.GetType().GetProperty(targetProperty.Name);
                if (sourceProperty?.PropertyType != typeof(ObjectiveTestItem))
                {
                    continue;
                }

                if (sourceProperty.GetValue(source) is not ObjectiveTestItem sourceItem)
                {
                    continue;
                }

                if (targetProperty.GetValue(target) is not ObjectiveTestItem targetItem)
                {
                    targetItem = new ObjectiveTestItem();
                    targetProperty.SetValue(target, targetItem);
                }

                targetItem.TestValue = sourceItem.TestValue;
                targetItem.Value = sourceItem.Value;
                targetItem.LowLimit = sourceItem.LowLimit;
                targetItem.UpLimit = sourceItem.UpLimit;
            }
        }
    }

    internal sealed class ObjectiveTestResultCsvExportModel
    {
        [DisplayName("W255")]
        public object W51ARTestResult { get; set; }

        [DisplayName("W255")]
        public object W255ARTestResult { get; set; }

        [DisplayName("W255")]
        public object W255TestResult { get; set; }

        [DisplayName("R255")]
        public object RedTestResult { get; set; }

        [DisplayName("G255")]
        public object GreenTestResult { get; set; }

        [DisplayName("B255")]
        public object BlueTestResult { get; set; }

        [DisplayName("Chessborad_4x4")]
        public object ChessboardARTestResult { get; set; }

        [DisplayName("Chessborad_5x5")]
        public object Chessboard55TestResult { get; set; }

        [DisplayName("Chessborad_6x6")]
        public object ChessboardTestResult { get; set; }

        [DisplayName("MTF")]
        public object MTFHVARTestResult { get; set; }

        [DisplayName("MTF")]
        public object MTFHVTestResult { get; set; }

        [DisplayName("MTF")]
        public object VRMTFHTestResult { get; set; }

        [DisplayName("MTF")]
        public object VRMTFVTestResult { get; set; }

        [DisplayName("Distortion")]
        public object DistortionARTestResult { get; set; }

        [DisplayName("Optical_Center")]
        public object OpticCenterTestResult { get; set; }
    }

    internal sealed class W255LegacyCsvExportResult : ViewModelBase
    {
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Horizontal_Field_Of_View_Angle", Unit = "degree" };
        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Vertical_Field of_View_Angle", Unit = "degree" };
        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Diagonal_Field_of_View_Angle", Unit = "degree" };
        public ObjectiveTestItem P1Lv { get; set; } = new ObjectiveTestItem() { Name = "P1(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P1Cx { get; set; } = new ObjectiveTestItem() { Name = "P1(Cx)" };
        public ObjectiveTestItem P1Cy { get; set; } = new ObjectiveTestItem() { Name = "P1(Cy)" };
        public ObjectiveTestItem P1Cu { get; set; } = new ObjectiveTestItem() { Name = "P1(u')" };
        public ObjectiveTestItem P1Cv { get; set; } = new ObjectiveTestItem() { Name = "P1(v')" };
        public ObjectiveTestItem P2Lv { get; set; } = new ObjectiveTestItem() { Name = "P2(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P2Cx { get; set; } = new ObjectiveTestItem() { Name = "P2(Cx)" };
        public ObjectiveTestItem P2Cy { get; set; } = new ObjectiveTestItem() { Name = "P2(Cy)" };
        public ObjectiveTestItem P2Cu { get; set; } = new ObjectiveTestItem() { Name = "P2(u')" };
        public ObjectiveTestItem P2Cv { get; set; } = new ObjectiveTestItem() { Name = "P2(v')" };
        public ObjectiveTestItem P3Lv { get; set; } = new ObjectiveTestItem() { Name = "P3(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P3Cx { get; set; } = new ObjectiveTestItem() { Name = "P3(Cx)" };
        public ObjectiveTestItem P3Cy { get; set; } = new ObjectiveTestItem() { Name = "P3(Cy)" };
        public ObjectiveTestItem P3Cu { get; set; } = new ObjectiveTestItem() { Name = "P3(u')" };
        public ObjectiveTestItem P3Cv { get; set; } = new ObjectiveTestItem() { Name = "P3(v')" };
        public ObjectiveTestItem P4Lv { get; set; } = new ObjectiveTestItem() { Name = "P4(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P4Cx { get; set; } = new ObjectiveTestItem() { Name = "P4(Cx)" };
        public ObjectiveTestItem P4Cy { get; set; } = new ObjectiveTestItem() { Name = "P4(Cy)" };
        public ObjectiveTestItem P4Cu { get; set; } = new ObjectiveTestItem() { Name = "P4(u')" };
        public ObjectiveTestItem P4Cv { get; set; } = new ObjectiveTestItem() { Name = "P4(v')" };
        public ObjectiveTestItem P5Lv { get; set; } = new ObjectiveTestItem() { Name = "P5(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P5Cx { get; set; } = new ObjectiveTestItem() { Name = "P5(Cx)" };
        public ObjectiveTestItem P5Cy { get; set; } = new ObjectiveTestItem() { Name = "P5(Cy)" };
        public ObjectiveTestItem P5Cu { get; set; } = new ObjectiveTestItem() { Name = "P5(u')" };
        public ObjectiveTestItem P5Cv { get; set; } = new ObjectiveTestItem() { Name = "P5(v')" };
        public ObjectiveTestItem P6Lv { get; set; } = new ObjectiveTestItem() { Name = "P6(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P6Cx { get; set; } = new ObjectiveTestItem() { Name = "P6(Cx)" };
        public ObjectiveTestItem P6Cy { get; set; } = new ObjectiveTestItem() { Name = "P6(Cy)" };
        public ObjectiveTestItem P6Cu { get; set; } = new ObjectiveTestItem() { Name = "P6(u')" };
        public ObjectiveTestItem P6Cv { get; set; } = new ObjectiveTestItem() { Name = "P6(v')" };
        public ObjectiveTestItem P7Lv { get; set; } = new ObjectiveTestItem() { Name = "P7(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P7Cx { get; set; } = new ObjectiveTestItem() { Name = "P7(Cx)" };
        public ObjectiveTestItem P7Cy { get; set; } = new ObjectiveTestItem() { Name = "P7(Cy)" };
        public ObjectiveTestItem P7Cu { get; set; } = new ObjectiveTestItem() { Name = "P7(u')" };
        public ObjectiveTestItem P7Cv { get; set; } = new ObjectiveTestItem() { Name = "P7(v')" };
        public ObjectiveTestItem P8Lv { get; set; } = new ObjectiveTestItem() { Name = "P8(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P8Cx { get; set; } = new ObjectiveTestItem() { Name = "P8(Cx)" };
        public ObjectiveTestItem P8Cy { get; set; } = new ObjectiveTestItem() { Name = "P8(Cy)" };
        public ObjectiveTestItem P8Cu { get; set; } = new ObjectiveTestItem() { Name = "P8(u')" };
        public ObjectiveTestItem P8Cv { get; set; } = new ObjectiveTestItem() { Name = "P8(v')" };
        public ObjectiveTestItem P9Lv { get; set; } = new ObjectiveTestItem() { Name = "P9(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P9Cx { get; set; } = new ObjectiveTestItem() { Name = "P9(Cx)" };
        public ObjectiveTestItem P9Cy { get; set; } = new ObjectiveTestItem() { Name = "P9(Cy)" };
        public ObjectiveTestItem P9Cu { get; set; } = new ObjectiveTestItem() { Name = "P9(u')" };
        public ObjectiveTestItem P9Cv { get; set; } = new ObjectiveTestItem() { Name = "P9(v')" };
        public ObjectiveTestItem LuminanceUniformity { get; set; } = new ObjectiveTestItem() { Name = "Luminance_Uniformity(min_max_100)" };
        public ObjectiveTestItem ColorUniformity { get; set; } = new ObjectiveTestItem() { Name = "Color_Uniformity(uv_max)" };
        public ObjectiveTestItem CenterLunimance { get; set; } = new ObjectiveTestItem() { Name = "Center_Luminance", Unit = "cd/m^2" };
        public ObjectiveTestItem AverageLuminance { get; set; } = new ObjectiveTestItem() { Name = "Average_Luminance", Unit = "cd/m^2" };
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesx { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1931Chromatic_Coordinates_x" };
        public ObjectiveTestItem CenterCIE1931ChromaticCoordinatesy { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1931Chromatic_Coordinates_y" };
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesu { get; set; } = new ObjectiveTestItem() { Name = "Center CIE_1976Chromatic_Coordinates_u'" };
        public ObjectiveTestItem CenterCIE1976ChromaticCoordinatesv { get; set; } = new ObjectiveTestItem() { Name = "Center_CIE_1976Chromatic_Coordinates_v'" };
        public ObjectiveTestItem Center_Correlated_Color_Temperature { get; set; } = new ObjectiveTestItem() { Name = "Center_Correlated_Color_Temperature", Unit = "K" };
    }

    internal sealed class MTFHARVLegacyCsvExportResult : ViewModelBase
    {
        public ObjectiveTestItem MTF0F_Center_H1 { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_H1", Unit = "%" };
        public ObjectiveTestItem MTF0F_Center_V1 { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_V1", Unit = "%" };
        public ObjectiveTestItem MTF0F_Center_H2 { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_H2", Unit = "%" };
        public ObjectiveTestItem MTF0F_Center_V2 { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_V2", Unit = "%" };
        public ObjectiveTestItem MTF0F_Center_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0F_Center_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0F_Center_Vertical", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftUp_Vertical", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightUp_Vertical", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_RightDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.4F_RightDown_Vertical", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_4F_LeftDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.4F_LeftDown_Vertical", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftUp_Vertical", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightUp_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightUp_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightUp_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightUp_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightUp_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightUp_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightUp_Vertical", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_RightDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.8F_RightDown_Vertical", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftDown_H1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_H1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftDown_V1 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_V1", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftDown_H2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_H2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftDown_V2 { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_V2", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftDown_horizontal { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_horizontal", Unit = "%" };
        public ObjectiveTestItem MTF0_8F_LeftDown_Vertical { get; set; } = new ObjectiveTestItem() { Name = "0.8F_LeftDown_Vertical", Unit = "%" };
    }

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
                        var BrowsableAttribute = property.GetCustomAttribute<BrowsableAttribute>();
                        if (BrowsableAttribute != null && !BrowsableAttribute.Browsable)
                        {
                            continue;
                        }
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
            var exportRoot = ObjectiveTestResultCsvExportProjector.CreateExportRoot(results, ProjectLUXConfig.Instance.CsvExportProfile);
            ExportToCsv(exportRoot, filePath);
        }

        private static void ExportToCsv(object results, string filePath)
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

        [DisplayName("Chessborad_5x5")]
        public Chessboard55TestResult Chessboard55TestResult { get; set; }

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