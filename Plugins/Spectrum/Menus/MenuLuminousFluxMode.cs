using ColorVision.UI.Menus;
using SpectrumResources = Spectrum.Properties.Resources;

namespace Spectrum.Menus
{
    /// <summary>
    /// Checkable menu item to toggle between 亮色度模式 (brightness/chromaticity) and 光通量模式 (luminous flux).
    /// Placed under the Tool menu so the mode switch is accessible but not prominently displayed.
    /// </summary>
    public class MenuLuminousFluxMode : SpectrumMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => SpectrumResources.LuminousFluxMode;
        public override int Order => 10;

        public override void Execute()
        {
            MainWindowConfig.Instance.EqeEnabled = !MainWindowConfig.Instance.EqeEnabled;
            MainWindow.Instance?.UpdateEqeColumnsVisibility(MainWindowConfig.Instance.EqeEnabled);
            MenuManager.GetInstance().RefreshMenuItemsByGuid(OwnerGuid);
        }

        public override bool? IsChecked => MainWindowConfig.Instance.EqeEnabled ? true : null;
    }
}
