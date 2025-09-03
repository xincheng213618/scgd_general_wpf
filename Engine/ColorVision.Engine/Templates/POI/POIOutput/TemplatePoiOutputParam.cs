using ColorVision.Database;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.POIOutput
{
    public class TemplatePoiOutputParam : ITemplate<PoiOutputParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiOutputParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiOutputParam>>();

        public TemplatePoiOutputParam()
        {
            Title = "Poi文件输出模板设置";
            TemplateDicId = 27;
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
        public override UserControl CreateUserControl() => new EditPoiOutput();

        public override IMysqlCommand? GetMysqlCommand() => new MysqlPoiOutput();
    }
}
