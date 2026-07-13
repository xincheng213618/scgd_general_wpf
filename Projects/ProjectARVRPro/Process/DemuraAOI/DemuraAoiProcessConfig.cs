using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.DemuraAOI
{
    public class DemuraAoiRangeRecipe : RecipeBase
    {
        public DemuraAoiRangeRecipe()
        {
        }

        public DemuraAoiRangeRecipe(double min, double max, bool isUse = false) : base(min, max)
        {
            _IsUse = isUse;
        }

        [DisplayName("启用")]
        public bool IsUse { get => _IsUse; set { _IsUse = value; OnPropertyChanged(); } }
        private bool _IsUse;
    }

    public class DemuraAoiGradeRecipe : ViewModelBase
    {
        public DemuraAoiGradeRecipe()
        {
        }

        public DemuraAoiGradeRecipe(string allowedGrades, bool isUse = true)
        {
            _AllowedGrades = allowedGrades;
            _IsUse = isUse;
        }

        [DisplayName("启用")]
        public bool IsUse { get => _IsUse; set { _IsUse = value; OnPropertyChanged(); } }
        private bool _IsUse = true;

        [DisplayName("允许等级")]
        [Description("多个等级使用英文逗号分隔，比较时忽略大小写")]
        public string AllowedGrades { get => _AllowedGrades; set { _AllowedGrades = value; OnPropertyChanged(); } }
        private string _AllowedGrades = string.Empty;

        public bool IsAllowed(string? grade)
        {
            if (!IsUse) return true;
            if (string.IsNullOrWhiteSpace(grade)) return false;

            return AllowedGrades.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(grade.Trim(), StringComparer.OrdinalIgnoreCase);
        }
    }

    public class DemuraAoiRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("W255")]
        [DisplayName("亮度均匀性")]
        [Description("内部按0~1比例判定，例如0.75表示75%；显示时转换为百分比")]
        public RecipeBase W255Uniformity { get => _W255Uniformity; set { _W255Uniformity = value; OnPropertyChanged(); } }
        private RecipeBase _W255Uniformity = new RecipeBase(0.75, 0);

        [Category("AOI等级")]
        [DisplayName("AOI允许等级")]
        public DemuraAoiGradeRecipe AoiGrade { get => _AoiGrade; set { _AoiGrade = value; OnPropertyChanged(); } }
        private DemuraAoiGradeRecipe _AoiGrade = new DemuraAoiGradeRecipe("WELL");

        [Category("AOI等级")]
        [DisplayName("黑场允许等级")]
        public DemuraAoiGradeRecipe BlackGrade { get => _BlackGrade; set { _BlackGrade = value; OnPropertyChanged(); } }
        private DemuraAoiGradeRecipe _BlackGrade = new DemuraAoiGradeRecipe("OK");

        [Category("AOI数值")]
        [DisplayName("最大缺陷密度")]
        public DemuraAoiRangeRecipe MaxDefectDensity { get => _MaxDefectDensity; set { _MaxDefectDensity = value; OnPropertyChanged(); } }
        private DemuraAoiRangeRecipe _MaxDefectDensity = new DemuraAoiRangeRecipe(0, 0);

        [Category("AOI数值")]
        [DisplayName("暗缺陷总数")]
        public DemuraAoiRangeRecipe DarkTotalDefects { get => _DarkTotalDefects; set { _DarkTotalDefects = value; OnPropertyChanged(); } }
        private DemuraAoiRangeRecipe _DarkTotalDefects = new DemuraAoiRangeRecipe(0, 0);

        [Category("AOI数值")]
        [DisplayName("亮缺陷总数")]
        public DemuraAoiRangeRecipe BrightTotalDefects { get => _BrightTotalDefects; set { _BrightTotalDefects = value; OnPropertyChanged(); } }
        private DemuraAoiRangeRecipe _BrightTotalDefects = new DemuraAoiRangeRecipe(0, 0);

        [Category("AOI数值")]
        [DisplayName("黑场亮点数量")]
        public DemuraAoiRangeRecipe BlackBrightCount { get => _BlackBrightCount; set { _BlackBrightCount = value; OnPropertyChanged(); } }
        private DemuraAoiRangeRecipe _BlackBrightCount = new DemuraAoiRangeRecipe(0, 0);

    }

    public class DemuraAoiProcessConfig : ProcessConfigBase
    {
        [Category("结果配置")]
        [DisplayName("结果名称")]
        [Description("导出CSV和DynamicTestResults使用的测试名称")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = "DemuraAOI";

        [Category("结果配置")]
        [DisplayName("启用最终卡控")]
        [Description("关闭时仍输出单项判定，但不改变流程总结果，用于现场观察比对")]
        public bool EnforceResult { get => _EnforceResult; set { _EnforceResult = value; OnPropertyChanged(); } }
        private bool _EnforceResult = true;

        [Category("必需数据")]
        [DisplayName("要求W255图像")]
        public bool RequireW255 { get => _RequireW255; set { _RequireW255 = value; OnPropertyChanged(); } }
        private bool _RequireW255 = true;

        [Category("必需数据")]
        [DisplayName("要求AOI分级结果")]
        public bool RequireAoiGrading { get => _RequireAoiGrading; set { _RequireAoiGrading = value; OnPropertyChanged(); } }
        private bool _RequireAoiGrading = true;

        [Category("必需数据")]
        [DisplayName("要求黑场结果")]
        public bool RequireBlackResult { get => _RequireBlackResult; set { _RequireBlackResult = value; OnPropertyChanged(); } }
        private bool _RequireBlackResult = true;

        [Category("W255计算")]
        [DisplayName("W255文件关键字")]
        public string W255Keyword { get => _W255Keyword; set { _W255Keyword = value; OnPropertyChanged(); } }
        private string _W255Keyword = "W255";

        [Category("W255计算")]
        [DisplayName("九点ROI半径")]
        [Description("单位为像素；九点位于图像宽高的1/4、1/2、3/4位置")]
        public int W255RoiRadius { get => _W255RoiRadius; set { _W255RoiRadius = value; OnPropertyChanged(); } }
        private int _W255RoiRadius = 30;

        [Category("解析配置")]
        [DisplayName("文件读取重试次数")]
        public int FileReadRetryCount { get => _FileReadRetryCount; set { _FileReadRetryCount = value; OnPropertyChanged(); } }
        private int _FileReadRetryCount = 5;

        [Category("解析配置")]
        [DisplayName("文件读取重试间隔(ms)")]
        public int FileReadRetryDelayMs { get => _FileReadRetryDelayMs; set { _FileReadRetryDelayMs = value; OnPropertyChanged(); } }
        private int _FileReadRetryDelayMs = 200;

    }
}
