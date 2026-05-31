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
                                Name = "CommunicationProtocol",
                                Description = "CommunicationProtocolDescription",
                                Order =1,
                                Type = ConfigSettingType.Class,
                                Source = SocketConfig.Instance
                            },
            };
        }

    }
}
