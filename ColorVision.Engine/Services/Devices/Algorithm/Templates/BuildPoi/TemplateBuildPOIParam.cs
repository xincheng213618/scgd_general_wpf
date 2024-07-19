using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.BuildPoi
{
    public class TemplateBuildPOIParam : ITemplate<BuildPOIParam>, IITemplateLoad
    {
        public TemplateBuildPOIParam()
        {
            Title = "BuildPOI算法设置";
            Code = ModMasterType.BuildPOI;
            TemplateParams = BuildPOIParam.BuildPOIParams;
        }
    }
}
