#pragma warning disable CA1805
using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ProjectARVRPro.Process
{
    public static class PoiDisplayTemplateDefaults
    {
        public const string Cie = "X:@X:F3  Y:@Y:F3  Z:@Z:F3\\nx:@x:F3  y:@y:F3\\nu′:@u:F3  v′:@v:F3\\nCCT:@CCT:F3 K  λd:@Wave:F3 nm";
        public const string Luminance = "Y:@Y:F2";
    }

    /// <summary>
    /// Base class for process configurations with SaveCsv support.
    /// </summary>
    public abstract class ProcessConfigBase : ViewModelBase
    {
        [DisplayName("保存CSV")]
        [Description("是否保存测试结果到CSV文件")]
        public bool SaveCsv { get => _SaveCsv; set { _SaveCsv = value; OnPropertyChanged(); } }
        private bool _SaveCsv = false;
    }

    /// <summary>
    /// Base class for process configurations that own an independent recipe.
    /// </summary>
    public abstract class ProcessConfigBase<TRecipeConfig> : ProcessConfigBase
        where TRecipeConfig : class, IRecipeConfig, new()
    {
        [Browsable(false)]
        public TRecipeConfig RecipeConfig { get => _RecipeConfig; set { _RecipeConfig = value ?? new(); OnPropertyChanged(); } }
        private TRecipeConfig _RecipeConfig = new();
    }
}
