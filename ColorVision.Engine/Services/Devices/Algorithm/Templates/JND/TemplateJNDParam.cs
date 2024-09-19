using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.JND
{
    public class TemplateJNDParam : ITemplate<JNDParam>, IITemplateLoad
    {    
        public static ObservableCollection<TemplateModel<JNDParam>> Params { get; set; } = new ObservableCollection<TemplateModel<JNDParam>>();

        public TemplateJNDParam()
        {
            Title = "JNDParam算法设置";
            Code = "OLED.JND.CalVas";
            TemplateParams = Params;
        }
    }
}
