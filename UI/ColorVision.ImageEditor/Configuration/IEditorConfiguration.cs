using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Configuration
{
    /// <summary>
    /// 编辑器配置项接口 - 所有配置项的基础接口
    /// </summary>
    public interface IConfigurationItem
    {
        /// <summary>
        /// 配置项唯一标识
        /// </summary>
        string Key { get; }

        /// <summary>
        /// 配置项版本，用于兼容性处理
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// 配置项描述
        /// </summary>
        string Description { get; }
    }

    /// <summary>
    /// 编辑器配置接口 - 统一的配置管理接口
    /// </summary>
    public interface IEditorConfiguration : INotifyPropertyChanged
    {
        /// <summary>
        /// 配置唯一标识
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// 配置名称
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// 配置版本
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        DateTime LastModified { get; }

        /// <summary>
        /// 获取配置项
        /// </summary>
        T GetItem<T>(string key) where T : class, IConfigurationItem;

        /// <summary>
        /// 设置配置项
        /// </summary>
        void SetItem<T>(T item) where T : class, IConfigurationItem;

        /// <summary>
        /// 移除配置项
        /// </summary>
        bool RemoveItem(string key);

        /// <summary>
        /// 获取所有配置项
        /// </summary>
        IEnumerable<IConfigurationItem> GetAllItems();

        /// <summary>
        /// 配置变更事件
        /// </summary>
        event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        /// <summary>
        /// 标记配置已修改
        /// </summary>
        void MarkAsModified();

        /// <summary>
        /// 重置为默认值
        /// </summary>
        void ResetToDefaults();

        /// <summary>
        /// 从其他配置导入
        /// </summary>
        void ImportFrom(IEditorConfiguration other);
    }

    /// <summary>
    /// 配置变更事件参数
    /// </summary>
    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string Key { get; }
        public object OldValue { get; }
        public object NewValue { get; }
        public ConfigurationChangeType ChangeType { get; }

        public ConfigurationChangedEventArgs(string key, object oldValue, object newValue, ConfigurationChangeType changeType)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
            ChangeType = changeType;
        }
    }

    /// <summary>
    /// 配置变更类型
    /// </summary>
    public enum ConfigurationChangeType
    {
        Added,
        Modified,
        Removed,
        Reset
    }

    /// <summary>
    /// 可持久化的配置接口
    /// </summary>
    public interface IPersistableConfiguration : IEditorConfiguration
    {
        /// <summary>
        /// 配置文件路径
        /// </summary>
        string FilePath { get; set; }

        /// <summary>
        /// 是否自动保存
        /// </summary>
        bool AutoSave { get; set; }

        /// <summary>
        /// 保存配置
        /// </summary>
        void Save();

        /// <summary>
        /// 加载配置
        /// </summary>
        void Load();

        /// <summary>
        /// 异步保存
        /// </summary>
        System.Threading.Tasks.Task SaveAsync();

        /// <summary>
        /// 异步加载
        /// </summary>
        System.Threading.Tasks.Task LoadAsync();
    }
}
