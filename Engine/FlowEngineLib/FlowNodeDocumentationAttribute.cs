using System;

namespace FlowEngineLib;

/// <summary>
/// Supplies structured, read-only usage documentation for a flow node.
/// Text values may be resource keys or literal fallback text.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class FlowNodeDocumentationAttribute : Attribute
{
    public FlowNodeDocumentationAttribute(string summary)
    {
        Summary = summary;
    }

    public string Summary { get; }

    public string Usage { get; set; } = string.Empty;

    public string Processing { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}
