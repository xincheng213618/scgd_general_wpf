namespace ColorVision.Solution.Editor
{
    // 自定义Attribute用于声明扩展名和默认项
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class EditorForExtensionAttribute : Attribute
    {
        public string? Name { get; }
        public string? ResourceKey { get; }
        public string? EditorId { get; }
        public string[] Extensions { get; }
        public bool IsDefault { get; }
        public int Priority { get; }
        public bool IsVisibleInOpenWith { get; }

        public EditorForExtensionAttribute(
            string extensions,
            string? name = null,
            bool isDefault = false,
            string? resourceKey = null,
            string? editorId = null,
            int priority = 0,
            bool isVisibleInOpenWith = true)
        {
            Name = name;
            ResourceKey = resourceKey;
            EditorId = editorId;
            Extensions = extensions.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            IsDefault = isDefault;
            Priority = priority;
            IsVisibleInOpenWith = isVisibleInOpenWith;
        }
    }
}
