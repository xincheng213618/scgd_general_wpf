﻿namespace ColorVision.Engine.Templates.POI.POIFilters
{
    public class MenuPoiFliterParam : MenuTemplatePoiBase
    {
        public override string Header => "Poi过滤模板设置";
        public override int Order => 1;
        public override ITemplate Template => new TemplatePoiFilterParam();
    }
}
