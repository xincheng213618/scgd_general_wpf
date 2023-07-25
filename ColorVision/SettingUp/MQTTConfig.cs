using ColorVision.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.SettingUp
{
    public class MQTTSetting : ViewModelBase
    {
        /// <summary>
        /// 是否显示心跳
        /// </summary>
        public bool IsShieldHeartbeat { get => _IsShieldHeartbeat; set { _IsShieldHeartbeat = value; NotifyPropertyChanged(); } }
        private bool _IsShieldHeartbeat;

        /// <summary>
        /// 只显示选中的
        /// </summary>
        public bool ShowSelect { get => _ShowSelect; set { _ShowSelect = value; NotifyPropertyChanged(); } }
        private bool _ShowSelect;

        public int AliveTimeout { get => _AliveTimeout; set { _AliveTimeout = value; NotifyPropertyChanged(); } }
        private int _AliveTimeout = 60;

        public int SendTimeout { get => _SendTimeout; set { _SendTimeout = value; NotifyPropertyChanged(); } }
        private int _SendTimeout = 10;
    }


    /// <summary>
    /// MQTT配置
    /// </summary>
    public class MQTTConfig : ViewModelBase
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;


        /// <summary>
        /// IP地址
        /// </summary>
        public string Host { get => _Host; set { _Host = value; NotifyPropertyChanged(); } }
        private string _Host = "127.0.0.1";

        /// <summary>
        /// 端口地址
        /// </summary>
        public int Port { get => _Port; set {
                _Port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                NotifyPropertyChanged(); 
            } }
        private int _Port = 1883;

        /// <summary>
        /// 账号
        /// </summary>
        public string UserName { get=>_UserName; set { _UserName = value; NotifyPropertyChanged(); } }
        private string _UserName = string.Empty;

        /// <summary>
        /// 密码
        /// </summary>
        public string UserPwd { get => _UserPwd; set { _UserPwd = value; NotifyPropertyChanged(); } }
        private string _UserPwd = string.Empty;

    }
}
