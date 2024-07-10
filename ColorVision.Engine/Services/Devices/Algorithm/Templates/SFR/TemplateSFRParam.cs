using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.SFR
{
    public class TemplateSFRParam : ITemplate<SFRParam>, IITemplateLoad
    {
        public TemplateSFRParam()
        {
            Title = "SFRParam算法设置";
            Code = ModMasterType.SFR;
            TemplateParams = SFRParam.SFRParams;
        }
    }
}
