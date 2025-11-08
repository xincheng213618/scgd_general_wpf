using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.FocusPoints
{
    public class TemplateFocusPoints : ITemplate<FocusPointsParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<FocusPointsParam>> Params { get; set; } = new ObservableCollection<TemplateModel<FocusPointsParam>>();

        public TemplateFocusPoints()
        {
            Title =ColorVision.Engine.Properties.Resources.FocusPointsTemplateSettings;
            TemplateDicId = 15;
            Code = "focusPoints";
            TemplateParams = Params;
        }
    }
}
