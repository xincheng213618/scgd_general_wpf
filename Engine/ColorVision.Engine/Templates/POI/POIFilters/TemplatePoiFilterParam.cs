using ColorVision.Database;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.POIFilters
{
    public class TemplatePoiFilterParam : ITemplate<PoiFilterParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiFilterParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiFilterParam>>();

        public TemplatePoiFilterParam()
        {
            Title =ColorVision.Engine.Properties.Resources.POIFilterTemplateSetting;
            TemplateDicId = 23;
            Code = "POIFilter";
            TemplateParams = Params;
            IsUserControl = true;
        }
        public override IMysqlCommand? GetMysqlCommand() => new MysqlPOIFilter();

        public override void SetUserControlDataContext(int index)
        {
            EditPOIFilters.SetParam(TemplateParams[index].Value);
        }
        private EditPoiFilters? _editPOIFilters;
        public EditPoiFilters EditPOIFilters
        {
            get => _editPOIFilters ??= new EditPoiFilters();
            set => _editPOIFilters = value;
        }

        public override UserControl GetUserControl()
        {
            return EditPOIFilters;
        }
        public override UserControl CreateUserControl() => new EditPoiFilters();

    }
}
