namespace ColorVision.Solution
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GenericEditorAttribute : Attribute
    {
        public string? Name { get; }
        public string? ResourceKey { get; }
        public string? EditorId { get; }
        public bool IsDefault { get; }
        public int Priority { get; }
        public bool IsVisibleInOpenWith { get; }

        public GenericEditorAttribute(
            string? name = null,
            string? resourceKey = null,
            string? editorId = null,
            bool isDefault = false,
            int priority = 0,
            bool isVisibleInOpenWith = true)
        {
            Name = name;
            ResourceKey = resourceKey;
            EditorId = editorId;
            IsDefault = isDefault;
            Priority = priority;
            IsVisibleInOpenWith = isVisibleInOpenWith;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FolderEditorAttribute : Attribute
    {
        public string? Name { get; }
        public string? ResourceKey { get; }
        public string? EditorId { get; }
        public bool IsDefault { get; }
        public int Priority { get; }
        public bool IsVisibleInOpenWith { get; }

        public FolderEditorAttribute(
            string? name = null,
            string? resourceKey = null,
            string? editorId = null,
            bool isDefault = false,
            int priority = 0,
            bool isVisibleInOpenWith = true)
        {
            Name = name;
            ResourceKey = resourceKey;
            EditorId = editorId;
            IsDefault = isDefault;
            Priority = priority;
            IsVisibleInOpenWith = isVisibleInOpenWith;
        }
    }

}
