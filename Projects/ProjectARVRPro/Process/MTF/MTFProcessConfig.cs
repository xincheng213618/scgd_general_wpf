using ProjectARVRPro.Recipe;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTF
{
    public class MTFProcessConfig : ProcessConfigBase
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
        private string _Name = "MTF";

        [Category("上下限配置")]
        [DisplayName("统一上下限")]
        [Description("所有MTF项目使用的统一上下限")]
        public RecipeBase UnifiedRecipe { get => _UnifiedRecipe; set { _UnifiedRecipe = value; OnPropertyChanged(); } }
        private RecipeBase _UnifiedRecipe = new RecipeBase(0.5, 0);

        [Category("Fix补偿")]
        [DisplayName("水平Fix")]
        [Description("水平方向统一Fix补偿系数")]
        public double FixH { get => _FixH; set { _FixH = value; OnPropertyChanged(); } }
        private double _FixH = 1;

        [Category("Fix补偿")]
        [DisplayName("垂直Fix")]
        [Description("垂直方向统一Fix补偿系数")]
        public double FixV { get => _FixV; set { _FixV = value; OnPropertyChanged(); } }
        private double _FixV = 1;

        [Category("名称映射")]
        [DisplayName("名称映射字典")]
        [Description("MTFResult名称到导出显示名称的映射，Key为MTFResult中的name，Value为导出时显示的名称")]
        public ObservableCollection<NameMappingItem> NameMapping { get => _NameMapping; set { _NameMapping = value; OnPropertyChanged(); } }
        private ObservableCollection<NameMappingItem> _NameMapping = new ObservableCollection<NameMappingItem>();

        public string GetDisplayName(string rawName)
        {
            foreach (var item in NameMapping)
            {
                if (item.Key == rawName)
                    return item.Value;
            }
            return rawName;
        }
    }

    public class NameMappingItem
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
