using System;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.Configuration
{
    /// <summary>
    /// 服务定位器 - 用于解耦组件间的依赖
    /// </summary>
    public interface IServiceLocator
    {
        /// <summary>
        /// 注册服务
        /// </summary>
        void Register<TService>(TService instance) where TService : class;
        void Register<TService>(Func<TService> factory) where TService : class;
        void Register<TInterface, TImplementation>() where TImplementation : class, TInterface;

        /// <summary>
        /// 获取服务
        /// </summary>
        TService GetService<TService>() where TService : class;
        object GetService(Type serviceType);

        /// <summary>
        /// 尝试获取服务
        /// </summary>
        bool TryGetService<TService>(out TService service) where TService : class;
        bool TryGetService(Type serviceType, out object service);

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        bool IsRegistered<TService>();
        bool IsRegistered(Type serviceType);

        /// <summary>
        /// 移除服务
        /// </summary>
        bool Unregister<TService>();
        bool Unregister(Type serviceType);

        /// <summary>
        /// 清空所有服务
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// 服务定位器实现 - 简单的依赖注入容器
    /// </summary>
    public class ServiceLocator : IServiceLocator
    {
        private static readonly Lazy<ServiceLocator> _instance =
            new Lazy<ServiceLocator>(() => new ServiceLocator());

        public static IServiceLocator Instance => _instance.Value;

        private readonly Dictionary<Type, ServiceEntry> _services = new Dictionary<Type, ServiceEntry>();
        private readonly object _lock = new object();

        public void Register<TService>(TService instance) where TService : class
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var serviceType = typeof(TService);
            lock (_lock)
            {
                _services[serviceType] = new ServiceEntry(instance);
            }
        }

        public void Register<TService>(Func<TService> factory) where TService : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var serviceType = typeof(TService);
            lock (_lock)
            {
                _services[serviceType] = new ServiceEntry(factory);
            }
        }

        public void Register<TInterface, TImplementation>() where TImplementation : class, TInterface
        {
            var interfaceType = typeof(TInterface);
            var implementationType = typeof(TImplementation);

            lock (_lock)
            {
                _services[interfaceType] = new ServiceEntry(implementationType);
            }
        }

        public TService GetService<TService>() where TService : class
        {
            return (TService)GetService(typeof(TService));
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            lock (_lock)
            {
                if (!_services.TryGetValue(serviceType, out var entry))
                {
                    throw new ServiceNotRegisteredException($"服务 {serviceType.FullName} 未注册");
                }

                return entry.GetInstance();
            }
        }

        public bool TryGetService<TService>(out TService service) where TService : class
        {
            if (TryGetService(typeof(TService), out var objService))
            {
                service = (TService)objService;
                return true;
            }

            service = null;
            return false;
        }

        public bool TryGetService(Type serviceType, out object service)
        {
            if (serviceType == null)
            {
                service = null;
                return false;
            }

            lock (_lock)
            {
                if (_services.TryGetValue(serviceType, out var entry))
                {
                    try
                    {
                        service = entry.GetInstance();
                        return true;
                    }
                    catch
                    {
                        service = null;
                        return false;
                    }
                }
            }

            service = null;
            return false;
        }

        public bool IsRegistered<TService>()
        {
            return IsRegistered(typeof(TService));
        }

        public bool IsRegistered(Type serviceType)
        {
            if (serviceType == null)
                return false;

            lock (_lock)
            {
                return _services.ContainsKey(serviceType);
            }
        }

        public bool Unregister<TService>()
        {
            return Unregister(typeof(TService));
        }

        public bool Unregister(Type serviceType)
        {
            if (serviceType == null)
                return false;

            lock (_lock)
            {
                return _services.Remove(serviceType);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _services.Clear();
            }
        }

        /// <summary>
        /// 服务条目
        /// </summary>
        private class ServiceEntry
        {
            private readonly object _instance;
            private readonly Func<object> _factory;
            private readonly Type _implementationType;

            public ServiceEntry(object instance)
            {
                _instance = instance;
            }

            public ServiceEntry(Func<object> factory)
            {
                _factory = factory;
            }

            public ServiceEntry(Type implementationType)
            {
                _implementationType = implementationType;
            }

            public object GetInstance()
            {
                if (_instance != null)
                    return _instance;

                if (_factory != null)
                    return _factory();

                if (_implementationType != null)
                    return Activator.CreateInstance(_implementationType);

                throw new InvalidOperationException("服务条目未正确初始化");
            }
        }
    }

    /// <summary>
    /// 服务未注册异常
    /// </summary>
    public class ServiceNotRegisteredException : Exception
    {
        public ServiceNotRegisteredException(string message) : base(message) { }
        public ServiceNotRegisteredException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// 编辑器服务容器扩展
    /// </summary>
    public static class EditorServiceExtensions
    {
        /// <summary>
        /// 注册命令管理器
        /// </summary>
        public static void RegisterCommandManager(this IServiceLocator locator)
        {
            locator.Register<ICommandManager>(() => new CommandManager());
        }

        /// <summary>
        /// 注册配置管理器
        /// </summary>
        public static void RegisterConfiguration(this IServiceLocator locator, IEditorConfiguration configuration)
        {
            locator.Register<IEditorConfiguration>(configuration);
        }

        /// <summary>
        /// 获取命令管理器
        /// </summary>
        public static ICommandManager GetCommandManager(this IServiceLocator locator)
        {
            return locator.GetService<ICommandManager>();
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        public static IEditorConfiguration GetConfiguration(this IServiceLocator locator)
        {
            return locator.GetService<IEditorConfiguration>();
        }
    }
}
