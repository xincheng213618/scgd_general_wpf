using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
using System.Windows;

namespace ColorVision.Services.PhyCameras
{

    public class ExportPhyCamerManager : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "PhyCamerManager";

        public int Order => 9;

        public string? Header => ColorVision.Properties.Resource.MenuPhyCameraManager;

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new RelayCommand(A => Execute());

        private static void Execute()
        {
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
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
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = PhyCameraManager.GetInstance();
            ServicesHelper.SelectAndFocusFirstNode(TreeView1);
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
