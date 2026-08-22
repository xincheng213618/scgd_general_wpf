using ColorVision.Database;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.OLEDImageProcessing
{
    public class TemplateDediffusion : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; } = new();

        public TemplateDediffusion()
        {
            Title = "解串扰模板管理";
            Code = "OLED.Dediffusion";
            Name = "OLED_Dediffusion";
            TemplateDicId = 202;
            TemplateParams = Params;
            IsUserControl = true;
        }

        public EditTemplateJson EditTemplateJson { get; set; }

        public string Description { get; } = """
{
  "rebuildCfg": {
    "de_kernel": [
      0.005801945726846804,
      0.006259136699992473,
      0.007446957706026484,
      0.006259136699992473,
      0.005801945726846804,
      0.0067489050808179005,
      0.01613838260967959,
      0.05531039330231698,
      0.01613838260967959,
      0.0067489050808179005,
      0.011469772733238114,
      0.07160011470190694,
      0.5685520426436761,
      0.07160011470190694,
      0.011469772733238114,
      0.0067489050808179005,
      0.01613838260967959,
      0.05531039330231698,
      0.01613838260967959,
      0.0067489050808179005,
      0.005801945726846804,
      0.006259136699992473,
      0.007446957706026484,
      0.006259136699992473,
      0.005801945726846804
    ],
    "de_isotropic": 1,
    "de_steplength": 8,
    "de_defusion_en": true,
    "de_iterationlimit": 300,
    "de_totalerrorratio": 0.1,
    "de_kernel_size_cols": 5,
    "de_kernel_size_rows": 5
  }
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

        public override IMysqlCommand? GetMysqlCommand() => new MysqlDediffusion();
    }
}
