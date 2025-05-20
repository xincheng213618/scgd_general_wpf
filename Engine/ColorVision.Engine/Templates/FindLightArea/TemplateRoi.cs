using ColorVision.Engine.MySql;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.FindLightArea
{
    public class TemplateRoi : ITemplate<RoiParam>, IITemplateLoad
    {    
        public static ObservableCollection<TemplateModel<RoiParam>> Params { get; set; } = new ObservableCollection<TemplateModel<RoiParam>>();

        public TemplateRoi()
        {
            Name = "FindLightArea";
            Title = "发光区检测模板管理";
            Code = "FindLightArea";
            TemplateParams = Params;
        }

        public override IMysqlCommand? GetMysqlCommand()
        {
            return new MysqlRoi();
        }
    }
}
