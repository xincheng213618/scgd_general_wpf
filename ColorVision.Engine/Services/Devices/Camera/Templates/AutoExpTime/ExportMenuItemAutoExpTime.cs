﻿using ColorVision.Engine.Services.Devices.Algorithm.Templates.MTF;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam
{
    public class ExportMenuItemAutoExpTime : ExportTemplateBase
    {
        public override string OwnerGuid => "Template";
        public override string GuidId => "AutoExpTime";
        public override string Header => ColorVision.Engine.Properties.Resources.AutoExploreTemplate;
        public override int Order => 23;
        public override ITemplate Template => new TemplateAutoExpTimeParam();
    }
}