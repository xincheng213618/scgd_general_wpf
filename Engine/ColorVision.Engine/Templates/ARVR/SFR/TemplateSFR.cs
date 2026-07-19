using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.SFR
{
    public class TemplateSFR : ITemplate<SFRParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<SFRParam>> Params { get; set; } = new ObservableCollection<TemplateModel<SFRParam>>();

        public TemplateSFR()
        {
            Title = ColorVision.Engine.Properties.Resources.SFRTemplateManagement;
            TemplateDicId = 9;
            Code = "SFR";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditSFR.SetParam(TemplateParams[index].Value);
        }
        private EditSFR? _editSFR;
        public EditSFR EditSFR
        {
            get => _editSFR ??= new EditSFR();
            set => _editSFR = value;
        }

        public override UserControl GetUserControl()
        {
            return EditSFR;
        }

    }
}
