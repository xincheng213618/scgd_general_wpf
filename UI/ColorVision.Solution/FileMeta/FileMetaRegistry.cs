using ColorVision.UI;
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
            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IFileMeta).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        var genericAttr = type.GetCustomAttribute<GenericFileMetaAttribute>();
                        if (genericAttr != null)
                        {
                            _genericType = type;
                        }
                        else
                        {
                            var extAttr = type.GetCustomAttribute<FileMetaForExtensionAttribute>();
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

        /// <summary>
        /// Get all registered file meta types
        /// </summary>
        public static IEnumerable<Type> GetAllFileMetaTypes()
        {
            var types = new List<Type>(_extTypeMap.Values);
            if (_genericType != null)
                types.Add(_genericType);
            return types.Distinct();
        }
    }
}
