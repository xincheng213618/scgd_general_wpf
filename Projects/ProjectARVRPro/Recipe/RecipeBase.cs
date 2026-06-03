
using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Recipe
{
    public class RecipeBase : ViewModelBase
    {
        private double _Fix = 1;
        private double _B;

        public RecipeBase()
        {

        }
        public RecipeBase(double min, double max, double fix = 1, double b = 0)
        {
            _Min = min;
            _Max = max;
            _Fix = fix;
            _B = b;
        }
        public double Min { get => _Min; set { _Min = value; OnPropertyChanged(); } }
        private double _Min;

        public double Max { get => _Max; set { _Max = value; OnPropertyChanged(); } }
        private double _Max;

        public double Fix { get => _Fix; set { _Fix = value; OnPropertyChanged(); } }

        public double B { get => _B; set { _B = value; OnPropertyChanged(); } }

        public double Apply(double value) => value * Fix + B;
    }
}
