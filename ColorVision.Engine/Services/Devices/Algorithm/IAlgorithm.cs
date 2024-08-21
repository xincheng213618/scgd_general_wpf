using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public interface IAlgorithm
    {
        public UserControl GetUserControl();
    }
}
