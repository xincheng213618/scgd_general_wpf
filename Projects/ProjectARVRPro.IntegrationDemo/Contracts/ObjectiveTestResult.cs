using ColorVision.Common.MVVM;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Process.Distortion;
using ProjectARVRPro.Process.MTF.MTFHV;
using ProjectARVRPro.Process.MTF.MTFHV048;
using ProjectARVRPro.Process.MTF.MTFHV058;
using ProjectARVRPro.Process.OpticCenter;
using ProjectARVRPro.Process.RGB.Blue;
using ProjectARVRPro.Process.RGB.Green;
using ProjectARVRPro.Process.RGB.Red;
using ProjectARVRPro.Process.W25;
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
        /// <summary>W25 白场 25 阶测试结果，主要包含中心亮度和中心色品坐标。</summary>
        [DisplayName("W25")]
        public W25TestResult W25TestResult { get; set; }

        /// <summary>W51 视场角测试结果，包含水平、垂直、对角线视场角。</summary>
        [DisplayName("W51")]
        public W51TestResult W51TestResult { get; set; }

        /// <summary>W255 白场 255 阶测试结果，包含视场角、亮度均匀性、色度均匀性、中心亮度/色品坐标等。</summary>
        [DisplayName("W255")]
        public W255TestResult W255TestResult { get; set; }

        /// <summary>黑场测试结果，主要包含 FOFO 对比度。</summary>
        [DisplayName("Black")]
        public BlackTestResult BlackTestResult { get; set; }

        /// <summary>红场测试结果，包含红色画面的亮度均匀性、色度均匀性和中心光色参数。</summary>
        [DisplayName("R255")]
        public RedTestResult RedTestResult { get; set; }

        /// <summary>绿场测试结果，包含绿色画面的亮度均匀性、色度均匀性和中心光色参数。</summary>
        [DisplayName("G255")]
        public GreenTestResult GreenTestResult { get; set; }

        /// <summary>蓝场测试结果，包含蓝色画面的亮度均匀性、色度均匀性和中心光色参数。</summary>
        [DisplayName("B255")]
        public BlueTestResult BlueTestResult { get; set; }

        /// <summary>棋盘格测试结果，主要包含棋盘格对比度。</summary>
        [DisplayName("Chessborad")]
        public ChessboardTestResult ChessboardTestResult { get; set; }

        /// <summary>MTF 清晰度/解析力测试结果，包含多个视场位置的 H/V 方向 MTF。</summary>
        [DisplayName("MTF")]
        public MTFHVTestResult MTFHVTestResult { get; set; }

        /// <summary>MTF 0.4F/0.8F 组合测试结果列表。</summary>
        [DisplayName("MTF048")]
        public List<MTFHV048TestResult> MTFHV048TestResults { get; set; } = new List<MTFHV048TestResult>();

        /// <summary>MTF 0.5F/0.8F 组合测试结果列表。</summary>
        [DisplayName("MTF058")]
        public List<MTFHV058TestResult> MTFHV058TestResults { get; set; } = new List<MTFHV058TestResult>();

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
    /// 总体测试结果字符串，通常为 PASS 或 Fail。
    /// </summary>
        public string TotalResultString { get { return TotalResult ? "PASS" : "Fail"; } }
    }
}
