using System;
using System.Collections.Generic;
using Xunit;
using ColorVision.UI;

namespace ColorVision.UI.Tests
{
    /// <summary>
    /// 验证不同 IConfigService 实现与 ConfigSettingManager 的兼容性
    /// </summary>
    public class ConfigServiceAdaptersTests
    {
        #region 测试配置类

        public interface ITestConfig : IConfig
        {
            string Name { get; set; }
        }

        public class SelfManagedConfig : ITestConfig
        {
            public static SelfManagedConfig Instance { get; } = new();

            public string Name { get; set; } = "SelfManaged";

            // IConfig 实现
            public void Load() => _ = 0; 
            public void Save() => _ = 0;
        }

        #endregion

        #region SelfManagedConfigServiceAdapter 测试

        [Fact]
        public void SelfManagedAdapter_ResolveExistingStaticInstance()
        {
            var adapter = new SelfManagedConfigServiceAdapter();
            var config = adapter.GetRequiredService(typeof(SelfManagedConfig));

            Assert.NotNull(config);
            Assert.IsType<SelfManagedConfig>(config);
            Assert.Same(SelfManagedConfig.Instance, config);
        }

        [Fact]
        public void SelfManagedAdapter_Generic_ResolveExistingStaticInstance()
        {
            var adapter = new SelfManagedConfigServiceAdapter();
            var config = adapter.GetRequiredService<SelfManagedConfig>();

            Assert.NotNull(config);
            Assert.Same(SelfManagedConfig.Instance, config);
        }

        [Fact]
        public void SelfManagedAdapter_MultipleCallsReturnSameInstance()
        {
            var adapter = new SelfManagedConfigServiceAdapter();
            var config1 = adapter.GetRequiredService(typeof(SelfManagedConfig));
            var config2 = adapter.GetRequiredService(typeof(SelfManagedConfig));

            Assert.Same(config1, config2);
        }

        [Fact]
        public void SelfManagedAdapter_ThrowsForTypeWithoutStaticInstance()
        {
            var adapter = new SelfManagedConfigServiceAdapter();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                adapter.GetRequiredService(typeof(InvalidConfig)));

            Assert.Contains("public static Instance property", ex.Message);
        }

        [Fact]
        public void SelfManagedAdapter_ThrowsForNonConfigType()
        {
            var adapter = new SelfManagedConfigServiceAdapter();

            var ex = Assert.Throws<ArgumentException>(() =>
                adapter.GetRequiredService(typeof(string)));

            Assert.Contains("must implement IConfig", ex.Message);
        }

        #endregion

        #region HybridConfigServiceAdapter 测试

        [Fact]
        public void HybridAdapter_RegisteredInstanceTakesPrecedence()
        {
            var adapter = new HybridConfigServiceAdapter();
            var customInstance = new SelfManagedConfig { Name = "Custom" };
            adapter.Register(customInstance);

            var resolved = adapter.GetRequiredService<SelfManagedConfig>();

            Assert.Same(customInstance, resolved);
            Assert.NotSame(SelfManagedConfig.Instance, resolved);
        }

        [Fact]
        public void HybridAdapter_FallsBackToStaticInstanceIfNotRegistered()
        {
            var adapter = new HybridConfigServiceAdapter();
            // 不注册 SelfManagedConfig，应该回退到 static Instance

            var resolved = adapter.GetRequiredService<SelfManagedConfig>();

            Assert.Same(SelfManagedConfig.Instance, resolved);
        }

        [Fact]
        public void HybridAdapter_ClearsResolvedCacheWhenReregistered()
        {
            var adapter = new HybridConfigServiceAdapter();
            var instance1 = new SelfManagedConfig { Name = "First" };
            adapter.Register(instance1);
            var resolved1 = adapter.GetRequiredService<SelfManagedConfig>();

            var instance2 = new SelfManagedConfig { Name = "Second" };
            adapter.Register(instance2);
            var resolved2 = adapter.GetRequiredService<SelfManagedConfig>();

            Assert.Same(instance1, resolved1);
            Assert.Same(instance2, resolved2);
            Assert.NotSame(resolved1, resolved2);
        }

