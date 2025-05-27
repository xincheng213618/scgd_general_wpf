namespace System.ComponentModel
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FileExtensionAttribute : Attribute
    {
        public string[] Extensions { get; }
        public FileExtensionAttribute(params string[] extensions)
        {
            Extensions = extensions;
        }
    }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GenericFileAttribute : Attribute { }
}
