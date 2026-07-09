using ColorVision.Database;
using ColorVision.Engine.Templates.Menus;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.DetectScreenDefects
{
    public class MenuDetectScreenDefects : MenuITemplateAlgorithmBase
    {
        public override string Header => "屏幕缺陷检测";
        public override int Order => 1058;
        public override ITemplate Template => new TemplateDetectScreenDefects();
    }

    public class TemplateDetectScreenDefects : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateDetectScreenDefects()
        {
            Title = "屏幕缺陷检测模板管理";
            Code = "ARVR.DetectScreenDefects";
            Name = "DetectScreenDefects";
            TemplateDicId = 58;
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
            EditTemplateJson = new EditTemplateJson(Description);
            return EditTemplateJson;
        }

        public string Description { get; set; } = """
{
  "screenDefectCfg": {
    "blur_sigma": 1.35,
    "line_params": {
      "angle": 0,
      "length": 15,
      "threshold": 0.95
    },
    "defect_sizes": [25, 15, 5],
    "aa_min_ratio_h": 0.2,
    "aa_min_ratio_w": 0.2,
    "merge_distance": 10,
    "clarity_threshold": 0.6,
    "defect_thresholds": [0.25, 0.25, 0.5],
    "clarity_safe_margin": 55,
    "light_shrink_margin": 60,
    "brightness_threshold": 0.3
  }
}
""";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);

        public override IMysqlCommand? GetMysqlCommand() => new MysqlDetectScreenDefects();
    }
}
