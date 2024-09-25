using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck
{
    public class TemplateLedCheckParam : ITemplate<LedCheckParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<LedCheckParam>> Params { get; set; } = new ObservableCollection<TemplateModel<LedCheckParam>>();

        public TemplateLedCheckParam()
        {
            Title = "LedCheckParam算法设置";
            Code = "ledcheck";
            TemplateParams = Params;
        }
    }
}
