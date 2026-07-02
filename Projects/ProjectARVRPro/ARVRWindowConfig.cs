#pragma warning disable CA1822,CS0168,CS0219,CS4014,CS8601
using ColorVision.UI;

namespace ProjectARVRPro
{
    public class ARVRWindowConfig : WindowConfig
    {
        public static ARVRWindowConfig Instance => ConfigService.Instance.GetRequiredService<ARVRWindowConfig>();
    }
}
