using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Configuration
{
    /// <summary>
    /// 编辑器配置基类实现
    /// </summary>
    public class EditorConfiguration : ViewModelBase, IEditorConfiguration
    {
        private readonly Dictionary<string, IConfigurationItem> _items = new Dictionary<string, IConfigurationItem>();
        private readonly Dictionary<string, object> _metadata = new Dictionary<string, object>();

        public Guid Id { get; } = Guid.NewGuid();

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                var oldValue = _name;
                if (SetProperty(ref _name, value))
                {
                    OnConfigurationChanged(nameof(Name), oldValue, value, ConfigurationChangeType.Modified);
                }
            }
        }

        public Version Version { get; protected set; } = new Version(1, 0);

        private DateTime _lastModified;
        public DateTime LastModified
        {
            get => _lastModified;
            private set => SetProperty(ref _lastModified, value);
        }

        public event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        public EditorConfiguration()
        {
            _name = $"Config_{Id:N}";
            _lastModified = DateTime.Now;
        }

        public EditorConfiguration(string name) : this()
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public T GetItem<T>(string key) where T : class, IConfigurationItem
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (_items.TryGetValue(key, out var item) && item is T typedItem)
            {
                return typedItem;
            }

            return null;
        }

        public void SetItem<T>(T item) where T : class, IConfigurationItem
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            var key = item.Key;
            var oldItem = _items.ContainsKey(key) ? _items[key] : null;
            var changeType = oldItem == null ? ConfigurationChangeType.Added : ConfigurationChangeType.Modified;

            _items[key] = item;
            LastModified = DateTime.Now;

            // 监听配置项属性变更
            if (item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged -= OnItemPropertyChanged;
                notifyItem.PropertyChanged += OnItemPropertyChanged;
            }

            OnConfigurationChanged(key, oldItem, item, changeType);
        }

        public bool RemoveItem(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (_items.TryGetValue(key, out var oldItem))
            {
                _items.Remove(key);
                LastModified = DateTime.Now;

                // 取消监听
                if (oldItem is INotifyPropertyChanged notifyItem)
                {
                    notifyItem.PropertyChanged -= OnItemPropertyChanged;
                }

                OnConfigurationChanged(key, oldItem, null, ConfigurationChangeType.Removed);
                return true;
            }

            return false;
        }

        public IEnumerable<IConfigurationItem> GetAllItems()
        {
            return _items.Values.ToList();
        }

        public void MarkAsModified()
        {
            LastModified = DateTime.Now;
        }

        public void ResetToDefaults()
        {
            var oldItems = _items.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            _items.Clear();
            LastModified = DateTime.Now;

            // 通知所有配置项被移除
            foreach (var kvp in oldItems)
            {
                OnConfigurationChanged(kvp.Key, kvp.Value, null, ConfigurationChangeType.Removed);
            }

            // 初始化默认配置
            InitializeDefaults();

            OnConfigurationChanged(string.Empty, null, null, ConfigurationChangeType.Reset);
        }

        public void ImportFrom(IEditorConfiguration other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            // 开始批量导入
            var itemsToImport = other.GetAllItems().ToList();

            foreach (var item in itemsToImport)
            {
                // 使用反射创建副本（如果可能）
                var copiedItem = CopyItem(item);
                if (copiedItem != null)
                {
                    SetItem(copiedItem);
                }
            }

            // 导入元数据
            if (other is EditorConfiguration otherConfig)
            {
                foreach (var meta in otherConfig._metadata)
                {
                    _metadata[meta.Key] = meta.Value;
                }
            }

            MarkAsModified();
        }

        /// <summary>
        /// 设置元数据
        /// </summary>
        public void SetMetadata(string key, object value)
        {
            _metadata[key] = value;
        }

        /// <summary>
        /// 获取元数据
        /// </summary>
        public T GetMetadata<T>(string key, T defaultValue = default)
        {
            if (_metadata.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 初始化默认配置 - 子类可重写
        /// </summary>
        protected virtual void InitializeDefaults()
        {
            // 子类重写以添加默认配置项
        }

        /// <summary>
        /// 复制配置项 - 子类可重写以优化性能
        /// </summary>
        protected virtual IConfigurationItem CopyItem(IConfigurationItem item)
        {
            // 默认实现：尝试使用 MemberwiseClone 或创建新实例
            // 子类应该重写此方法以提供正确的复制逻辑
            return item;
        }

        private void OnConfigurationChanged(string key, object oldValue, object newValue, ConfigurationChangeType changeType)
        {
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(key, oldValue, newValue, changeType));
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is IConfigurationItem item)
            {
                LastModified = DateTime.Now;
                OnConfigurationChanged($"{item.Key}.{e.PropertyName}", null, sender, ConfigurationChangeType.Modified);
            }
        }
    }

    /// <summary>
    /// 可持久化的编辑器配置基类
    /// </summary>
    public abstract class PersistableEditorConfiguration : EditorConfiguration, IPersistableConfiguration
    {
        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        private bool _autoSave;
        public bool AutoSave
        {
            get => _autoSave;
            set => SetProperty(ref _autoSave, value);
        }

        protected PersistableEditorConfiguration() { }

        protected PersistableEditorConfiguration(string name) : base(name) { }

        public abstract void Save();
        public abstract void Load();
        public abstract Task SaveAsync();
        public abstract Task LoadAsync();

        protected virtual void OnAutoSave()
        {
            if (AutoSave && !string.IsNullOrEmpty(FilePath))
            {
                Save();
            }
        }
    }

    /// <summary>
    /// 基础配置项实现
    /// </summary>
    public abstract class ConfigurationItemBase : ViewModelBase, IConfigurationItem
    {
        public abstract string Key { get; }

        private Version _version = new Version(1, 0);
        public Version Version
        {
            get => _version;
            protected set => SetProperty(ref _version, value);
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
    }
}
