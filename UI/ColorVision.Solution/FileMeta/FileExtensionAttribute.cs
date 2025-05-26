namespace ColorVision.Solution.FileMeta
{
    // 自定义Attribute用于声明扩展名和默认项
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FileExtensionAttribute : Attribute
    {
        public string[] Extensions { get; }
        public bool IsDefault { get; }

        public FileExtensionAttribute(string extensions, bool isDefault = false)
        {
            Extensions = extensions.Split('|');
            IsDefault = isDefault;
        }
    }
}