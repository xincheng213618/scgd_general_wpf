using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.LedCheck
{
    public class TemplateLedCheck : ITemplate<LedCheckParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<LedCheckParam>> Params { get; set; } = new ObservableCollection<TemplateModel<LedCheckParam>>();

        public TemplateLedCheck()
        {
            Title = ColorVision.Engine.Properties.Resources.PixelLedDetect;
            Code = "FindLED";
            TemplateParams = Params;
        }
    }
}
