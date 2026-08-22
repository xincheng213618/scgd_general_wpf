using ColorVision.Database;
using ColorVision.Engine.Templates.Menus;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.OLEDImageProcessing
{
    [System.Obsolete("Algorithm-template menu entry removed from the main menu.")]
    public class MenuLocalizationImageEnhancement : MenuITemplateAlgorithmBase
    {
        public override string Header => "局部图像增强";
        public override int Order => 1201;
        public override ITemplate Template => new TemplateLocalizationImageEnhancement();
    }

    public class TemplateLocalizationImageEnhancement : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; } = new();

        public TemplateLocalizationImageEnhancement()
        {
            Title = "局部图像增强模板管理";
            Code = "OLED.LocalizationImageEnhan";
            Name = "OLED_LocalizationImageEnhan";
            TemplateDicId = 201;
            TemplateParams = Params;
            IsUserControl = true;
        }

        public EditTemplateJson EditTemplateJson { get; set; }

        public string Description { get; } = """
{
  "bs": 16,
  "sp": 100,
  "mapping": 5.5,
  "tgsigma": 0.9166666666666666,
  "blurSize": 31,
  "th_ratio": 0.1,
  "threshold": 5,
  "img_format_convert_factor": 256
}
""";

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }

        public override UserControl GetUserControl()
        {
            EditTemplateJson = new EditTemplateJson(Description);
            return EditTemplateJson;
        }

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);

        public override IMysqlCommand? GetMysqlCommand() => new MysqlLocalizationImageEnhancement();
    }
}
