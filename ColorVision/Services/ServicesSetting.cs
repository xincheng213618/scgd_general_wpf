using ColorVision.Settings;
using ColorVision.UI;

namespace ColorVision.Services
{
    public class ServicesSetting:IConfig
    {
        public static ServicesSetting Instance => ConfigHandler.GetInstance().GetRequiredService<ServicesSetting>();

        public int ShowType { get; set; }
    }
}
