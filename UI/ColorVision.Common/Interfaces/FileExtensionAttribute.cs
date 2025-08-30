namespace System.ComponentModel
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FileExtensionAttribute : Attribute
    {
        public string? Name { get; }

        public string[] Extensions { get; }

        public FileExtensionAttribute(string extensions, string? name = null)
        {
            Name = name;
            Extensions = extensions.Split('|');
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GenericFileAttribute : Attribute { }
}
