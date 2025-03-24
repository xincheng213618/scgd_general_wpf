using ColorVision.UI.Menus;
using System.Management;
using System.Text;
using System.Windows;

namespace ColorVision.Engine.ToolPlugins
{
    public class USBtool : MenuItemBase
    {

        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => nameof(USBtool);
        public override int Order => 100;
        public override void Execute()
        {
            StringBuilder usbInfo = new StringBuilder();
            var searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_USBController");

            foreach (ManagementObject usbController in searcher.Get())
            {
                usbInfo.AppendLine($"Name: {usbController["Name"]}");
                usbInfo.AppendLine($"PNPDeviceID: {usbController["PNPDeviceID"]}");
                usbInfo.AppendLine($"Status: {usbController["Status"]}");
                usbInfo.AppendLine();
            }

            MessageBox.Show(usbInfo.ToString(), "USB Controller Information");
        }
    }
}

