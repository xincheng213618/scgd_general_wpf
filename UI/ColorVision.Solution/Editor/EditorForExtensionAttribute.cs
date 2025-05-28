namespace ColorVision.Solution.Editor
{
    // 自定义Attribute用于声明扩展名和默认项
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class EditorForExtensionAttribute : Attribute
    {
        public string? Name { get; }

        public string[] Extensions { get; }
        public bool IsDefault { get; }

        public EditorForExtensionAttribute(string extensions,string? name =null, bool isDefault = false)
        {
            Name = name;
            Extensions = extensions.Split('|');
            IsDefault = isDefault;
        }
    }
}