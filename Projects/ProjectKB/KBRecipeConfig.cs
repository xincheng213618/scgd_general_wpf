using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ProjectKB
{
    [DisplayName("KB上下限判定 (KB Threshold Rules)")]
    public class KBRecipeConfig : ViewModelBase
    {
        [DisplayName("启用单键亮度判定 (EnableKeyLvLimit)"), Category("亮度")]
        public bool EnableKeyLvLimit { get => _EnableKeyLvLimit; set { _EnableKeyLvLimit = value; OnPropertyChanged(); } }
        private bool _EnableKeyLvLimit = true;

        [DisplayName("单键亮度下限 (MinKeyLv)"), Category("亮度")]
        public double MinKeyLv { get => _MinKeyLv; set { _MinKeyLv = value; OnPropertyChanged(); } }
        private double _MinKeyLv;

        [DisplayName("单键亮度上限 (MaxKeyLv)"), Category("亮度")]
        public double MaxKeyLv { get => _MaxKeyLv; set { _MaxKeyLv = value; OnPropertyChanged(); } }
        private double _MaxKeyLv;

        [DisplayName("启用平均亮度判定 (EnableAvgLvLimit)"), Category("亮度")]
        public bool EnableAvgLvLimit { get => _EnableAvgLvLimit; set { _EnableAvgLvLimit = value; OnPropertyChanged(); } }
        private bool _EnableAvgLvLimit = true;

        [DisplayName("平均亮度上限 (MaxAvgLv)"), Category("亮度")]
        public double MaxAvgLv { get => _MaxAvgLv; set { _MaxAvgLv = value; OnPropertyChanged(); } }
        private double _MaxAvgLv;

        [DisplayName("平均亮度下限 (MinAvgLv)"), Category("亮度")]
        public double MinAvgLv { get => _MinAvgLv; set { _MinAvgLv = value; OnPropertyChanged(); } }
        private double _MinAvgLv;

        /// <summary>
        /// 亮度一致性
        /// </summary>
        [DisplayName("启用亮度均匀性判定 (EnableUniformityLimit)"), Category("均匀性")]
        public bool EnableUniformityLimit { get => _EnableUniformityLimit; set { _EnableUniformityLimit = value; OnPropertyChanged(); } }
        private bool _EnableUniformityLimit = true;

        [DisplayName("亮度均匀性下限(%) (MinUniformity)"), Category("均匀性")]
        public double MinUniformity { get => _MinUniformity; set { _MinUniformity = value; OnPropertyChanged(); } }
        private double _MinUniformity;


        [DisplayName("启用局部对比度判定 (EnableKeyLcLimit)"), Category("局部对比度")]
        public bool EnableKeyLcLimit { get => _EnableKeyLcLimit; set { _EnableKeyLcLimit = value; OnPropertyChanged(); } }
        private bool _EnableKeyLcLimit = true;

        [DisplayName("局部对比度下限(%) (MinKeyLc)"), Category("局部对比度")]
        public double MinKeyLc { get => _MinKeyLc; set { _MinKeyLc = value; OnPropertyChanged(); } }
        private double _MinKeyLc;

        [DisplayName("局部对比度上限(%) (MaxKeyLc)"), Category("局部对比度")]
        public double MaxKeyLc { get => _MaxKeyLc; set { _MaxKeyLc = value; OnPropertyChanged(); } }
        private double _MaxKeyLc;


        [DisplayName("启用背光自动修正 (EnableBacklightAutotune)"), Category("背光自动修正")]
        public bool EnableBacklightAutotune { get => _EnableBacklightAutotune; set { _EnableBacklightAutotune = value; OnPropertyChanged(); } }
        private bool _EnableBacklightAutotune;

        [DisplayName("Sigmoid斜率 (AutotuneSteepness)"), Category("背光自动修正")]
        public double BacklightAutotuneSteepness { get => _BacklightAutotuneSteepness; set { _BacklightAutotuneSteepness = value; OnPropertyChanged(); } }
        private double _BacklightAutotuneSteepness = 5;

        [DisplayName("平均亮度Q1 (AvgLvQ1)"), Category("背光自动修正")]
        public double BacklightAutotuneAvgLvQ1 { get => _BacklightAutotuneAvgLvQ1; set { _BacklightAutotuneAvgLvQ1 = value; OnPropertyChanged(); } }
        private double _BacklightAutotuneAvgLvQ1 = -1;

        [DisplayName("平均亮度Q3 (AvgLvQ3)"), Category("背光自动修正")]
        public double BacklightAutotuneAvgLvQ3 { get => _BacklightAutotuneAvgLvQ3; set { _BacklightAutotuneAvgLvQ3 = value; OnPropertyChanged(); } }
        private double _BacklightAutotuneAvgLvQ3 = -1;

        [DisplayName("最小亮度Q1 (MinLvQ1)"), Category("背光自动修正")]
        public double BacklightAutotuneMinLvQ1 { get => _BacklightAutotuneMinLvQ1; set { _BacklightAutotuneMinLvQ1 = value; OnPropertyChanged(); } }
        private double _BacklightAutotuneMinLvQ1 = -1;

        [DisplayName("最小亮度Q3 (MinLvQ3)"), Category("背光自动修正")]
        public double BacklightAutotuneMinLvQ3 { get => _BacklightAutotuneMinLvQ3; set { _BacklightAutotuneMinLvQ3 = value; OnPropertyChanged(); } }
        private double _BacklightAutotuneMinLvQ3 = -1;

        [DisplayName("均匀性Q1(%) (UniformityQ1)"), Category("背光自动修正")]
        public double BacklightAutotuneUniformityQ1 { get => _BacklightAutotuneUniformityQ1; set { _BacklightAutotuneUniformityQ1 = value; OnPropertyChanged(); } }
        private double _BacklightAutotuneUniformityQ1 = -1;

        [DisplayName("均匀性Q3(%) (UniformityQ3)"), Category("背光自动修正")]
        public double BacklightAutotuneUniformityQ3 { get => _BacklightAutotuneUniformityQ3; set { _BacklightAutotuneUniformityQ3 = value; OnPropertyChanged(); } }
        private double _BacklightAutotuneUniformityQ3 = -1;



    }
}
