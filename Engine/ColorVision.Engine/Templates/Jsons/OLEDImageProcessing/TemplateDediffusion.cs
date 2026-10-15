using LocalizedText = global::ColorVision.Engine.EngineLocalization;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.OLEDImageProcessing
{
    public class TemplateDediffusion : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; } = new();

        public TemplateDediffusion()
        {
            Title = LocalizedText.Get("解串扰模板管理");
            Code = "OLED.Dediffusion";
            Name = "OLED_Dediffusion";
            TemplateDicId = 202;
            TemplateParams = Params;
            IsUserControl = true;
        }

        public EditTemplateJson EditTemplateJson { get; set; }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }

        public override UserControl GetUserControl()
        {
            EditTemplateJson = new EditTemplateJson();
            return EditTemplateJson;
        }

        public override UserControl CreateUserControl() => new EditTemplateJson();

    }
}
