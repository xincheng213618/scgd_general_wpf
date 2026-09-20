#pragma warning disable CA1707
using System.ComponentModel;

namespace ProjectARVRPro.Process.W51
{
    public class W51ProcessConfig : ProcessConfigBase<W51RecipeConfig>
    {
        [Category("显示配置")]
        [DisplayName("绘制FOV")]
        [Description("在 W51 结果图上绘制水平、垂直和对角 FOV；关闭时仅绘制发光区边界。")]
        public bool DrawFovOverlay { get => _DrawFovOverlay; set { _DrawFovOverlay = value; OnPropertyChanged(); } }
        private bool _DrawFovOverlay = true;

    }
}
