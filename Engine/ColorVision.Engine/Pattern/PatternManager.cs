using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace ColorVision.Engine.Pattern
{
    public class PatternManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PatternManager));
        private static PatternManager _instance;
        private static readonly object _locker = new();
        public static PatternManager GetInstance() { lock (_locker) { _instance ??= new PatternManager(); return _instance; } }

        public List<PatternMeta> Patterns { get; set; } = new List<PatternMeta>();
        private PatternManager()
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IPattern).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    try
                    {
                        var displayName = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name;
                        var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                        var category = type.GetCustomAttribute<CategoryAttribute>()?.Category ?? "";

                        IPattern pattern = (IPattern)Activator.CreateInstance(type);
                        if (pattern != null)
                        {
                            var patternMeta = new PatternMeta
                            {
                                Name = displayName,
                                Description = description,
                                Category = category,
                                Pattern = pattern
                            };
                            Patterns.Add(patternMeta);
                            log.Info($"已加载图案生成器: {type.FullName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"加载图案生成器失败: {type.FullName}", ex);
                    }

                }
            }
        }
    }
}
