using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.AutoExpTime
{
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
            EditTemplateJson = new EditTemplateJson();
            return EditTemplateJson;
        }

        public override UserControl CreateUserControl() => new EditTemplateJson();

    }
}
