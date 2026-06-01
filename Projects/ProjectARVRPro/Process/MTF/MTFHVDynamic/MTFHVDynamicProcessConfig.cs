using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTF.MTFHVDynamic
{
    public class MTFHVDynamicRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("上下限配置")]
        [DisplayName("H上下限")]
        [Description("MTFHV水平结果使用的上下限")]
        public RecipeBase HRecipe { get => _HRecipe; set { _HRecipe = value; OnPropertyChanged(); } }
        private RecipeBase _HRecipe = new RecipeBase(0.5, 0);

        [Category("上下限配置")]
        [DisplayName("V上下限")]
        [Description("MTFHV垂直结果使用的上下限")]
        public RecipeBase VRecipe { get => _VRecipe; set { _VRecipe = value; OnPropertyChanged(); } }
        private RecipeBase _VRecipe = new RecipeBase(0.5, 0);
    }

    public class MTFHVDynamicProcessConfig : ProcessConfigBase
    {
        [Category("显示配置")]
        [DisplayName("显示格式")]
        [Description("MTF HV值显示格式")]
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F1";

        [Category("导出配置")]
        [DisplayName("导出名称")]
        [Description("导出CSV和DynamicTestResults时显示的测试画面名称，例如MTFHV048")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = "MTFHV";

        [Category("导出配置")]
        [DisplayName("单位")]
        [Description("MTF HV结果单位")]
        public string Unit { get => _Unit; set { _Unit = value; OnPropertyChanged(); } }
        private string _Unit = "%";

        public MTFHVDynamicRecipeConfig RecipeConfig { get => _RecipeConfig; set { _RecipeConfig = value; OnPropertyChanged(); } }
        private MTFHVDynamicRecipeConfig _RecipeConfig = new MTFHVDynamicRecipeConfig();
    }
}
