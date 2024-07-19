using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Distortion
{
    public class TemplateDistortionParam : ITemplate<DistortionParam>, IITemplateLoad
    {
        public TemplateDistortionParam()
        {
            Title = "畸变算法设置";
            Code = ModMasterType.Distortion;
            TemplateParams = DistortionParam.DistortionParams;
        }
    }
}
