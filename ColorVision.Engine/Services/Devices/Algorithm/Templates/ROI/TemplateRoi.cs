using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.ROI
{
    public class TemplateRoi : ITemplate<RoiParam>, IITemplateLoad
    {    
        public static ObservableCollection<TemplateModel<RoiParam>> Params { get; set; } = new ObservableCollection<TemplateModel<RoiParam>>();

        public TemplateRoi()
        {
            Title = "发光区检测2";
            Code = "OLED.GetROI";
            TemplateParams = Params;
        }

        public override IMysqlCommand? GetMysqlCommand()
        {
            return new MysqlRoi();
        }
    }
}
