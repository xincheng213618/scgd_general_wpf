#pragma warning disable CA1707
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost
{
    public class TemplateGhostParam : ITemplate<GhostParam>, IITemplateLoad
    {
        public TemplateGhostParam()
        {
            Title = "GhostParam算法设置";
            Code = ModMasterType.Ghost;
            TemplateParams = GhostParam.GhostParams;
        }
    }


}
