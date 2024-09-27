using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.CADMapping
{
    public class TemplateCADMapping : ITemplate<CADMappingParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<CADMappingParam>> Params { get; set; } = new ObservableCollection<TemplateModel<CADMappingParam>>();

        public TemplateCADMapping()
        {
            Title = "CADMapping编辑";
            Code = "POI.CADMapping";
            TemplateParams = Params;
        }

        public override IMysqlCommand? GetMysqlCommand() => new MysqlCADMapping();
    }
}
