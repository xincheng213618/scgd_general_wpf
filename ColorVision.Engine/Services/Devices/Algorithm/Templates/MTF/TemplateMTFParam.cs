using ColorVision.Engine.Templates;
using ColorVision.Engine.Services.Dao;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.MTF
{
    public class TemplateMTFParam : ITemplate<MTFParam>, IITemplateLoad
    {
        public TemplateMTFParam()
        {
            Title = "MTFParam算法设置";
            Code = ModMasterType.MTF;
            TemplateParams = MTFParam.MTFParams;
        }
    }
}
