using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.LEDStripDetection
{
    public class TemplateLEDStripDetection : ITemplate<LEDStripDetectionParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<LEDStripDetectionParam>> Params { get; set; } = new ObservableCollection<TemplateModel<LEDStripDetectionParam>>();

        public TemplateLEDStripDetection()
        {
            Title = "灯条检测模板管理";
            Code = "LEDStripDetection";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditLEDStripDetection.SetParam(TemplateParams[index].Value);
        }
        public EditLEDStripDetection EditLEDStripDetection { get; set; } = new EditLEDStripDetection();

        public override UserControl GetUserControl()
        {
            return EditLEDStripDetection;
        }
        public override UserControl CreateUserControl() => new EditLEDStripDetection();
    }
}
