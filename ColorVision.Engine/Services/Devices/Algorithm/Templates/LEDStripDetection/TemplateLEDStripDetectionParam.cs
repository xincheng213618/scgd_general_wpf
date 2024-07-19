using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LEDStripDetection
{
    public class TemplateLEDStripDetectionParam : ITemplate<LEDStripDetectionParam>, IITemplateLoad
    {
        public TemplateLEDStripDetectionParam()
        {
            Title = "灯条检测算法设置";
            Code = "LEDStripDetection";
            TemplateParams = LEDStripDetectionParam.Params;
        }
    }
}
