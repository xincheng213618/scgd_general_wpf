using ColorVision.Engine.Abstractions;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    public class ConfigThirdPartyAlgorithms : DeviceServiceConfig
    {
        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();

        /// 许可
        /// </summary>
        public string BindCode { get => _BindCode; set { _BindCode = value; NotifyPropertyChanged(); } }
        private string _BindCode;
    }
}
