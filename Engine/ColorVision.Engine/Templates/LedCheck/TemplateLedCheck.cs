using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.LedCheck
{
    public class TemplateLedCheck : ITemplate<LedCheckParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<LedCheckParam>> Params { get; set; } = new ObservableCollection<TemplateModel<LedCheckParam>>();

        public TemplateLedCheck()
        {
            Title = "LedCheckParam算法设置";
            Code = "ledcheck";
            TemplateParams = Params;
        }
    }
}
