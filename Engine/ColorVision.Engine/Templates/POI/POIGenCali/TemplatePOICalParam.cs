using ColorVision.Engine.MySql;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.POIGenCali
{
    public class TemplatePoiGenCalParam : ITemplate<PoiGenCaliParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiGenCaliParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiGenCaliParam>>();

        public TemplatePoiGenCalParam()
        {
            Title = "Poi修正标定参数模板设置";
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
