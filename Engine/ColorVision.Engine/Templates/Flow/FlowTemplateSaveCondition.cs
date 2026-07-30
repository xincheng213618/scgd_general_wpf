namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// Carries editor-session state into a template save. This value must be
    /// owned by one document window rather than shared through FlowParam.
    /// </summary>
    public sealed record FlowTemplateSaveCondition(
        string? ExpectedContentHash);
}
