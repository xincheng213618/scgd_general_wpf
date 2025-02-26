using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.LedCheck
{
    public class TemplateLedCheck : ITemplate<LedCheckParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<LedCheckParam>> Params { get; set; } = new ObservableCollection<TemplateModel<LedCheckParam>>();

        public TemplateLedCheck()
        {
            Title = "像素级灯珠检测";
            Code = "ledcheck";
            TemplateParams = Params;
        }
    }
}
