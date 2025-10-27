using ColorVision.Common.MVVM;

namespace ProjectLUX.Recipe
{
    public class RecipeBase : ViewModelBase
    {
        public RecipeBase()
        {

        }
        public RecipeBase(double min,double max)
        {
            _Min = min;
            _Max = max;
        }
        public double Min { get => _Min; set { _Min = value; OnPropertyChanged(); } }
        private double _Min;

        public double Max { get => _Max; set { _Max = value; OnPropertyChanged(); } }
        private double _Max;
    }
}