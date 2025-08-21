using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using System.ComponentModel;

namespace ProjectKB
{
    [DisplayName("ARVR上下限判定")]
    public class KBRecipeConfig : ViewModelBase, IRecipe
    {
        public double MinKeyLv { get => _MinKeyLv; set { _MinKeyLv = value; NotifyPropertyChanged(); } }
        private double _MinKeyLv;

        public double MaxKeyLv { get => _MaxKeyLv; set { _MaxKeyLv = value; NotifyPropertyChanged(); } }
        private double _MaxKeyLv;

        public double MaxAvgLv { get => _MaxAvgLv; set { _MaxAvgLv = value; NotifyPropertyChanged(); } }
        private double _MaxAvgLv;

        public double MinAvgLv { get => _MinAvgLv; set { _MinAvgLv = value; NotifyPropertyChanged(); } }
        private double _MinAvgLv;

        /// <summary>
        /// 亮度一致性
        /// </summary>
        public double MinUniformity { get => _MinUniformity; set { if (value > 100) { _MinUniformity = 100;  return; } if (value < 0) { _MinUniformity = 0; return; } _MinUniformity = value; NotifyPropertyChanged(); } }
        private double _MinUniformity;

        public double MinKeyLc { get => _MinKeyLc; set { if (value > 100) { _MinKeyLc = 100; return; } if (value < -100){ _MinKeyLc = -100; return; } _MinKeyLc = value; NotifyPropertyChanged(); } }
        private double _MinKeyLc;

        public double MaxKeyLc { get => _MaxKeyLc; set { if (value > 100) { _MaxKeyLc = 100; return; } if (value < -100) { _MaxKeyLc = -100; return; } _MaxKeyLc = value; NotifyPropertyChanged(); } }
        private double _MaxKeyLc;

    }
}