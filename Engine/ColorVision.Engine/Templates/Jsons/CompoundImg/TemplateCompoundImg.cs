using ColorVision.Database;
using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.CompoundImg
{

    public class TJCompoundImgParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJCompoundImgParam));



        public TJCompoundImgParam() : base()
        {
        }

        public TJCompoundImgParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateCompoundImg : ITemplateJson<TJCompoundImgParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJCompoundImgParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJCompoundImgParam>>();

        public TemplateCompoundImg()
        {
            Title = "图像拼接模板管理";
            Code = "CompoundImg";
            Name = "图像拼接";
            TemplateDicId = 46;
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
        public string Description { get; set; } = "{\r\n  \"caclType\": 0,\r\n  \"debugCfg\": {\r\n    \"Debug\": false,\r\n    \"debugPath\": \"Result\\\\\",\r\n    \"debugImgResize\": 2\r\n  },\r\n  \"overlapPart\": 0.2\r\n}";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlCompoundImg();

    }




}
