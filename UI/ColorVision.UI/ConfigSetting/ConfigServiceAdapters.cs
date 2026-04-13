using System;
using System.Collections.Generic;
using System.Reflection;

#pragma warning disable CA1822

namespace ColorVision.UI
{
    /// <summary>
    /// 用于自维护单例的配置类的适配器。
    /// 配置类需要有公开的静态 Instance 属性。
    /// 
    /// 示例：
    /// public class MyConfig : IConfig
    /// {
    ///     public static MyConfig Instance { get; } = new();
    ///     // ...
    /// }
    /// </summary>
    public class SelfManagedConfigServiceAdapter : IConfigService
    {
        private readonly Dictionary<Type, Lazy<IConfig>> _instances = new();

        public IConfig GetRequiredService(Type type)
        {
            if (!typeof(IConfig).IsAssignableFrom(type))
                throw new ArgumentException($"Type {type.FullName} must implement IConfig", nameof(type));

            if (!_instances.TryGetValue(type, out var lazy))
            {
                lazy = new Lazy<IConfig>(() => StaticResolveInstance(type));
                _instances[type] = lazy;
            }
            return lazy.Value;
        }

        public T1 GetRequiredService<T1>() where T1 : IConfig
            => (T1)GetRequiredService(typeof(T1));

        public void SaveConfigs() => throw new NotSupportedException("Not implemented for self-managed configs");
        public void LoadConfigs() => throw new NotSupportedException("Not implemented for self-managed configs");
        public void Save<T1>() where T1 : IConfig => throw new NotSupportedException("Not implemented for self-managed configs");

        private static IConfig StaticResolveInstance(Type type)
        {
            // 寻找 public static Instance 属性
            var instanceProp = type.GetProperty("Instance",
                BindingFlags.Static | BindingFlags.Public);

            if (instanceProp?.GetValue(null) is IConfig instance)
                return instance;

            throw new InvalidOperationException(
                $"Type {type.FullName} must have a public static Instance property that returns an IConfig instance");
        }
    }

    /// <summary>
    /// 混合适配器：支持显式注册的实例 + 反射查找 static Instance
    /// 优先返回已注册的实例，其次查找 static Instance 属性
    /// </summary>
    public class HybridConfigServiceAdapter : IConfigService
    {
        private readonly Dictionary<Type, IConfig> _registered = new();
        private readonly Dictionary<Type, Lazy<IConfig>> _resolved = new();

        /// <summary>
        /// 显式注册一个配置实例
        /// </summary>
        public void Register<T>(T instance) where T : IConfig
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            _registered[typeof(T)] = instance;
            _resolved.Remove(typeof(T)); // 清除缓存
        }

        public IConfig GetRequiredService(Type type)
        {
            if (!typeof(IConfig).IsAssignableFrom(type))
                throw new ArgumentException($"Type {type.FullName} must implement IConfig", nameof(type));

            // 优先检查已注册的实例
            if (_registered.TryGetValue(type, out var registered))
                return registered;

            // 其次缓存解析结果
            if (!_resolved.TryGetValue(type, out var lazy))
            {
                lazy = new Lazy<IConfig>(() => StaticResolveInstance(type));
                _resolved[type] = lazy;
            }
            return lazy.Value;
        }

        public T1 GetRequiredService<T1>() where T1 : IConfig
            => (T1)GetRequiredService(typeof(T1));

        public void SaveConfigs() => throw new NotSupportedException("Not implemented for hybrid configs");
        public void LoadConfigs() => throw new NotSupportedException("Not implemented for hybrid configs");
        public void Save<T1>() where T1 : IConfig => throw new NotSupportedException("Not implemented for hybrid configs");

        private static IConfig StaticResolveInstance(Type type)
        {
            // 寻找 public static Instance 属性
            var instanceProp = type.GetProperty("Instance",
                BindingFlags.Static | BindingFlags.Public);

            if (instanceProp?.GetValue(null) is IConfig instance)
                return instance;

            throw new InvalidOperationException(
                $"Type {type.FullName} must either be registered via Register<T>() or have a public static Instance property");
        }
    }

    /// <summary>
    /// ASP.NET Core 依赖注入适配器
    /// 将 IServiceProvider 适配为 IConfigService
    /// </summary>
    public class AspNetCoreConfigServiceAdapter : IConfigService
    {
        private readonly IServiceProvider _serviceProvider;

        public AspNetCoreConfigServiceAdapter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public IConfig GetRequiredService(Type type)
        {
            if (!typeof(IConfig).IsAssignableFrom(type))
                throw new ArgumentException($"Type {type.FullName} must implement IConfig", nameof(type));

            try
            {
                return (IConfig)_serviceProvider.GetService(type)
                    ?? throw new InvalidOperationException($"Service of type {type.FullName} not found in IServiceProvider");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to resolve IConfig instance for type {type.FullName}", ex);
            }
        }

        public T1 GetRequiredService<T1>() where T1 : IConfig
            => (T1)GetRequiredService(typeof(T1));

        public void SaveConfigs() => throw new NotSupportedException("ASP.NET Core adapter does not implement config persistence");
        public void LoadConfigs() => throw new NotSupportedException("ASP.NET Core adapter does not implement config persistence");
        public void Save<T1>() where T1 : IConfig => throw new NotSupportedException("ASP.NET Core adapter does not implement config persistence");
    }
}

#pragma warning restore CA1822
