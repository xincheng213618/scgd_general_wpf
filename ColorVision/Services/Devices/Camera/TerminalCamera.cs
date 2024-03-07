using ColorVision.Services.Dao;
using ColorVision.Services.Terminal;
using System.Windows;

namespace ColorVision.Services.Devices.Camera
{
    public class TerminalCamera : TerminalService
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
