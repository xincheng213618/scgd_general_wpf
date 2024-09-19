using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.ROI
{
    public class TemplateROIParam : ITemplate<ROIParam>, IITemplateLoad
    {    
        public static ObservableCollection<TemplateModel<ROIParam>> Params { get; set; } = new ObservableCollection<TemplateModel<ROIParam>>();

        public TemplateROIParam()
        {
            Title = "JNDParam算法设置";
            Code = "OLED.GetROI";
            TemplateParams = Params;
        }
    }
}
