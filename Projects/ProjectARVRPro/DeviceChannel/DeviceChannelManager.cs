using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;

namespace ProjectARVRPro.DeviceChannel
{
    /// <summary>
    /// 设备通道管理器 — 管理所有设备通道的生命周期、配置持久化和指令调度
    /// <para>单例模式，在一键执行前通过 <see cref="GetOrCreateChannelAsync"/> 获取/创建通道，
    /// 通道在整个测试流程中保持长连接，测试结束后调用 <see cref="DisconnectAllAsync"/> 断开。</para>
    /// </summary>
    public class DeviceChannelManager : ViewModelBase, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceChannelManager));
        private const string PersistFileName = "DeviceChannels.json";
        private static string PersistDirectory => ViewResultManager.DirectoryPath;
        private static string PersistFilePath => Path.Combine(PersistDirectory, PersistFileName);

        private static DeviceChannelManager? _instance;
        private static readonly object _locker = new();

        public static DeviceChannelManager GetInstance()
        {
            lock (_locker) { _instance ??= new DeviceChannelManager(); return _instance; }
        }

        /// <summary>
        /// 活跃的通道实例（按通道名称索引）
        /// </summary>
        private readonly Dictionary<string, IDeviceChannel> _channels = new();

        /// <summary>
        /// 通道配置列表（持久化）
        /// </summary>
        public ObservableCollection<DeviceChannelConfig> ChannelConfigs { get; set; } = new();

        [JsonIgnore]
        public RelayCommand EditCommand { get; }

        private DeviceChannelManager()
        {
            EditCommand = new RelayCommand(a => Edit());
            Load();
        }

        private void Edit()
        {
            new PropertyEditorWindow(this)
            {
                Owner = System.Windows.Application.Current.GetActiveWindow(),
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner
            }.ShowDialog();
            Save();
        }

        /// <summary>
        /// 获取已存在的通道实例，如果不存在或未连接则创建并连接
        /// </summary>
        public async Task<IDeviceChannel> GetOrCreateChannelAsync(DeviceChannelConfig config)
        {
            if (_channels.TryGetValue(config.Name, out var existing) && existing.IsConnected)
                return existing;

            // 清理旧通道
            if (existing != null)
            {
                existing.Dispose();
                _channels.Remove(config.Name);
            }

            var channel = CreateChannel(config);
            await channel.ConnectAsync();
            _channels[config.Name] = channel;
            log.Info($"通道已创建并连接: {config.Name} ({config.ChannelType})");
            return channel;
        }

        /// <summary>
        /// 根据名称获取已连接的通道
        /// </summary>
        public IDeviceChannel? GetChannel(string name)
        {
            return _channels.TryGetValue(name, out var ch) && ch.IsConnected ? ch : null;
        }

        /// <summary>
        /// 根据配置查找对应的 DeviceChannelConfig
        /// </summary>
        public DeviceChannelConfig? FindConfig(string channelName)
        {
            return ChannelConfigs.FirstOrDefault(c => c.Name == channelName);
        }

        /// <summary>
        /// 根据通道类型查找第一个启用的配置
        /// </summary>
        public DeviceChannelConfig? FindConfigByType(DeviceChannelType channelType)
        {
            return ChannelConfigs.FirstOrDefault(c => c.IsEnabled && c.ChannelType == channelType);
        }

        /// <summary>
        /// 执行指令 — 自动查找/创建通道并发送指令
        /// </summary>
        public async Task<DeviceCommandResult> ExecuteAsync(DeviceChannelConfig config, string command, int? timeoutMs = null)
        {
            try
            {
                var channel = await GetOrCreateChannelAsync(config);
                return await channel.ExecuteCommandAsync(command, timeoutMs);
            }
            catch (Exception ex)
            {
                log.Error($"通道执行异常: {config.Name}", ex);
                return new DeviceCommandResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// 断开所有通道
        /// </summary>
        public async Task DisconnectAllAsync()
        {
            foreach (var kvp in _channels)
            {
                try
                {
                    await kvp.Value.DisconnectAsync();
                    log.Info($"通道已断开: {kvp.Key}");
                }
                catch (Exception ex)
                {
                    log.Warn($"断开通道异常: {kvp.Key}", ex);
                }
            }
            _channels.Clear();
        }

        /// <summary>
        /// 根据配置创建通道实例
        /// </summary>
        private static IDeviceChannel CreateChannel(DeviceChannelConfig config)
        {
            return config.ChannelType switch
            {
                DeviceChannelType.ThunderbirdSerial => new ThunderbirdSerialChannel(config),
                DeviceChannelType.GenericSerial => new GenericSerialChannel(config),
                DeviceChannelType.Socket => new SocketChannel(config),
                _ => throw new NotSupportedException($"不支持的通道类型: {config.ChannelType}")
            };
        }

        // ─── 持久化 ─────────────────────────────────────

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(PersistDirectory);
                string json = JsonConvert.SerializeObject(ChannelConfigs, Formatting.Indented);
                File.WriteAllText(PersistFilePath, json);
                log.Info($"通道配置已保存: {PersistFilePath}");
            }
            catch (Exception ex)
            {
                log.Error("保存通道配置失败", ex);
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(PersistFilePath))
                {
                    string json = File.ReadAllText(PersistFilePath);
                    var configs = JsonConvert.DeserializeObject<ObservableCollection<DeviceChannelConfig>>(json);
                    if (configs != null)
                    {
                        ChannelConfigs = configs;
                        OnPropertyChanged(nameof(ChannelConfigs));
                    }
                    log.Info($"通道配置已加载: {ChannelConfigs.Count} 个通道");
                }
            }
            catch (Exception ex)
            {
                log.Error("加载通道配置失败", ex);
            }
        }

        public void Dispose()
        {
            foreach (var ch in _channels.Values)
                ch.Dispose();
            _channels.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
