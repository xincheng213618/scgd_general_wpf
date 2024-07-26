using ColorVision.Engine.Templates;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LEDStripDetection
{
    public class TemplateLEDStripDetectionParam : ITemplate<LEDStripDetectionParam>, IITemplateLoad
    {
        public TemplateLEDStripDetectionParam()
        {
            Title = "灯条检测算法设置";
            Code = "LEDStripDetection";
            TemplateParams = LEDStripDetectionParam.Params;
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


    }
}
