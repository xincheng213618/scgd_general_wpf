using ColorVision.Database;
using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.SFR2
{

    public class TJSFRParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJSFRParam));



        public TJSFRParam() : base()
        {
        }

        public TJSFRParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateSFR2 : ITemplateJson<TJSFRParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJSFRParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJSFRParam>>();

        public TemplateSFR2()
        {
            Title = "SFRV2模板管理";
            Code = "SFR";
            Name = "SFR_V2";
            TemplateDicId = 49;
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
        public string Description { get; set; } = "{\r\n  \"caclWay\": 1,\r\n  \"MaskRect\": {\r\n    \"h\": 0,\r\n    \"w\": 0,\r\n    \"x\": 0,\r\n    \"y\": 0,\r\n    \"enable\": false\r\n  },\r\n  \"debugCfg\": {\r\n    \"Debug\": false,\r\n    \"debugPath\": \"Result\\\\\\\\\",\r\n    \"debugImgResize\": 2\r\n  },\r\n  \"sfrAutoPoi1\": {\r\n    \"dst_roi_h\": 60,\r\n    \"dst_roi_w\": 60,\r\n    \"minLength\": 100,\r\n    \"active_Top\": true,\r\n    \"active_Left\": true,\r\n    \"active_Right\": true,\r\n    \"lowThreshold\": 20,\r\n    \"active_Bottom\": true,\r\n    \"highThreshold\": 40,\r\n    \"thresholdRatio\": 0.6\r\n  }\r\n}\r\n";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlSFR2();

    }




}
