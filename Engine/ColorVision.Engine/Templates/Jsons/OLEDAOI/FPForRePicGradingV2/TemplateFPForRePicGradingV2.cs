using LocalizedText = global::ColorVision.Engine.EngineLocalization;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForRePicGradingV2
{
    public class TJFPForRePicGradingV2Param : TemplateJsonParam
    {
        public TJFPForRePicGradingV2Param() : base()
        {
        }

        public TJFPForRePicGradingV2Param(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {
        }
    }

    public class TemplateFPForRePicGradingV2 : ITemplateJson<TJFPForRePicGradingV2Param>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJFPForRePicGradingV2Param>> Params { get; set; } = new ObservableCollection<TemplateModel<TJFPForRePicGradingV2Param>>();

        public TemplateFPForRePicGradingV2()
        {
            Title = LocalizedText.Get("缺陷检测V2模板管理");
            Code = "OLED.AOI.FPForRePicGradingV2";
            Name = "FPForRePicGradingV2";
            TemplateDicId = 56;
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateJson EditTemplateJson { get; set; }

        public override UserControl GetUserControl()
        {
            EditTemplateJson = new EditTemplateJson();
            return EditTemplateJson;
        }
        public override UserControl CreateUserControl() => new EditTemplateJson();
    }
}
