namespace ColorVision.Engine.Templates;

/// <summary>Allows template hosts to reject saving an invalid editor draft.</summary>
public interface ITemplateEditorValidation
{
    bool TryCommitPendingEdits();
    void AcceptSavedChanges();
}
