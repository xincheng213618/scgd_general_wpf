using ColorVision.Common.MVVM;
using System;
using System.ComponentModel;
using ColorVision.Engine.Utilities;
using ColorVision.Engine.Properties;
using Newtonsoft.Json;

namespace ColorVision.Engine.Cache
{
    public interface IFileServerCfg
    {
        public FileServerCfg FileServerCfg { get; set; }
    }

    public class FileServerCfg : ViewModelBase
    {
        /// <summary>
        /// 数据基础路径
        /// </summary>
        [LocalizedDisplayName(nameof(Resources.DataBasicPath)), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string DataBasePath { get => _DataBasePath; set { _DataBasePath = value; OnPropertyChanged(); } }
        private string _DataBasePath = "D:\\CVTest";

        /// <summary>
        /// 端口地址
        /// </summary>
        [Browsable(false), LocalizedDisplayName(nameof(Resources.PortAddress))]
        public string Endpoint { get => _Endpoint; set { _Endpoint = value; OnPropertyChanged(); } }
        private string _Endpoint = "127.0.0.1";
        /// <summary>
        /// 端口范围
        /// </summary>
        [Browsable(false), LocalizedDisplayName(nameof(Resources.PortRange))]
        public string PortRange { get => _PortRange; set { _PortRange = value; OnPropertyChanged(); } }
        private string _PortRange = ((Func<string>)(() => { int fromPort = Math.Abs(new Random().Next()) % 99 + 6600;  return string.Format("{0}-{1}", fromPort, fromPort + 5);  }))();

        /// <summary>
        /// 保留旧服务协议字段，固定下发 0 以禁用按天删除；文件清理由流程容量策略负责。
        /// </summary>
        [Browsable(false), LocalizedDisplayName(nameof(Resources.RetentionDays))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        // 旧服务将大于 30 的值压回 30；忽略旧配置中的正值，避免重新启用按天清理。
        public uint SaveDays { get => 0; set { } }
    }
}
