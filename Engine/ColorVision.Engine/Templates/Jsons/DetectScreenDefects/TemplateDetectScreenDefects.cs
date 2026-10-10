using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.DetectScreenDefects
{
    public class TemplateDetectScreenDefects : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateDetectScreenDefects()
        {
            Title = Properties.Resources.ScreenDefect_TemplateManagement;
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
            EditTemplateJson = new EditTemplateJson();
            return EditTemplateJson;
        }

        public override UserControl CreateUserControl() => new EditTemplateJson();

    }
}
