using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.FOV
{
    public class TemplateFOVParam : ITemplate<FOVParam>, IITemplateLoad
    {
        public TemplateFOVParam()
        {
            Title = "FOVParam算法设置";
            Code = "FOV";
            TemplateParams = FOVParam.FOVParams;
        }
    }
}
