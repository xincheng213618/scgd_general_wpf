using ColorVision.UI;

namespace ColorVision.SocketProtocol
{
    public class SocketConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = ColorVision.SocketProtocol.Properties.Resources.CommunicationProtocol,
                                Order =1,
                                Type = ConfigSettingType.Class,
                                Source = SocketConfig.Instance
                            },
            };
        }

    }
}
