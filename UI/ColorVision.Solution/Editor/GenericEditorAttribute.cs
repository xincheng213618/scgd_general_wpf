namespace ColorVision.Solution
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GenericEditorAttribute : Attribute
    {
        public string? Name { get; }
        public string? ResourceKey { get; }
        public GenericEditorAttribute(string? name = null, string? resourceKey = null)
        {
            Name = name;
            ResourceKey = resourceKey;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FolderEditorAttribute : Attribute
    {
        public string? Name { get; }
        public string? ResourceKey { get; }
        public FolderEditorAttribute(string? name = null, string? resourceKey = null)
        {
            Name = name;
            ResourceKey = resourceKey;
        }
    }

}