using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.FocusPoints
{
    public class TemplateFocusPointsParam : ITemplate<FocusPointsParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<FocusPointsParam>> Params { get; set; } = new ObservableCollection<TemplateModel<FocusPointsParam>>();

        public TemplateFocusPointsParam()
        {
            Title = "FocusPoints算法设置";
            Code = "focusPoints";
            TemplateParams = Params;
        }
    }
}
