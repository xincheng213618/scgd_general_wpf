using System;

namespace ColorVision.UI
{
    public interface IConfigService
    {
        /// <summary>
        /// 按类型获取配置单例。如果尚未加载，则从持久化存储加载或创建默认实例。
        /// </summary>
        IConfig GetRequiredService(Type type);

        /// <summary>
        /// 按泛型获取配置单例（语法糖）。
        /// </summary>
        T1 GetRequiredService<T1>() where T1 : IConfig;

        void SaveConfigs();
        void LoadConfigs();

        void Save<T1>() where T1 : IConfig;
    }
}
