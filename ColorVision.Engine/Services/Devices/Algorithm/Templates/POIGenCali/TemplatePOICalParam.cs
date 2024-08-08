using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POIGenCali
{
    public class TemplatePoiGenCalParam : ITemplate<PoiGenCaliParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiGenCaliParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiGenCaliParam>>();

        public TemplatePoiGenCalParam()
        {
            Title = "PoiGenCali算法设置";
            Code = "POIGenCali";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditPoiGenCali.SetParam(TemplateParams[index].Value);
        }
        public EditPoiGenCali EditPoiGenCali { get; set; } = new EditPoiGenCali();

        public override UserControl GetUserControl()
        {
            return EditPoiGenCali;
        }
    }
}
