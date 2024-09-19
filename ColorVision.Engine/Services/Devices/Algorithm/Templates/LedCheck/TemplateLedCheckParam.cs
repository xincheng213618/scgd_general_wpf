using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck
{
    public class TemplateLedCheckParam : ITemplate<LedCheckParam>, IITemplateLoad
    {
        public TemplateLedCheckParam()
        {
            Title = "LedCheckParam算法设置";
            Code = "ledcheck";
            TemplateParams = LedCheckParam.LedCheckParams;
        }
    }
}
