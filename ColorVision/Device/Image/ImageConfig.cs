using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Device.Image
{
    public class ImageConfig : BaseDeviceConfig
    {
        public string Endpoint { get; set; }
        public string ImgPath { get; set; }
    }
}
