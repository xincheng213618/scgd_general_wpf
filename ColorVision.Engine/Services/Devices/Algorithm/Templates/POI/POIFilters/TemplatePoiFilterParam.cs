﻿using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.POIFilters
{
    public class TemplatePoiFilterParam : ITemplate<PoiFilterParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiFilterParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiFilterParam>>();

        public TemplatePoiFilterParam()
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
        public EditPoiFilters EditPOIFilters { get; set; } = new EditPoiFilters();

        public override UserControl GetUserControl()
        {
            return EditPOIFilters;
        }
    }
}
