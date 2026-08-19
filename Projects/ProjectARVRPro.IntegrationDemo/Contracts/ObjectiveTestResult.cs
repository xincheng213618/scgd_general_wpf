using ColorVision.Common.MVVM;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Process.Distortion;
using ProjectARVRPro.Process.KeyedResults.FieldOfView;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using ProjectARVRPro.Process.MTF.MTF07.MTFH;
using ProjectARVRPro.Process.MTF.MTF07.MTFV;
using ProjectARVRPro.Process.MTF.MTFHV;
using ProjectARVRPro.Process.MTF.MTFHV048;
using ProjectARVRPro.Process.MTF.MTFHV058;
using ProjectARVRPro.Process.OpticCenter;
using ProjectARVRPro.Process.ScreenDefects;
using ProjectARVRPro.Process.W255;
using ProjectARVRPro.Process.W51;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;

namespace ProjectARVRPro
{
    /// <summary>
    /// ARVRPro 客观测试结果聚合对象。Data 节点反序列化后对应这个类型。
    /// </summary>
    public class ObjectiveTestResult : ViewModelBase
    {
        /// <summary>W51 视场角测试结果，包含水平、垂直、对角线视场角。</summary>
        [DisplayName("W51")]
        public W51TestResult W51TestResult { get; set; }

        /// <summary>W255 白场 255 阶测试结果，包含视场角、亮度均匀性、色度均匀性、中心亮度/色品坐标等。</summary>
        [DisplayName("W255")]
        public W255TestResult W255TestResult { get; set; }

        /// <summary>黑场测试结果，主要包含 FOFO 对比度。</summary>
        [DisplayName("Black")]
        public BlackTestResult BlackTestResult { get; set; }

        /// <summary>按配置名称输出的视场角测试结果。</summary>
        [DisplayName("视场角测试")]
        public Dictionary<string, FieldOfViewTestResult> FieldOfViewTestResults { get; set; } = new Dictionary<string, FieldOfViewTestResult>();

        /// <summary>按配置名称输出的亮度、色度及均匀性测试结果。</summary>
        [DisplayName("亮色度测试")]
        public Dictionary<string, LuminanceChromaticityTestResult> LuminanceChromaticityTestResults { get; set; } = new Dictionary<string, LuminanceChromaticityTestResult>();

        /// <summary>按配置名称输出的YW 12X7与8X7双POI组亮色度结果。</summary>
        [DisplayName("YW亮色度测试")]
        public Dictionary<string, LuminanceChromaticityYWTestResult> LuminanceChromaticityYWTestResults { get; set; } = new Dictionary<string, LuminanceChromaticityYWTestResult>();

        /// <summary>棋盘格测试结果，主要包含棋盘格对比度。</summary>
        [DisplayName("Chessborad")]
        public ChessboardTestResult ChessboardTestResult { get; set; }

        /// <summary>按配置名称输出的棋盘格测试结果。</summary>
        [DisplayName("棋盘格测试")]
        public Dictionary<string, ChessboardTestResult> ChessboardTestResults { get; set; } = new Dictionary<string, ChessboardTestResult>();

        /// <summary>MTF 清晰度/解析力测试结果，包含多个视场位置的 H/V 方向 MTF。</summary>
        [DisplayName("MTF")]
        public MTFHVTestResult MTFHVTestResult { get; set; }

        /// <summary>MTF 0.4F/0.8F 组合测试结果列表。</summary>
        [DisplayName("MTF048")]
        public List<MTFHV048TestResult> MTFHV048TestResults { get; set; } = new List<MTFHV048TestResult>();

        /// <summary>MTF 0.5F/0.8F 组合测试结果列表。</summary>
        [DisplayName("MTF058")]
        public List<MTFHV058TestResult> MTFHV058TestResults { get; set; } = new List<MTFHV058TestResult>();

        /// <summary>按配置名称输出的 MTF 0.5F/0.8F 测试结果。</summary>
        [DisplayName("DynamicMTFHV058")]
        public Dictionary<string, MTFHV058TestResult> DynamicMTFHV058TestResults { get; set; } = new Dictionary<string, MTFHV058TestResult>();

        /// <summary>按配置名称输出的水平方向中心与 0.7F MTF 测试结果。</summary>
        [DisplayName("MTFH07")]
        public Dictionary<string, MTFH07TestResult> MTFH07TestResults { get; set; } = new Dictionary<string, MTFH07TestResult>();

        /// <summary>按配置名称输出的垂直方向中心与 0.7F MTF 测试结果。</summary>
        [DisplayName("MTFV07")]
        public Dictionary<string, MTFV07TestResult> MTFV07TestResults { get; set; } = new Dictionary<string, MTFV07TestResult>();

        /// <summary>畸变测试结果，包含 TV 畸变、光学畸变和九点/梯形畸变。</summary>
        [DisplayName("Distortion")]
        public DistortionTestResult DistortionTestResult { get; set; }

        /// <summary>光学中心测试结果，包含图像中心/光学中心偏移、倾斜和旋转。</summary>
        [DisplayName("Optical_Center")]
        public OpticCenterTestResult OpticCenterTestResult { get; set; }

        /// <summary>
        /// 动态测试结果字典。Key 为测试画面名称，Value 为该画面下的测试项集合；用于后续扩展 MTF 等动态项目。
        /// </summary>
        public Dictionary<string, ObservableCollection<ObjectiveTestItem>> DynamicTestResults { get; set; } = new Dictionary<string, ObservableCollection<ObjectiveTestItem>>();

        /// <summary>
        /// 动态关注点结果字典。Key 为测试画面名称，Value 为该画面下的 POI 光色数据。
        /// </summary>
        public Dictionary<string, ObservableCollection<PoixyuvData>> DynamicPoixyuvDatas { get; set; } = new Dictionary<string, ObservableCollection<PoixyuvData>>();

        /// <summary>
        /// 动态屏幕缺陷检测结果。Key 为测试画面名称，Value 为缺陷汇总和缺陷框参数。
        /// </summary>
        public Dictionary<string, ScreenDefectsData> DynamicScreenDefectResults { get; set; } = new Dictionary<string, ScreenDefectsData>();

        /// <summary>
        /// 总体测试结果。true 表示整机或当前流程判定通过。
        /// </summary>
        public bool TotalResult
        {
            get { return _TotalResult; }
            set
            {
                _TotalResult = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalResultString));
            }
        }
        private bool _TotalResult = true;

        /// <summary>
        /// 测试失败时的总体失败说明。
        /// </summary>
        public string Msg { get; set; } = string.Empty;

        /// <summary>
        /// 总体测试结果字符串，通常为 PASS 或 Fail。
        /// </summary>
        public string TotalResultString { get { return TotalResult ? "PASS" : "Fail"; } }
    }
}