        [Fact]
        public void HybridAdapter_RegisterWithGeneric()
        {
            var adapter = new HybridConfigServiceAdapter();
            var config = new SelfManagedConfig { Name = "RegisteredGeneric" };
            adapter.Register(config);

            var resolved = adapter.GetRequiredService<SelfManagedConfig>();

            Assert.Same(config, resolved);
        }

        #endregion

        #region AspNetCoreConfigServiceAdapter 测试

        [Fact]
        public void AspNetCoreAdapter_ResolveFromServiceProvider()
        {
            var config = new SelfManagedConfig { Name = "AspNetCore" };
            var mockProvider = new MockServiceProvider(typeof(SelfManagedConfig), config);
            var adapter = new AspNetCoreConfigServiceAdapter(mockProvider);

            var resolved = adapter.GetRequiredService(typeof(SelfManagedConfig));

            Assert.NotNull(resolved);
            Assert.Same(config, resolved);
        }

        [Fact]
        public void AspNetCoreAdapter_GenericResolve()
        {
            var config = new SelfManagedConfig { Name = "AspNetCore" };
            var mockProvider = new MockServiceProvider(typeof(SelfManagedConfig), config);
            var adapter = new AspNetCoreConfigServiceAdapter(mockProvider);

            var resolved = adapter.GetRequiredService<SelfManagedConfig>();

            Assert.Same(config, resolved);
        }

        [Fact]
        public void AspNetCoreAdapter_ThrowsForUnresolvedType()
        {
            var mockProvider = new MockServiceProvider();
            var adapter = new AspNetCoreConfigServiceAdapter(mockProvider);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                adapter.GetRequiredService(typeof(SelfManagedConfig)));

            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public void AspNetCoreAdapterThrowsIfProviderIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new AspNetCoreConfigServiceAdapter(null!));

            Assert.Equal("serviceProvider", ex.ParamName);
        }

        #endregion

        #region 配置管理集成测试

        /// <summary>
        /// 模拟 ConfigSettingManager 如何使用不同的 IConfigService 实现
        /// </summary>
        [Fact]
        public void ConfigSettingManager_WorksWithSelfManagedAdapter()
        {
            var adapter = new SelfManagedConfigServiceAdapter();
            // ConfigSettingManager 仅需要调用：
            var config = adapter.GetRequiredService(typeof(SelfManagedConfig));

            Assert.NotNull(config);
        }

        [Fact]
        public void ConfigSettingManager_WorksWithHybridAdapter()
        {
            var adapter = new HybridConfigServiceAdapter();
            var instance = new SelfManagedConfig();
            adapter.Register(instance);

            var config = adapter.GetRequiredService(typeof(SelfManagedConfig));

            Assert.Same(instance, config);
        }

        [Fact]  
        public void ConfigSettingManager_WorksWithAspNetCoreAdapter()
        {
            var config = new SelfManagedConfig();
            var mockProvider = new MockServiceProvider(typeof(SelfManagedConfig), config);
            var adapter = new AspNetCoreConfigServiceAdapter(mockProvider);

            var resolved = adapter.GetRequiredService(typeof(SelfManagedConfig));

            Assert.Same(config, resolved);
        }

        #endregion

        #region 测试工具

        /// <summary>
        /// 模拟 IServiceProvider 用于测试
        /// </summary>
        private class MockServiceProvider : IServiceProvider
        {
            private readonly Dictionary<Type, object> _services = new();

            public MockServiceProvider(Type type, object instance)
            {
                _services[type] = instance;
            }

            public MockServiceProvider()
            {
            }

            public object? GetService(Type serviceType)
                => _services.TryGetValue(serviceType, out var service) ? service : null;
        }

        /// <summary>
        /// 没有 static Instance 的无效配置类
        /// </summary>
        public class InvalidConfig : IConfig
        {
            public void Load() => _ = 0;
            public void Save() => _ = 0;
        }

        #endregion
    }
}
