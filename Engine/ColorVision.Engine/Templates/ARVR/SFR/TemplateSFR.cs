using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.SFR
{
    public class TemplateSFR : ITemplate<SFRParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<SFRParam>> Params { get; set; } = new ObservableCollection<TemplateModel<SFRParam>>();

        public TemplateSFR()
        {
            Title = "SFR模板管理";
            TemplateDicId = 9;
            Code = "SFR";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditSFR.SetParam(TemplateParams[index].Value);
        }
        public EditSFR EditSFR { get; set; } = new EditSFR();

        public override UserControl GetUserControl()
        {
            return EditSFR;
        }

    }
}
