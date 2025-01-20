using ColorVision.Engine.MySql;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.JND
{
    public class TemplateJND : ITemplate<JNDParam>, IITemplateLoad
    {    
        public static ObservableCollection<TemplateModel<JNDParam>> Params { get; set; } = new ObservableCollection<TemplateModel<JNDParam>>();

        public TemplateJND()
        {
            Title = "JND";
            Code = "OLED.JND.CalVas";
            TemplateParams = Params;
        }
        public override IMysqlCommand? GetMysqlCommand()
        {
            return new MysqlJND();
        }
    }
}
