#pragma warning disable CA1707
using ColorVision.Engine.Media;
using System.ComponentModel;

namespace ProjectARVRPro.Process.Black
{
    public class BlackProcessConfig : ProcessConfigBase<BlackRecipeConfig>
    {
        public bool IsUsingNing { get => _IsUsingNing; set { _IsUsingNing = value; OnPropertyChanged(); } }
        private bool _IsUsingNing ;

        public string FormatString { get => _FormatString; set { _FormatString = value; OnPropertyChanged(); } }
        private string _FormatString = "F2";

        public string Key_Center { get => _Key_Center; set { _Key_Center = value; OnPropertyChanged(); } }
        private string _Key_Center = "P_5";

        [Category("显示配置")]
        [DisplayName("显示内容")]
        [Description("选择关注点图层显示的字段和小数位；仅影响结果图绘制。")]
        [PropertyEditorType(typeof(CvcieTemplatePropertiesEditor))]
        public string DisplayTemplate { get => _DisplayTemplate; set { _DisplayTemplate = value ?? string.Empty; OnPropertyChanged(); } }
        private string _DisplayTemplate = PoiDisplayTemplateDefaults.Cie;
    }
}
