using ColorVision.UI;
using ColorVision.UI.Menus;

namespace CaptchaOCR
{
    public class CaptchaOCRPlugin : MenuItemBase
    {
        public override string OwnerGuid =>  MenuItemConstants.Tool;
        public override string Header { get;  } = "验证码识别";
        public override void Execute()
        {
            var window = new CaptchaWindow();
            window.Show();
        }
    }
}
