using ColorVision.UI;
using System.Reflection;

namespace ColorVision.Solution.FolderMeta
{
    /// <summary>
    /// Registry for automatic folder meta registration
    /// </summary>
    public static class FolderMetaRegistry
    {
        private static readonly Dictionary<string, Type> _patternTypeMap = new();
        private static Type? _genericType;

        /// <summary>
        /// Register folder metas from all assemblies using attributes
        /// </summary>
        public static void RegisterFolderMetasFromAssemblies()
        {
            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IFolderMeta).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        var genericAttr = type.GetCustomAttribute<GenericFolderMetaAttribute>();
                        if (genericAttr != null)
                        {
                            _genericType = type;
                        }
                        else
                        {
                            var patternAttr = type.GetCustomAttribute<FolderMetaForPatternAttribute>();
                            if (patternAttr != null)
                            {
                                foreach (var pattern in patternAttr.Patterns)
                                    _patternTypeMap[pattern.ToLowerInvariant()] = type;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get folder meta type by directory pattern or name
        /// </summary>
        /// <param name="directoryInfo">Directory information</param>
        /// <returns>Folder meta type or generic type as fallback</returns>
        public static Type? GetFolderMetaType(DirectoryInfo directoryInfo)
        {
            var dirName = directoryInfo.Name.ToLowerInvariant();
            
            // Check for exact name match first
            if (_patternTypeMap.TryGetValue(dirName, out var type))
                return type;
            
            // Check for pattern matches
            foreach (var pattern in _patternTypeMap.Keys)
            {
                if (dirName.Contains(pattern) || System.Text.RegularExpressions.Regex.IsMatch(dirName, pattern))
                {
                    return _patternTypeMap[pattern];
                }
            }
            
            return _genericType; // fallback to generic
        }

        /// <summary>
        /// Get all registered folder meta types
        /// </summary>
        public static IEnumerable<Type> GetAllFolderMetaTypes()
        {
            var types = new List<Type>(_patternTypeMap.Values);
            if (_genericType != null)
                types.Add(_genericType);
            return types.Distinct();
        }
    }
}