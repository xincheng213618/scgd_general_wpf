using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.JDN
{
    public class TemplateJDN : ITemplate<JDNParam>, IITemplateLoad
    {    
        public static ObservableCollection<TemplateModel<JDNParam>> Params { get; set; } = new ObservableCollection<TemplateModel<JDNParam>>();

        public TemplateJDN()
        {
            Title = "JDN";
            Code = "OLED.JDN.CalVas";
            TemplateParams = Params;
        }
        public override IMysqlCommand? GetMysqlCommand()
        {
            return new MysqlJDN();
        }
    }
}
