using ColorVision.Engine.Templates.Browser;
using ColorVision.Engine.FlowProcessing.PostProcess;
using System;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow;

/// <summary>Flow covers with the shared template browsing interactions.</summary>
public sealed class FlowTemplateManagerWindow : TemplateBrowserWindow
{
    public FlowTemplateManagerWindow(TemplateFlow template, int selectedIndex = 0)
        : this(template, selectedIndex, FlowTemplateCoverService.Shared) { }

    internal FlowTemplateManagerWindow(TemplateFlow template, int selectedIndex, FlowTemplateCoverService coverService)
        : base(template, selectedIndex, new TemplateBrowserOptions(Properties.Resources.Flow, true,
            item => template.Save((TemplateModel<FlowParam>)item),
            item => PostProcessManager.BindLegacyTemplatesBeforeRename((TemplateModel<FlowParam>)item)), coverService)
    {
        var unavailable = TemplateFlow.UnavailableLocalTemplates;
        if (unavailable.Count == 0)
            return;

        HeadingText.Text += " · " + EngineLocalization.Format($"{unavailable.Count} 个本地流程无法读取");
        HeadingText.ToolTip = string.Join(Environment.NewLine, unavailable.Select(item => $"{item.Name} ({item.Id})"));
    }
}
