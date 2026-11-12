using System;

namespace ColorVision.UI;

public sealed record CopilotFlowValidationSnapshot(
    string Revision,
    bool RevisionMatches,
    bool? IsValid,
    string Diagnostic,
    Guid? InvalidNodeId,
    int NodeCount,
    int ConnectionCount,
    bool IsExecutionActive,
    bool IsEditorReadOnly);
