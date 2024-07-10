using ColorVision.Engine.Templates;
using ColorVision.Engine.Services.Dao;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.FocusPoints
{
    public class TemplateFocusPointsParam : ITemplate<FocusPointsParam>, IITemplateLoad
    {
        public TemplateFocusPointsParam()
        {
            Title = "FocusPoints算法设置";
            Code = ModMasterType.FocusPoints;
            TemplateParams = FocusPointsParam.FocusPointsParams;
        }
    }
}
