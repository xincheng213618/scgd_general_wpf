using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Windows;

namespace ColorVision.Engine.Services.PhySpectrums 
{
    public class ExportPhySpectrum : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "物理光谱仪";
        public override int Order => 0;
        public override void Execute()
        {
            new PhySpectrumManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterScreen }.ShowDialog();
        }
    }

    public class PhySpectrumManagerWindowConfig: WindowConfig { }
    /// <summary>
    /// PhySpectrumManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PhySpectrumManagerWindow : Window
    {
        public static PhySpectrumManagerWindowConfig Config  => ConfigService.Instance.GetRequiredService<PhySpectrumManagerWindowConfig>();
        public PhySpectrumManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            Config.SetWindow(this);
            SizeChanged += (s, e) => Config.SetConfig(this);
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            PhySpectrumManager.GetInstance().Load();
            this.DataContext = PhySpectrumManager.GetInstance();
            PhySpectrumManager.GetInstance().RefreshEmpty();
        }

        private void TreeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            StackPanelShow.Children.Clear();
            if (TreeView1.SelectedItem is PhySpectrum phySpectrum)
            {
                StackPanelShow.Children.Add(phySpectrum.GetDeviceInfo());
            }
        }
    }
}
