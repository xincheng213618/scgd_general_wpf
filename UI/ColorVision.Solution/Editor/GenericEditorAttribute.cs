namespace ColorVision.Solution
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GenericEditorAttribute : Attribute
    {
        public string? Name { get; }
        public GenericEditorAttribute(string? name = null)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FolderEditorAttribute : Attribute
    {
        public string? Name { get; }
        public FolderEditorAttribute(string? name = null)
        {
            Name = name;
        }
    }

}