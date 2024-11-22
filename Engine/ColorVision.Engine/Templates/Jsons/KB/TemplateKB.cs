using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class TemplateKB : ITemplateJson<TemplateJsonParam>
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateKB()
        {
            Title = "KB算法设置";
            Code = "KB";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateJson EditTemplateJson { get; set; } = new EditTemplateJson();

        public override UserControl GetUserControl()
        {
            return EditTemplateJson;
        }

    }




}
