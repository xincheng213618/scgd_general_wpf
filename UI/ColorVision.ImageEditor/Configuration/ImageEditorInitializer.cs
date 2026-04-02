using System;

namespace ColorVision.ImageEditor.Configuration
{
    /// <summary>
    /// ImageEditor 初始化器 - 配置依赖注入和服务注册
    /// </summary>
    public static class ImageEditorInitializer
    {
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// 初始化 ImageEditor 模块
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
                return;

            lock (_lock)
            {
                if (_isInitialized)
                    return;

                var services = ServiceLocator.Instance;

                // 注册命令管理器
                services.Register<ICommandManager>(() => new CommandManager
                {
                    MaxHistorySize = 100
                });

                // 注册事务性命令管理器（与上面同一个实例）
                services.Register<ITransactionalCommandManager>(() =>
                    services.GetService<ICommandManager>() as ITransactionalCommandManager);

                // 注册默认配置
                services.Register<IEditorConfiguration>(new ImageEditorConfiguration("Default"));

                _isInitialized = true;
            }
        }

        /// <summary>
        /// 使用自定义配置初始化
        /// </summary>
        public static void Initialize(IEditorConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            lock (_lock)
            {
                var services = ServiceLocator.Instance;

                // 注册命令管理器
                services.Register<ICommandManager>(() => new CommandManager
                {
                    MaxHistorySize = 100
                });

                // 注册事务性命令管理器
                services.Register<ITransactionalCommandManager>(() =>
                    services.GetService<ICommandManager>() as ITransactionalCommandManager);

                // 注册自定义配置
                services.Register<IEditorConfiguration>(configuration);

                _isInitialized = true;
            }
        }

        /// <summary>
        /// 重置初始化状态（用于测试）
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                ServiceLocator.Instance.Clear();
                _isInitialized = false;
            }
        }
    }
}
