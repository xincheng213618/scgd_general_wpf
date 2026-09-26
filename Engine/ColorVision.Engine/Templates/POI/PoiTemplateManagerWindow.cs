using ColorVision.Engine.Templates.Browser;
using ColorVision.Engine.Templates.Flow;

namespace ColorVision.Engine.Templates.POI;

/// <summary>Compact POI icon tiles; browsing never requests point details for a preview.</summary>
public sealed class PoiTemplateManagerWindow : TemplateBrowserWindow
{
    public PoiTemplateManagerWindow(TemplatePoi template, int selectedIndex = 0)
        : this(template, selectedIndex, FlowTemplateCoverService.Shared) { }

    internal PoiTemplateManagerWindow(TemplatePoi template, int selectedIndex, FlowTemplateCoverService coverService)
        : base(template, selectedIndex, new TemplateBrowserOptions("POI", false,
            item => template.Save((TemplateModel<PoiParam>)item)), coverService) { }
}
