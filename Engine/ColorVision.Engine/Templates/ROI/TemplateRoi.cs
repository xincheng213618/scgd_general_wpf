using ColorVision.Engine.MySql;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.ROI
{
    public class TemplateRoi : ITemplate<RoiParam>, IITemplateLoad
    {    
        public static ObservableCollection<TemplateModel<RoiParam>> Params { get; set; } = new ObservableCollection<TemplateModel<RoiParam>>();

        public TemplateRoi()
        {
            Title = "发光区检测";
            Code = "OLED.GetROI";
            TemplateParams = Params;
        }

        public override IMysqlCommand? GetMysqlCommand()
        {
            return new MysqlRoi();
        }
    }
}
