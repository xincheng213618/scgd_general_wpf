using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace ProjectARVRPro.LegacyARVR
{

    /// <summary>
    /// 旧版ARVR扁平ObjectiveTestResult，与老ProjectARVR输出格式完全兼容。
    /// 用于向对方系统输出兼容的JSON和CSV。
    /// </summary>
    public class LegacyARVRObjectiveTestResult : ViewModelBase
    {
        public ObjectiveTestItem LuminanceUniformity { get; set; }
        public ObjectiveTestItem ColorUniformity { get; set; }
        public ObjectiveTestItem CenterCorrelatedColorTemperature { get; set; }
        public ObjectiveTestItem White1CenterCorrelatedColorTemperature { get; set; }
        public ObjectiveTestItem CenterLuminace { get; set; }
        public ObjectiveTestItem White1CenterLuminace { get; set; }

        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; }
        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; }
        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; }

        public ObjectiveTestItem FOFOContrast { get; set; }
        public ObjectiveTestItem ChessboardContrast { get; set; }

        public ObjectiveTestItem HorizontalTVDistortion { get; set; }
        public ObjectiveTestItem VerticalTVDistortion { get; set; }

        public ObjectiveTestItem MTF_H_Center_0F { get; set; }
        public ObjectiveTestItem MTF_H_LeftUp_0_5F { get; set; }
        public ObjectiveTestItem MTF_H_RightUp_0_5F { get; set; }
        public ObjectiveTestItem MTF_H_RightDown_0_5F { get; set; }
        public ObjectiveTestItem MTF_H_LeftDown_0_5F { get; set; }
        public ObjectiveTestItem MTF_H_LeftUp_0_8F { get; set; }
        public ObjectiveTestItem MTF_H_RightUp_0_8F { get; set; }
        public ObjectiveTestItem MTF_H_RightDown_0_8F { get; set; }
        public ObjectiveTestItem MTF_H_LeftDown_0_8F { get; set; }

        public ObjectiveTestItem MTF_V_Center_0F { get; set; }
        public ObjectiveTestItem MTF_V_LeftUp_0_5F { get; set; }
        public ObjectiveTestItem MTF_V_RightUp_0_5F { get; set; }
        public ObjectiveTestItem MTF_V_RightDown_0_5F { get; set; }
        public ObjectiveTestItem MTF_V_LeftDown_0_5F { get; set; }
        public ObjectiveTestItem MTF_V_LeftUp_0_8F { get; set; }
        public ObjectiveTestItem MTF_V_RightUp_0_8F { get; set; }
        public ObjectiveTestItem MTF_V_RightDown_0_8F { get; set; }
        public ObjectiveTestItem MTF_V_LeftDown_0_8F { get; set; }

        public ObjectiveTestItem XTilt { get; set; }
        public ObjectiveTestItem YTilt { get; set; }
        public ObjectiveTestItem Rotation { get; set; }
        public ObjectiveTestItem Ghost { get; set; }

        public bool TotalResult { get => _TotalResult; set { _TotalResult = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalResultString)); } }
        private bool _TotalResult = false;

        public string TotalResultString => TotalResult ? "PASS" : "Fail";

        public bool FlowWhiteTestReslut { get; set; } = false;
        public bool FlowWhite1TestReslut { get; set; } = false;
        public bool FlowWhite2TestReslut { get; set; } = false;
        public bool FlowBlackTestReslut { get; set; } = false;
        public bool FlowChessboardTestReslut { get; set; } = false;
        public bool FlowMTFHTestReslut { get; set; } = false;
        public bool FlowMTFVTestReslut { get; set; } = false;
        public bool FlowDistortionTestReslut { get; set; } = false;
        public bool FlowOpticCenterTestReslut { get; set; } = false;
    }

    /// <summary>
    /// 旧版CSV导出器，与老ProjectARVR的ExportToCsv输出格式兼容
    /// </summary>
    public static class LegacyARVRCsvExporter
    {
        public static void ExportToCsv(List<LegacyARVRObjectiveTestResult> results, string filePath)
        {
            var itemProps = typeof(LegacyARVRObjectiveTestResult).GetProperties()
                .Where(p => p.PropertyType == typeof(ObjectiveTestItem))
                .ToList();

            var headers = new List<string>();
            foreach (var prop in itemProps)
            {
                headers.Add($"{prop.Name}_TestValue");
                headers.Add($"{prop.Name}_TestResult");
                headers.Add($"{prop.Name}_LowLimit");
                headers.Add($"{prop.Name}_UpLimit");
            }
            headers.Add("TotalResult");
            headers.Add("TotalResultString");

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));

            foreach (var result in results)
            {
                var row = new List<string>();
                foreach (var prop in itemProps)
                {
                    var item = (ObjectiveTestItem)prop.GetValue(result);
                    row.Add(item?.TestValue ?? "");
                    row.Add(item?.TestResult.ToString() ?? "");
                    row.Add(item?.LowLimit.ToString() ?? "");
                    row.Add(item?.UpLimit.ToString() ?? "");
                }
                row.Add(result.TotalResult.ToString());
                row.Add(result.TotalResultString);
                sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }

    /// <summary>
    /// 将新版ObjectiveTestResult转换为旧版LegacyARVRObjectiveTestResult
    /// </summary>
    public static class LegacyARVRConverter
    {
        private static ObjectiveTestItem Convert(ObjectiveTestItem src)
        {
            if (src == null) return null;
            return new ObjectiveTestItem
            {
                Name = src.Name,
                TestValue = src.TestValue,
                Value = src.Value,
                LowLimit = src.LowLimit,
                UpLimit = src.UpLimit,
                Unit = src.Unit,
            };
        }

        /// <summary>
        /// 从DynamicTestResults中按Name查找ObjectiveTestItem
        /// </summary>
        private static ObjectiveTestItem FindDynamic(ObjectiveTestResult src, string dictKey, string itemName)
        {
            if (src.DynamicTestResults != null &&
                src.DynamicTestResults.TryGetValue(dictKey, out var items))
            {
                return items.FirstOrDefault(i => i.Name == itemName);
            }
            return null;
        }

        public static LegacyARVRObjectiveTestResult ToLegacy(ObjectiveTestResult src)
        {
            if (src == null) return new LegacyARVRObjectiveTestResult();

            var legacy = new LegacyARVRObjectiveTestResult();

            // W255 → LuminanceUniformity, ColorUniformity, CenterLuminace
            legacy.LuminanceUniformity = Convert(src.W255TestResult?.LuminanceUniformity);
            legacy.ColorUniformity = Convert(src.W255TestResult?.ColorUniformity);
            legacy.CenterLuminace = Convert(src.W255TestResult?.CenterLunimance);
            legacy.CenterCorrelatedColorTemperature = Convert(src.W255TestResult?.CenterCorrelatedColorTemperature);


            // W25 → White1CenterLuminace
            legacy.White1CenterLuminace = Convert(src.W25TestResult?.CenterLunimance);

            // W51 → FOV angles (优先W51，回退W255)
            legacy.HorizontalFieldOfViewAngle = Convert(src.W51TestResult?.HorizontalFieldOfViewAngle ?? src.W255TestResult?.HorizontalFieldOfViewAngle);
            legacy.VerticalFieldOfViewAngle = Convert(src.W51TestResult?.VerticalFieldOfViewAngle ?? src.W255TestResult?.VerticalFieldOfViewAngle);
            legacy.DiagonalFieldOfViewAngle = Convert(src.W51TestResult?.DiagonalFieldOfViewAngle ?? src.W255TestResult?.DiagonalFieldOfViewAngle);

            // Black → FOFOContrast
            legacy.FOFOContrast = Convert(src.BlackTestResult?.FOFOContrast);

            // Chessboard → ChessboardContrast
            legacy.ChessboardContrast = Convert(src.ChessboardTestResult?.ChessboardContrast);

            // Distortion
            legacy.HorizontalTVDistortion = Convert(src.DistortionTestResult?.HorizontalTVDistortion);
            legacy.VerticalTVDistortion = Convert(src.DistortionTestResult?.VerticalTVDistortion);

            // MTF_H: 优先从DynamicTestResults["MTFH"]取，取不到为null
            legacy.MTF_H_Center_0F = Convert(FindDynamic(src, "MTFH", "MTF_H_Center_0F"));
            legacy.MTF_H_LeftUp_0_5F = Convert(FindDynamic(src, "MTFH", "MTF_H_LeftUp_0_5F"));
            legacy.MTF_H_RightUp_0_5F = Convert(FindDynamic(src, "MTFH", "MTF_H_RightUp_0_5F"));
            legacy.MTF_H_RightDown_0_5F = Convert(FindDynamic(src, "MTFH", "MTF_H_RightDown_0_5F"));
            legacy.MTF_H_LeftDown_0_5F = Convert(FindDynamic(src, "MTFH", "MTF_H_LeftDown_0_5F"));
            legacy.MTF_H_LeftUp_0_8F = Convert(FindDynamic(src, "MTFH", "MTF_H_LeftUp_0_8F"));
            legacy.MTF_H_RightUp_0_8F = Convert(FindDynamic(src, "MTFH", "MTF_H_RightUp_0_8F"));
            legacy.MTF_H_RightDown_0_8F = Convert(FindDynamic(src, "MTFH", "MTF_H_RightDown_0_8F"));
            legacy.MTF_H_LeftDown_0_8F = Convert(FindDynamic(src, "MTFH", "MTF_H_LeftDown_0_8F"));

            // MTF_V: 优先从DynamicTestResults["MTFV"]取
            legacy.MTF_V_Center_0F = Convert(FindDynamic(src, "MTFV", "MTF_V_Center_0F"));
            legacy.MTF_V_LeftUp_0_5F = Convert(FindDynamic(src, "MTFV", "MTF_V_LeftUp_0_5F"));
            legacy.MTF_V_RightUp_0_5F = Convert(FindDynamic(src, "MTFV", "MTF_V_RightUp_0_5F"));
            legacy.MTF_V_RightDown_0_5F = Convert(FindDynamic(src, "MTFV", "MTF_V_RightDown_0_5F"));
            legacy.MTF_V_LeftDown_0_5F = Convert(FindDynamic(src, "MTFV", "MTF_V_LeftDown_0_5F"));
            legacy.MTF_V_LeftUp_0_8F = Convert(FindDynamic(src, "MTFV", "MTF_V_LeftUp_0_8F"));
            legacy.MTF_V_RightUp_0_8F = Convert(FindDynamic(src, "MTFV", "MTF_V_RightUp_0_8F"));
            legacy.MTF_V_RightDown_0_8F = Convert(FindDynamic(src, "MTFV", "MTF_V_RightDown_0_8F"));
            legacy.MTF_V_LeftDown_0_8F = Convert(FindDynamic(src, "MTFV", "MTF_V_LeftDown_0_8F"));

            // OpticCenter → XTilt, YTilt, Rotation
            legacy.XTilt = Convert(src.OpticCenterTestResult?.OptCenterXTilt ?? src.OpticCenterTestResult?.OptCenterXTilt);
            legacy.YTilt = Convert(src.OpticCenterTestResult?.OptCenterYTilt ?? src.OpticCenterTestResult?.OptCenterYTilt);
            legacy.Rotation = Convert(src.OpticCenterTestResult?.OptCenterRotation ?? src.OpticCenterTestResult?.OptCenterRotation);

            // Ghost: 新版无此数据
            legacy.Ghost = null;

            // CenterCorrelatedColorTemperature: 新版无此数据
            legacy.CenterCorrelatedColorTemperature = null;
            legacy.White1CenterCorrelatedColorTemperature = null;

            // TotalResult
            legacy.TotalResult = src.TotalResult;

            // Flow结果标志：根据子结果是否存在判断
            legacy.FlowWhiteTestReslut = src.W255TestResult != null;
            legacy.FlowWhite1TestReslut = src.W25TestResult != null;
            legacy.FlowWhite2TestReslut = src.W51TestResult != null;
            legacy.FlowBlackTestReslut = src.BlackTestResult != null;
            legacy.FlowChessboardTestReslut = src.ChessboardTestResult != null;
            legacy.FlowMTFHTestReslut = src.DynamicTestResults?.ContainsKey("MTFH") == true;
            legacy.FlowMTFVTestReslut = src.DynamicTestResults?.ContainsKey("MTFV") == true;
            legacy.FlowDistortionTestReslut = src.DistortionTestResult != null;
            legacy.FlowOpticCenterTestReslut = src.OpticCenterTestResult != null;

            return legacy;
        }
    }
}
