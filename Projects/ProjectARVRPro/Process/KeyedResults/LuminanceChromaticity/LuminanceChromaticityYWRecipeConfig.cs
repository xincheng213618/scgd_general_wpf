using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity
{
    public class LuminanceChromaticityYWRecipeConfig : ViewModelBase, IRecipeConfig
    {
        private const string Category12X7 = "YW 12X7";
        private const string Category8X7 = "YW 8X7";

        [Category(Category12X7)]
        [DisplayName("Average luminance(nit)")]
        public RecipeBase AverageLuminance12X7 { get => _AverageLuminance12X7; set { _AverageLuminance12X7 = value; OnPropertyChanged(); } }
        private RecipeBase _AverageLuminance12X7 = new(750, 0);

        [Category(Category12X7)]
        [DisplayName("Luminance uniformity(%)")]
        public RecipeBase LuminanceUniformity12X7 { get => _LuminanceUniformity12X7; set { _LuminanceUniformity12X7 = value; OnPropertyChanged(); } }
        private RecipeBase _LuminanceUniformity12X7 = new(0.20, 0);

        [Category(Category12X7)]
        [DisplayName("Color uniformity")]
        public RecipeBase ColorUniformity12X7 { get => _ColorUniformity12X7; set { _ColorUniformity12X7 = value; OnPropertyChanged(); } }
        private RecipeBase _ColorUniformity12X7 = new(0, 0.05);

        [Category(Category8X7)]
        [DisplayName("Average luminance(nit)")]
        public RecipeBase AverageLuminance8X7 { get => _AverageLuminance8X7; set { _AverageLuminance8X7 = value; OnPropertyChanged(); } }
        private RecipeBase _AverageLuminance8X7 = new(750, 0);

        [Category(Category8X7)]
        [DisplayName("Luminance uniformity(%)")]
        public RecipeBase LuminanceUniformity8X7 { get => _LuminanceUniformity8X7; set { _LuminanceUniformity8X7 = value; OnPropertyChanged(); } }
        private RecipeBase _LuminanceUniformity8X7 = new(0.20, 0);

        [Category(Category8X7)]
        [DisplayName("Color uniformity")]
        public RecipeBase ColorUniformity8X7 { get => _ColorUniformity8X7; set { _ColorUniformity8X7 = value; OnPropertyChanged(); } }
        private RecipeBase _ColorUniformity8X7 = new(0, 0.05);
    }
}
