﻿using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.POIGenCali
{
    public class ExportPoiGenCalParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplatePOI";
        public override string GuidId => "PoiGenCali";
        public override string Header => "PoiGenCali算法设置";
        public override int Order => 2;
        public override ITemplate Template => new TemplatePoiGenCalParam();
    }
}
