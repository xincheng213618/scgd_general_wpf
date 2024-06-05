using ColorVision.Common.MVVM;
using ColorVision.UI;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    public class LocalVideoConfig : ViewModelBase,IConfig
    {
        public static LocalVideoConfig Instance => ConfigHandler.GetInstance().GetRequiredService<LocalVideoConfig>();

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
        public int Port
        {
            get => _Port; set
            {
                _Port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                NotifyPropertyChanged();
            }
        }
        private int _Port;
    }
}
