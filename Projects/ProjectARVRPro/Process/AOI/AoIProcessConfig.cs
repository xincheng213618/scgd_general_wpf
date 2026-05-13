using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.AOI
{
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

        [Category("导出配置")]
        [DisplayName("输出原图")]
        [Description("是否将批次原图一并输出到导出目录")]
        public bool ExportOriginalImage { get => _ExportOriginalImage; set { _ExportOriginalImage = value; OnPropertyChanged(); } }
        private bool _ExportOriginalImage;

        [Category("导出配置")]
        [DisplayName("原图转TIF")]
        [Description("输出原图时，若文件为cvraw则转换为tif后导出")]
        public bool ExportOriginalAsTif { get => _ExportOriginalAsTif; set { _ExportOriginalAsTif = value; OnPropertyChanged(); } }
        private bool _ExportOriginalAsTif;

        [Category("导出配置")]
        [DisplayName("输出CIE")]
        [Description("是否输出 OLED_RebuildPixelsMem 生成的 CIE 文件")]
        public bool ExportCieFile { get => _ExportCieFile; set { _ExportCieFile = value; OnPropertyChanged(); } }
        private bool _ExportCieFile;

        public AoiRecipeConfig RecipeConfig { get => _RecipeConfig; set { _RecipeConfig = value; OnPropertyChanged(); } }
        private AoiRecipeConfig _RecipeConfig = new AoiRecipeConfig();

    }
}
