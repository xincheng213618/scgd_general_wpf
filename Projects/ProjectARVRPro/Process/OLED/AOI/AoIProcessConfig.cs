using ColorVision.Common.MVVM;
using ProjectARVRPro.Fix;
using ProjectARVRPro.Recipe;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ProjectARVRPro.Process.OLED.AOI
{
    public class aoiFixConfig : ViewModelBase, IFixConfig
    {
        [Category("Fix")]
        public double UnifiedFix { get => _UnifiedFix; set { _UnifiedFix = value; OnPropertyChanged(); } }
        private double _UnifiedFix = 1;
    }

    public class AoiRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("上下限配置")]
        [DisplayName("统一上下限")]
        [Description("所有MTF项目使用的统一上下限")]
        public RecipeBase UnifiedRecipe { get => _UnifiedRecipe; set { _UnifiedRecipe = value; OnPropertyChanged(); } }
        private RecipeBase _UnifiedRecipe = new RecipeBase(0.5, 0);
    }

    public class AoIProcessConfig : ProcessConfigBase
    {
        [Category("显示配置")]
        [DisplayName("显示格式")]
        [Description("MTF值显示格式")]
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F1";

        [Category("导出配置")]
        [DisplayName("导出名称")]
        [Description("导出CSV时显示的测试画面名称")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = "AOI"; 

        public aoiFixConfig FixConfig { get => _FixConfig; set { _FixConfig = value; OnPropertyChanged(); } }
        private aoiFixConfig _FixConfig = new aoiFixConfig();

        public AoiRecipeConfig RecipeConfig { get => _RecipeConfig; set { _RecipeConfig = value; OnPropertyChanged(); } }
        private AoiRecipeConfig _RecipeConfig = new AoiRecipeConfig();

    }
}
