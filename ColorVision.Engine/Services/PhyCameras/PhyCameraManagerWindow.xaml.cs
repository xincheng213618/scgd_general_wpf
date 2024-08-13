using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ColorVision.Engine.Services.PhyCameras
{

    public class PhyCamerManagerWizardStep : IWizardStep
    {
        public int Order => 9;

        public string Title => Properties.Resources.AddPhysicalCamera;

        public string Description => "对设备的物理相机进行配置";

        public RelayCommand? RelayCommand => new RelayCommand(a =>
        {
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterScreen }.ShowDialog();
        });
    }

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
            ServicesHelper.SelectAndFocusFirstNode(TreeView1);
            PhyCameraManager.GetInstance().Loaded +=(s,e) => ServicesHelper.SelectAndFocusFirstNode(TreeView1);
            PhyCameraManager.GetInstance().PhyCameras.CollectionChanged += (s,e)=> ServicesHelper.SelectAndFocusFirstNode(TreeView1);
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
