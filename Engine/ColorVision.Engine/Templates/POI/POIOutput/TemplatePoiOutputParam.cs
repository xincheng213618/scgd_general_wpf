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
            Title =ColorVision.Engine.Properties.Resources.PoiFileOutputTemplateSetting;
            TemplateDicId = 27;
            Code = "PoiOutput";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditControl.SetParam(TemplateParams[index].Value);
        }

        private EditPoiOutput? _editControl;
        public EditPoiOutput EditControl
        {
            get => _editControl ??= new EditPoiOutput();
            set => _editControl = value;
        }
        public override UserControl GetUserControl()
        {
            return EditControl;
        }
        public override UserControl CreateUserControl() => new EditPoiOutput();

        public override IMysqlCommand? GetMysqlCommand() => new MysqlPoiOutput();
    }
}
