using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Collections.Generic;

namespace ColorVision.Engine.Services
{
    public class ServicesConfig : ViewModelBase, IConfig
    {
        public static ServicesConfig Instance => ConfigService.Instance.GetRequiredService<ServicesConfig>();

        public int ShowType { get; set; }

    }
}
