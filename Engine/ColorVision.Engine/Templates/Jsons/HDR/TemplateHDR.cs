using ColorVision.Database;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.HDR
{

    public class TemplateHDR : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateHDR()
        {
            Title = ColorVision.Engine.Properties.Resources.HdrTemplateManagement;
            Code = "Camera.RunParams";
            Name = "Camera,HDR";
            TemplateDicId = 43;
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
        public string Description { get; set; } = "{\r\n  \"Gain\": 10,//增益\r\n  \"AvgCount\": 1, //平均次数\r\n  \"ThLow\": 50,//饱和度下限\r\n  \"ThHigh\": 150,//饱和度上限\r\n  \"ExpTimes\": [ //曝光参数列表\r\n    10,\r\n    50,\r\n    100\r\n  ],\r\n  \"HDRExpTime\": 100 //合成后的曝光时间\r\n}";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlHDR();

    }




}
