#pragma warning disable

using ColorVision.Common.MVVM;
using ProjectLUX;
using ProjectLUX.Fix;
using System.ComponentModel;

namespace ProjectLUX.Fix
{

    public class FixConfig : ViewModelBase
    {
        public FixConfig()
        {
            Configs = new Dictionary<Type, IFixConfig>();

        }
        public Dictionary<Type, IFixConfig> Configs { get; set; }

        public T GetRequiredService<T>() where T : IFixConfig
        {
            var type = typeof(T);

            if (Configs.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            if (Activator.CreateInstance(type) is IFixConfig defaultConfig)
            {
                Configs[type] = defaultConfig;
            }
            // 此处递归调用是为了确保缓存和异常处理逻辑一致
            return GetRequiredService<T>();
        }

    }

}