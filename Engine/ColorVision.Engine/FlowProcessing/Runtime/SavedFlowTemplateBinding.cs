using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing;

internal static class SavedFlowTemplateBinding
{
    internal static IEnumerable<TemplateModel<FlowParam>> SelectableTemplates => TemplateFlow.Params
        .Where(template => !string.IsNullOrWhiteSpace(template.Value.FlowKey))
        .GroupBy(template => template.Value.FlowKey!, StringComparer.Ordinal)
        .Where(group => group.Count() == 1)
        .Select(group => group.First())
        .Where(HasSavedCanvas);

    internal static TemplateModel<FlowParam>? Find(string? flowKey, out bool ambiguous)
    {
        ambiguous = false;
        if (string.IsNullOrWhiteSpace(flowKey))
            return null;

        // Several legacy templates can reference the same resource. A binding
        // containing only its FlowKey cannot identify which template to run.
        var matches = TemplateFlow.Params
            .Where(template => string.Equals(template.Value.FlowKey, flowKey, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        ambiguous = matches.Length > 1;
        return matches.Length == 1 && HasSavedCanvas(matches[0]) ? matches[0] : null;
    }

    internal static string AmbiguousMessage(string? flowKey) =>
        EngineLocalization.Format($"流程标识 {flowKey} 对应多个模板，无法确定要执行的模板。");

    private static bool HasSavedCanvas(TemplateModel<FlowParam> template) =>
        !string.IsNullOrWhiteSpace(template.Value.DataBase64);
}
