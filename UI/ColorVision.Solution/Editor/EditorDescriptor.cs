namespace ColorVision.Solution.Editor
{
    public enum EditorResourceKind
    {
        File,
        Folder,
    }

    /// <summary>
    /// Stable editor registration consumed by the open service and Open With UI.
    /// Attribute discovery is kept as a compatibility adapter; callers should use
    /// descriptors instead of relying on reflection order or concrete editor types.
    /// </summary>
    public sealed record EditorDescriptor(
        string Id,
        Type EditorType,
        EditorResourceKind ResourceKind,
        IReadOnlyList<string> Extensions,
        bool IsGeneric,
        bool IsDefault,
        int Priority,
        bool IsVisibleInOpenWith,
        string? DisplayName = null)
    {
        /// <summary>
        /// Optional creation hook for editors that require injected dependencies.
        /// The editor type remains the stable identity and compatibility metadata.
        /// </summary>
        public Func<IEditor>? Factory { get; init; }

        public bool SupportsExtension(string extension)
        {
            if (ResourceKind != EditorResourceKind.File)
                return false;
            if (IsGeneric)
                return true;

            string normalizedExtension = EditorManager.NormalizeExtension(extension);
            return Extensions.Contains(normalizedExtension, StringComparer.OrdinalIgnoreCase);
        }
    }
}
