using ColorVision.Database;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.SFRFindROI
{
    public class TemplateSFRFindROI : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateSFRFindROI()
        {
            Title = "SFR寻边模板管理";
            Code = "ARVR.SFR.FindROI";
            TemplateDicId = 36;
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
        public string Description { get; set; } = "//sfr roi配置\r\nstruct SfrRoiParam\r\n{\r\n        float th;                 //二值化阈值      \r\n        float lowThreshold;       //边缘化低阈值 \r\n        float highThreshold;      //边缘化高阈值\r\n        float minLength;          //最短线段长度\r\n        int   roi_w;              //roi的宽和高      \r\n        int   roi_h;              //\r\n\r\n};";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);

        public override IMysqlCommand? GetMysqlCommand() => new MysqlSFRFindROI();

    }




}
