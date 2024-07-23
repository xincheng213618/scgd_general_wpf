using ColorVision.Engine.Services.Devices.Algorithm.Templates.LEDStripDetection;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.POIFilters
{
    public class TemplatePOIFilterParam : ITemplate<POIFilterParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<POIFilterParam>> Params { get; set; } = new ObservableCollection<TemplateModel<POIFilterParam>>();

        public TemplatePOIFilterParam()
        {
            Title = "POIFilter模板设置";
            Code = "POIFilter";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditPOIFilters.SetParam(TemplateParams[index].Value);
        }
        public EditPOIFilters EditPOIFilters { get; set; } = new EditPOIFilters();

        public override UserControl GetUserControl()
        {
            return EditPOIFilters;
        }
    }
}
