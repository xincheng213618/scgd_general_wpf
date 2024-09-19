using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.FocusPoints
{
    public class TemplateFocusPointsParam : ITemplate<FocusPointsParam>, IITemplateLoad
    {
        public TemplateFocusPointsParam()
        {
            Title = "FocusPoints算法设置";
            Code = "focusPoints";
            TemplateParams = FocusPointsParam.FocusPointsParams;
        }
    }
}
