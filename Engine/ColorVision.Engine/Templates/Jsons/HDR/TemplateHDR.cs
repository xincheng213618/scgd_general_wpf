using ColorVision.Engine.MySql;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.HDR
{

    public class TemplateHDR : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateHDR()
        {
            Title = "HDR模板管理";
            Code = "camera_exp_time";
            Name = "相机参数";
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
            EditTemplateJson = EditTemplateJson ?? new EditTemplateJson(Description);
            return EditTemplateJson;
        }
        public string Description { get; set; } = "";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlHDR();

    }




}
