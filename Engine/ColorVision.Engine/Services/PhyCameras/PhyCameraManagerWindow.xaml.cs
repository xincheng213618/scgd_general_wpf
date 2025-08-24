using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{
    public class ExportPhyCamerManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => Properties.Resources.MenuPhyCameraManager;
        public override int Order => 2;

        public override void Execute()
        {
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterScreen }.ShowDialog();
        }
    }

    public class PhyCameraManagerWindowConfig: WindowConfig
    {
        public static PhyCameraManagerWindowConfig Instance => ConfigService.Instance.GetRequiredService<PhyCameraManagerWindowConfig>();

        public bool AllowCreate { get => _AllowCreate; set { _AllowCreate = value; OnPropertyChanged(); } }
        private bool _AllowCreate;
    }

    /// <summary>
    /// PhySpectrumManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PhyCameraManagerWindow : Window
    {
        public static PhyCameraManagerWindowConfig Config  => ConfigService.Instance.GetRequiredService<PhyCameraManagerWindowConfig>();
        public PhyCameraManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            Config.SetWindow(this);
            SizeChanged += (s, e) => Config.SetConfig(this);
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            PhyCameraManager.GetInstance().LoadPhyCamera();
            this.DataContext = PhyCameraManager.GetInstance();
            PhyCameraManager.GetInstance().RefreshEmptyCamera();
        }

        private void TreeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            StackPanelShow.Children.Clear();
            if (TreeView1.SelectedItem is PhyCamera phyCamera)
            {
                StackPanelShow.Children.Add(phyCamera.GetDeviceInfo());
            }
        }
    }
}
