using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.Distortion2
{

    public class TemplateJsonDistortion2 : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TemplateJsonDistortion2));

        public TemplateJsonDistortion2() : base()
        {
        }

        public TemplateJsonDistortion2(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateDistortion2 : ITemplateJson<TemplateJsonDistortion2>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonDistortion2>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonDistortion2>>();

        public TemplateDistortion2()
        {
            Title =ColorVision.Engine.Properties.Resources.Distortion20TemplateManagement;
            Code = "distortion";
            Name = "distortion2.0";
            TemplateDicId = 40;
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
            EditTemplateJson = new EditTemplateJson();
            return EditTemplateJson;
        }
        public override UserControl CreateUserControl() => new EditTemplateJson();
    }




}
