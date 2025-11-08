using ColorVision.Database;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.POIGenCali
{
    public class TemplatePoiGenCalParam : ITemplate<PoiGenCaliParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiGenCaliParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiGenCaliParam>>();

        public TemplatePoiGenCalParam()
        {
            Title = ColorVision.Engine.Properties.Resources.POICalibrationCorrectionTemplateSettings;
            TemplateDicId = 25;
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
        public override UserControl CreateUserControl() => new EditPoiGenCali();

        public override IMysqlCommand? GetMysqlCommand()
        {
            return new MysqlPOIFilter();
        }
    }
}
