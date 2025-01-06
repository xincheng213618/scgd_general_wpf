using ColorVision.Themes;
using ColorVision.UI.Menus;
using System;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{
    public class ExportPhyCamerManager : MenuItemBase
    {
        public override string OwnerGuid => "Tool";
        public override string GuidId => "PhyCamerManager";
        public override string Header => Properties.Resources.MenuPhyCameraManager;
        public override int Order => 0;

        public override void Execute()
        {
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterScreen }.ShowDialog();
        }
    }

    /// <summary>
    /// PhyCameraManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PhyCameraManagerWindow : Window
    {
        public PhyCameraManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
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
