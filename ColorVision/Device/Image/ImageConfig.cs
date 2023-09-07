using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Device.Image
{
    public class ImageConfig : BaseDeviceConfig
    {
        public string Endpoint { get => _Endpoint; set { _Endpoint = value;NotifyPropertyChanged(); } }
        private string _Endpoint;



        public string ImgPath { get => _ImgPath; set { _ImgPath = value; NotifyPropertyChanged(); } }
        private string _ImgPath;

    }
}
