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
            Configs ??= new Dictionary<Type, IRecipeConfig>();

            if (Configs.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            if (Activator.CreateInstance(type) is not T defaultConfig)
                throw new InvalidOperationException($"无法创建 Recipe 配置类型 {type.FullName}。");

            Configs[type] = defaultConfig;
            return defaultConfig;
        }
    }
}
