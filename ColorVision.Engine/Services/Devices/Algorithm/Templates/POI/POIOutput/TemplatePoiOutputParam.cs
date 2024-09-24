using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.PoiOutput
{
    public class TemplatePoiOutputParam : ITemplate<PoiOutputParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiOutputParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiOutputParam>>();

        public TemplatePoiOutputParam()
        {
            Title = "PoiOutput算法设置";
            Code = "PoiOutput";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditControl.SetParam(TemplateParams[index].Value);
        }

        public EditPoiOutput EditControl { get; set; } = new EditPoiOutput();
        public override UserControl GetUserControl()
        {
            return EditControl;
        }
    }
}
