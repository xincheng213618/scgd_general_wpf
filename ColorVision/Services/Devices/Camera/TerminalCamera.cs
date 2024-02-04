using ColorVision.Services.Dao;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Services.Devices.Camera
{
    public class TerminalCamera:TerminalService
    {
        public TerminalCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
        }

        public override void Create()
        {
            MessageBox.Show("CameraCreate");
        }
    }
}
