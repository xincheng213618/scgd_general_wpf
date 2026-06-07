using ColorVision.Common.MVVM;
using System;
using System.ComponentModel;
using ColorVision.Engine.Utilities;
using ColorVision.Engine.Properties;

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
        [LocalizedDisplayName(nameof(Resources.PortAddress))]
        public string Endpoint { get => _Endpoint; set { _Endpoint = value; OnPropertyChanged(); } }
        private string _Endpoint = "127.0.0.1";
        /// <summary>
        /// 端口范围
        /// </summary>
        [LocalizedDisplayName(nameof(Resources.PortRange))]
        public string PortRange { get => _PortRange; set { _PortRange = value; OnPropertyChanged(); } }
        private string _PortRange = ((Func<string>)(() => { int fromPort = Math.Abs(new Random().Next()) % 99 + 6600;  return string.Format("{0}-{1}", fromPort, fromPort + 5);  }))();

        /// <summary>
        /// 保存天数
        /// </summary>
        [LocalizedDisplayName(nameof(Resources.RetentionDays))]
        public uint SaveDays { get => _SaveDays; set { _SaveDays = value; OnPropertyChanged(); } }
        private uint _SaveDays = 365;
    }
}