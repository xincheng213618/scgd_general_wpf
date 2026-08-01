using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics;

/// <summary>
/// Internal identity used when querying flow execution history. FlowKey is the
/// canonical identity; TemplateId and FlowName are compatibility fallbacks for
/// records written before a stable FlowKey was available.
/// </summary>
internal readonly record struct FlowIdentity
{
    public FlowIdentity(
        int templateId,
        string? flowKey,
        string? flowName)
    {
        TemplateId = Math.Max(0, templateId);
        FlowKey = Normalize(flowKey);
        FlowName = Normalize(flowName);
    }

    public int TemplateId { get; }

    public string? FlowKey { get; }

    public string? FlowName { get; }

    public bool IsEmpty =>
        TemplateId <= 0
        && FlowKey == null
        && FlowName == null;

    /// <summary>
    /// Mirrors the database query policy for tests and in-memory callers.
    /// A row with a different non-empty FlowKey never matches by TemplateId.
    /// </summary>
    public bool Matches(FlowRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        string? runFlowKey = Normalize(run.FlowKey);
        if (FlowKey != null)
        {
            return runFlowKey != null
                ? string.Equals(
                    FlowKey,
                    runFlowKey,
                    StringComparison.Ordinal)
                : TemplateId > 0
                    && run.TemplateId == TemplateId;
        }

        if (TemplateId > 0)
            return run.TemplateId == TemplateId;

        return run.TemplateId <= 0
            && runFlowKey == null
            && string.Equals(
                FlowName,
                Normalize(run.FlowName),
                StringComparison.Ordinal);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
