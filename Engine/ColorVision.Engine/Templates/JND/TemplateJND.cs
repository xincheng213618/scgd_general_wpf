using ColorVision.Database;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.JND
{
    public class TemplateJND : ITemplate<JNDParam>, IITemplateLoad
    {    
        public static ObservableCollection<TemplateModel<JNDParam>> Params { get; set; } = new ObservableCollection<TemplateModel<JNDParam>>();

        public TemplateJND()
        {
            Title =ColorVision.Engine.Properties.Resources.JNDTemplateManagement;
            TemplateDicId = 30;
            Code = "OLED.JND.CalVas";
            TemplateParams = Params;
        }
        public override IMysqlCommand? GetMysqlCommand()
        {
            return new MysqlJND();
        }
    }
}
