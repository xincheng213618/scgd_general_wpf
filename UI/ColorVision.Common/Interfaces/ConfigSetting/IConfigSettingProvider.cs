using System.Collections.Generic;
namespace ColorVision.UI
{
    public interface IConfigSettingProvider
    {
        IEnumerable<ConfigSettingMetadata> GetConfigSettings();
    }
}
