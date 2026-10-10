using LocalizedText = global::ColorVision.Engine.EngineLocalization;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.OLEDImageProcessing
{
    public class TemplateLocalizationImageEnhancement : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; } = new();

        public TemplateLocalizationImageEnhancement()
        {
            Title = LocalizedText.Get("局部图像增强模板管理");
            Code = "OLED.LocalizationImageEnhan";
            Name = "OLED_LocalizationImageEnhan";
            TemplateDicId = 201;
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
