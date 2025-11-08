using ColorVision.Database;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.POI.POIRevise
{
    public class TemplatePoiReviseParam : ITemplate<PoiReviseParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiReviseParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiReviseParam>>();

        public TemplatePoiReviseParam()
        {
            Title = ColorVision.Engine.Properties.Resources.POICorrectionTemplateSettings; 
            TemplateDicId = 24;
            Code = "PoiRevise";
            TemplateParams = Params;
        }


        public override IMysqlCommand? GetMysqlCommand()
        {
            return new MysqlPoiRevise();
        }
    }
}
