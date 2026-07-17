namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// Optional contract for editor content that owns persisted user changes.
    /// The workspace uses it to provide one Save/dirty/close lifecycle without
    /// coupling the document host to a concrete editor control.
    /// </summary>
    public interface IEditorDocumentContent
    {
        bool IsDirty { get; }

        bool CanSave { get; }

        event EventHandler? DocumentStateChanged;

        bool Save();
    }

    /// <summary>
    /// Implemented by editor content that can keep its in-memory document when
    /// the backing file or one of its parent folders is renamed.
    /// </summary>
    public interface IResourcePathAwareDocumentContent
    {
        bool TryUpdateResourcePath(string resourcePath);
    }

    /// <summary>
    /// Implemented by editor content that can replace its in-memory state with
    /// the current contents of the backing resource.
    /// </summary>
    public interface IReloadableEditorDocumentContent
    {
        bool ReloadFromDisk();
    }
}
