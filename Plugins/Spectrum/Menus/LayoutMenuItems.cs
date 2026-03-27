using ColorVision.UI.Menus;

namespace Spectrum.Menus
{
    public class MenuSaveLayout : SpectrumMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "保存窗口布局";
        public override int Order => 100;

        public override void Execute()
        {
            MainWindow.Instance?.SaveLayout();
        }
    }

    public class MenuApplyLayout : SpectrumMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "应用窗口布局";
        public override int Order => 101;

        public override void Execute()
        {
            MainWindow.Instance?.LoadLayout();
        }
    }

    public class MenuResetLayout : SpectrumMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "重置窗口布局";
        public override int Order => 102;

        public override void Execute()
        {
            MainWindow.Instance?.ResetLayout();
        }
    }

    public class MenuToggleLog : SpectrumMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "切换日志面板";
        public override int Order => 110;

        public override void Execute()
        {
            MainWindow.Instance?.ToggleLogPanel();
        }
    }

    public class MenuToggleCie : SpectrumMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "切换CIE色度图";
        public override int Order => 111;

        public override void Execute()
        {
            MainWindow.Instance?.ToggleCiePanel();
        }
    }
}
