using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ProjectARVRPro
{
    [DisplayName("ARVR上下限判定")]
    public class RecipeConfig : ViewModelBase
    {
        public RecipeConfig()
        {
            Configs = new Dictionary<Type, IRecipeConfig>();
        }
        public Dictionary<Type, IRecipeConfig> Configs { get; set; }

        public T GetRequiredService<T>() where T : IRecipeConfig
        {
            var type = typeof(T);

            if (Configs.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            if (Activator.CreateInstance(type) is IRecipeConfig defaultConfig)
            {
                Configs[type] = defaultConfig;
            }
            // 此处递归调用是为了确保缓存和异常处理逻辑一致
            return GetRequiredService<T>();
        }
    }
}
