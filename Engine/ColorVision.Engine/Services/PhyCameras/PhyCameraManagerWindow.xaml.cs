using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ColorVision.Engine.Services.PhyCameras
{
    public class StatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string;
            if (string.IsNullOrEmpty(status))
                return "未知";

            // 统一转为小写比较，防止大小写问题
            switch (status.ToLower())
            {
                case "online":
                    return ColorVision.Engine.Properties.Resources.Online;
                case "offline": // 兼容你的拼写错误
                    return ColorVision.Engine.Properties.Resources.offline;
                default:
                    return status; // 如果是其他状态，直接显示原文本
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PhyCameraOnlineComparer : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is PhyCamera a && y is PhyCamera b)
            {
                bool aOnline = string.Equals(a.SysResourceModel?.Remark, "Online", StringComparison.OrdinalIgnoreCase);
                bool bOnline = string.Equals(b.SysResourceModel?.Remark, "Online", StringComparison.OrdinalIgnoreCase);

                if (aOnline && !bOnline) return -1;
                if (!aOnline && bOnline) return 1;
                return a.Id.CompareTo(b.Id);
            }
            return 0;
        }
    }

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
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            PhyCameraManager.GetInstance().LoadPhyCamera();
            this.DataContext = PhyCameraManager.GetInstance();
            PhyCameraManager.GetInstance().RefreshEmptyCamera();
            ApplyOnlineSorting();
            PhyCameraManager.GetInstance().PhyCameras.CollectionChanged += OnPhyCamerasCollectionChanged;
            this.Closed += (s, args) => PhyCameraManager.GetInstance().PhyCameras.CollectionChanged -= OnPhyCamerasCollectionChanged;
        }

        private void OnPhyCamerasCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ApplyOnlineSorting();
        }

        private void ApplyOnlineSorting()
        {
            var view = CollectionViewSource.GetDefaultView(PhyCameraManager.GetInstance().PhyCameras);
            if (view is ListCollectionView listView)
            {
                listView.CustomSort = new PhyCameraOnlineComparer();
            }
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
