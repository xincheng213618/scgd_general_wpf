using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ProjectKB
{
    [DisplayName("KB上下限判定")]
    public class KBRecipeConfig : ViewModelBase
    {
        [DisplayName("启用单键亮度判定"), Category("亮度")]
        public bool EnableKeyLvLimit { get => _EnableKeyLvLimit; set { _EnableKeyLvLimit = value; OnPropertyChanged(); } }
        private bool _EnableKeyLvLimit = true;

        [DisplayName("单键亮度下限"), Category("亮度")]
        public double MinKeyLv { get => _MinKeyLv; set { _MinKeyLv = value; OnPropertyChanged(); } }
        private double _MinKeyLv;

        [DisplayName("单键亮度上限"), Category("亮度")]
        public double MaxKeyLv { get => _MaxKeyLv; set { _MaxKeyLv = value; OnPropertyChanged(); } }
        private double _MaxKeyLv;

        [DisplayName("启用平均亮度判定"), Category("亮度")]
        public bool EnableAvgLvLimit { get => _EnableAvgLvLimit; set { _EnableAvgLvLimit = value; OnPropertyChanged(); } }
        private bool _EnableAvgLvLimit = true;

        [DisplayName("平均亮度上限"), Category("亮度")]
        public double MaxAvgLv { get => _MaxAvgLv; set { _MaxAvgLv = value; OnPropertyChanged(); } }
        private double _MaxAvgLv;

        [DisplayName("平均亮度下限"), Category("亮度")]
        public double MinAvgLv { get => _MinAvgLv; set { _MinAvgLv = value; OnPropertyChanged(); } }
        private double _MinAvgLv;

        /// <summary>
        /// 亮度一致性
        /// </summary>
        [DisplayName("启用亮度均匀性判定"), Category("均匀性")]
        public bool EnableUniformityLimit { get => _EnableUniformityLimit; set { _EnableUniformityLimit = value; OnPropertyChanged(); } }
        private bool _EnableUniformityLimit = true;

        [DisplayName("亮度均匀性下限(%)"), Category("均匀性")]
        public double MinUniformity { get => _MinUniformity; set { if (value > 100) { _MinUniformity = 100;  return; } if (value < 0) { _MinUniformity = 0; return; } _MinUniformity = value; OnPropertyChanged(); } }
        private double _MinUniformity;

        [DisplayName("启用局部对比度判定"), Category("局部对比度")]
        public bool EnableKeyLcLimit { get => _EnableKeyLcLimit; set { _EnableKeyLcLimit = value; OnPropertyChanged(); } }
        private bool _EnableKeyLcLimit = true;

        [DisplayName("局部对比度下限(%)"), Category("局部对比度")]
        public double MinKeyLc { get => _MinKeyLc; set { if (value > 100) { _MinKeyLc = 100; return; } if (value < -100){ _MinKeyLc = -100; return; } _MinKeyLc = value; OnPropertyChanged(); } }
        private double _MinKeyLc;

        [DisplayName("局部对比度上限(%)"), Category("局部对比度")]
        public double MaxKeyLc { get => _MaxKeyLc; set { if (value > 100) { _MaxKeyLc = 100; return; } if (value < -100) { _MaxKeyLc = -100; return; } _MaxKeyLc = value; OnPropertyChanged(); } }
        private double _MaxKeyLc;

    }
}