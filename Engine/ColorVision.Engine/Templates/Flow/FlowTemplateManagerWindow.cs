using ColorVision.Engine.Templates.Browser;

namespace ColorVision.Engine.Templates.Flow;

/// <summary>Flow covers with the shared template browsing interactions.</summary>
public sealed class FlowTemplateManagerWindow : TemplateBrowserWindow
{
    public FlowTemplateManagerWindow(TemplateFlow template, int selectedIndex = 0)
        : this(template, selectedIndex, FlowTemplateCoverService.Shared) { }

    internal FlowTemplateManagerWindow(TemplateFlow template, int selectedIndex, FlowTemplateCoverService coverService)
        : base(template, selectedIndex, new TemplateBrowserOptions("流程", true,
            item => template.Save((TemplateModel<FlowParam>)item)), coverService) { }
}
