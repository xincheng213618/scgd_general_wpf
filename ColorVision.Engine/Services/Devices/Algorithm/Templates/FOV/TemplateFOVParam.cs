using ColorVision.Engine.Templates;
using ColorVision.Engine.Services.Dao;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.FOV
{
    public class TemplateFOVParam : ITemplate<FOVParam>, IITemplateLoad
    {
        public TemplateFOVParam()
        {
            Title = "FOVParam算法设置";
            Code = ModMasterType.FOV;
            TemplateParams = FOVParam.FOVParams;
        }
    }
}
