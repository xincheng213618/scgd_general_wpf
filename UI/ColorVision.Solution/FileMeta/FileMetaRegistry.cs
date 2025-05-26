using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.Solution.FileMeta
{
    // 注册表实现
    public static class FileMetaRegistry
    {
        private static readonly Dictionary<string, Type> _extTypeMap = new();
        private static Type? _genericType;

        public static void RegisterFileMetasFromAssemblies()
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IFileMeta).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        if (type.GetCustomAttribute<GenericFileAttribute>() != null)
                        {
                            _genericType = type;
                        }
                        else
                        {
                            var extAttr = type.GetCustomAttribute<FileExtensionAttribute>();
                            if (extAttr != null)
                            {
                                foreach (var ext in extAttr.Extensions)
                                    _extTypeMap[ext.ToLowerInvariant()] = type;
                            }
                        }
                    }
                }
            }
        }

        public static Type? GetFileMetaTypeByExtension(string extension)
        {
            if (_extTypeMap.TryGetValue(extension.ToLowerInvariant(), out var type))
                return type;
            return _genericType; // fallback
        }
    }
}
