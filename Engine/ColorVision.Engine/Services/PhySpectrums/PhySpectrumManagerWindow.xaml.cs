using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.PhySpectrums
{
    public sealed class ExportPhySpectrumManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => Properties.Resources.PhysicalSpectrumManager;
        public override int Order => 3;
        public override void Execute() => new PhySpectrumManagerWindow { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
    }

    public partial class PhySpectrumManagerWindow : Window
    {
        public PhySpectrumManagerWindow(string? serial = null, int comPort = 0)
        {
            InitializeComponent();
            this.ApplyCaption();
            var manager = new PhySpectrumManager(serial, comPort);
            DataContext = manager;
            Loaded += async (_, _) => await manager.RefreshAsync();
            Closed += (_, _) => manager.Dispose();
        }
    }
}
