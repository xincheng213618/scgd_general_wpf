using ColorVision.Common.MVVM;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Process.Distortion;
using ProjectARVRPro.Process.MTF.MTFHV;
using ProjectARVRPro.Process.MTF.MTFHV048;
using ProjectARVRPro.Process.MTF.MTFHV058;
using ProjectARVRPro.Process.OpticCenter;
using ProjectARVRPro.Process.RGB.LuminanceChromaticity;
using ProjectARVRPro.Process.ScreenDefects;
using ProjectARVRPro.Process.W255;
using ProjectARVRPro.Process.W51;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ProjectARVRPro
{
    /// <summary>
    /// 表示一组客观测试项的测试结果，每个属性对应一个具体的测试项目，包含测试值、上下限、结果等信息。
    /// </summary>
    public class ObjectiveTestResult:ViewModelBase
    {
        [DisplayName("W51")]
        public W51TestResult W51TestResult { get; set; }

        [DisplayName("W255")]
        public W255TestResult W255TestResult { get; set; }

        [DisplayName("Black")]
        public BlackTestResult BlackTestResult { get; set; }

        [DisplayName("亮色度测试")]
        public Dictionary<string, LuminanceChromaticityTestResult> LuminanceChromaticityTestResults { get; set; } = new();

        [DisplayName("Chessborad")]
        public ChessboardTestResult ChessboardTestResult { get; set; }

        [DisplayName("MTF")]
        public MTFHVTestResult MTFHVTestResult { get; set; }

        [DisplayName("MTF048")]
        public List<MTFHV048TestResult> MTFHV048TestResults { get; set; } = new List<MTFHV048TestResult>();

        [DisplayName("MTF058")]
        public List<MTFHV058TestResult> MTFHV058TestResults { get; set; } = new List<MTFHV058TestResult>();

        [DisplayName("DynamicMTFHV058")]
        public Dictionary<string, MTFHV058TestResult> DynamicMTFHV058TestResults { get; set; } = new Dictionary<string, MTFHV058TestResult>();

        [DisplayName("Distortion")]
        public DistortionTestResult DistortionTestResult { get; set; }

        [DisplayName("Optical_Center")]
        public OpticCenterTestResult OpticCenterTestResult { get; set; }

        /// <summary>
        /// 动态测试结果字典，Key为测试画面名称，Value为测试项集合。
        /// 用于动态添加MTF等测试结果，无需静态声明属性，与现有静态属性导出兼容。
        /// </summary>
        public Dictionary<string, ObservableCollection<ObjectiveTestItem>> DynamicTestResults { get; set; } = new Dictionary<string, ObservableCollection<ObjectiveTestItem>>();

        /// <summary>
        /// 动态关注点结果字典，Key为测试画面名称，Value为关注点集合。
        /// 用于动态添加 POI_XYZ 关注点结果，无需静态声明属性。
        /// </summary>
        public Dictionary<string, ObservableCollection<PoixyuvData>> DynamicPoixyuvDatas { get; set; } = new Dictionary<string, ObservableCollection<PoixyuvData>>();

        /// <summary>
        /// 动态屏幕缺陷检测结果，Key为测试画面名称，Value为已清理的缺陷汇总与缺陷框参数。
        /// 该结果仅用于展示、绘制和客户JSON输出，不参与上下限或总结果判定。
        /// </summary>
        public Dictionary<string, ScreenDefectsData> DynamicScreenDefectResults { get; set; } = new Dictionary<string, ScreenDefectsData>();

        /// <summary>
        /// 总体测试结果（true表示通过，false表示不通过）
        /// </summary>
        public bool TotalResult { get => _TotalResult; set { _TotalResult = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalResultString)); } } 
        private bool _TotalResult = true;

        /// <summary>
        /// 总体失败说明；测试失败时用于告诉调用方具体失败原因。
        /// </summary>
        public string Msg { get => _Msg; set { _Msg = value; OnPropertyChanged(); } }
        private string _Msg = string.Empty;

        /// <summary>
        /// 总体测试结果字符串（如“pass”或“fail”）
        /// </summary>
        public string TotalResultString => TotalResult?"PASS":"Fail";

    }


}
