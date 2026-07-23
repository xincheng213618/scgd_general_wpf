using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.LEDStripDetection
{
    public class TemplateLEDStripDetection : ITemplate<LEDStripDetectionParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<LEDStripDetectionParam>> Params { get; set; } = new ObservableCollection<TemplateModel<LEDStripDetectionParam>>();

        public TemplateLEDStripDetection()
        {
            Title = ColorVision.Engine.Properties.Resources.LedBandDetectorManagement;
            TemplateDicId = 21;
            Code = "LEDStripDetection";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditLEDStripDetection.SetParam(TemplateParams[index].Value);
        }
        private EditLEDStripDetection? _editLEDStripDetection;
        public EditLEDStripDetection EditLEDStripDetection
        {
            get => _editLEDStripDetection ??= new EditLEDStripDetection();
            set => _editLEDStripDetection = value;
        }

        public override UserControl GetUserControl()
        {
            return EditLEDStripDetection;
        }
        public override UserControl CreateUserControl() => new EditLEDStripDetection();
    }
}
