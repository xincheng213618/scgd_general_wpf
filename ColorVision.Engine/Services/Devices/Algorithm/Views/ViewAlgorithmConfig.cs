#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    public class ViewAlgorithmConfig : ViewModelBase, IConfig
    {
        public static ViewAlgorithmConfig Instance => ConfigHandler.GetInstance().GetRequiredService<ViewAlgorithmConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        public ImageViewConfig ImageViewConfig { get; set; } = new ImageViewConfig();

        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; NotifyPropertyChanged(); } }
        private bool _IsShowListView = true;
        public bool IsShowSideListView { get => _IsShowSideListView; set { _IsShowSideListView = value; NotifyPropertyChanged(); } }
        private bool _IsShowSideListView = true;


        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView;

    }
}
