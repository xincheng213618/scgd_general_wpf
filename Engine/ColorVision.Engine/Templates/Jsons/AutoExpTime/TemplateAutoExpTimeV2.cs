using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Templates;
using ColorVision.Engine.Templates.Menus;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.AutoExpTime
{
    public class MenuAutoExpTimeV2 : MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuItemCamera);
        public override string Header => $"{ColorVision.Engine.Properties.Resources.AutoExploreTemplate} V2";
        public override int Order => 24;
        public override ITemplate Template => new TemplateAutoExpTimeV2();
    }

    public class TemplateAutoExpTimeV2 : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateAutoExpTimeV2()
        {
            Title = ColorVision.Engine.Properties.Resources.AutoExposureTemplateSettings;
            Code = "auto_exp_time";
            Name = "Camera,auto_exp_time_v2";
            TemplateDicId = 94;
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
  "expTimeCfg": {
    "type": 0,
    "maxExpTime": 60000,
    "minExpTime": 10,
    "autoExpFlag": false,
    "autoExpSatDev": 20,
    "RoiMarginRatio": {
      "margin_RatioTop": 0,
      "margin_RatioLeft": 0,
      "margin_RatioRight": 0,
      "margin_RatioBottom": 0
    },
    "burstThreshold": 200,
    "autoExpSatMaxAD": 65535,
    "autoExpSyncFreq": 60,
    "autoExpTimeBegin": 5,
    "autoExpSaturation": 70,
    "autoExpMaxPecentage": 0.01
  }
}
""";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);

        public override IMysqlCommand? GetMysqlCommand() => new MysqlAutoExpTimeV2();
    }
}
