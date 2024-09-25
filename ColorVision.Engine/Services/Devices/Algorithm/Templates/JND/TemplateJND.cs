using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.JND
{
    public class TemplateJND : ITemplate<JNDParam>, IITemplateLoad
    {    
        public static ObservableCollection<TemplateModel<JNDParam>> Params { get; set; } = new ObservableCollection<TemplateModel<JNDParam>>();

        public TemplateJND()
        {
            Title = "JDN";
            Code = "OLED.JND.CalVas";
            TemplateParams = Params;
        }
    }
}
