using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Engine.Services.PhySpectrums
{
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
