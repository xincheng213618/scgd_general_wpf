using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Aoi
{
    public class TemplateAOIParam : ITemplate<AOIParam>, IITemplateLoad
    {
        public TemplateAOIParam()
        {
            Title = "AOIParam设置";
            Code = "AOI";
            TemplateParams = AOIParam.Params;
        }
    }
}
