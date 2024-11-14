#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Views
{
    public class ViewThirdPartyAlgorithmsConfig : ViewModelBase, IConfig
    {
        public static ViewThirdPartyAlgorithmsConfig Instance => ConfigService.Instance.GetRequiredService<ViewThirdPartyAlgorithmsConfig>();

        public RelayCommand EditCommand { get; set; }

        public ViewThirdPartyAlgorithmsConfig()
        {
            EditCommand = new RelayCommand(a => new EditViewThirdPartyAlgorithmsConfig(this) { Owner =Application.Current.GetActiveWindow() ,WindowStartupLocation =WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        public ImageViewConfig ImageViewConfig { get; set; } = new ImageViewConfig();

        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; NotifyPropertyChanged(); } }
        private bool _IsShowListView = true;
        public bool IsShowSideListView { get => _IsShowSideListView; set { _IsShowSideListView = value; NotifyPropertyChanged(); } }
        private bool _IsShowSideListView = true;

        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView = true;

        public bool InsertAtBeginning { get => _InsertAtBeginning; set { _InsertAtBeginning = value; NotifyPropertyChanged(); } }
        private bool _InsertAtBeginning = true;

        public string SaveSideDataDirPath { get => _SaveSideDataDirPath; set { _SaveSideDataDirPath = value; NotifyPropertyChanged(); } }
        private string _SaveSideDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public bool AutoSaveSideData { get => _AutoSaveSideData; set { _AutoSaveSideData = value; NotifyPropertyChanged(); } }
        private bool _AutoSaveSideData;

    }
}
