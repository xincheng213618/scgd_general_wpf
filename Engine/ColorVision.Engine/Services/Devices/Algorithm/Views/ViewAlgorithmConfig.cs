using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    [DisplayName("AlgorithmViewConfig")]
    public class ViewAlgorithmConfig : ViewConfigBase, IConfig
    {
        public static ViewAlgorithmConfig Instance => ConfigService.Instance.GetRequiredService<ViewAlgorithmConfig>();

        [JsonIgnore]
        public ObservableCollection<ViewResultAlg> ViewResults { get; set; } = new ObservableCollection<ViewResultAlg>();

        [JsonIgnore]
        public RelayCommand ClearListCommand { get; set; }
        public ViewAlgorithmConfig()
        {
            ClearListCommand = new RelayCommand(a => ViewResults.Clear());
        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        [DisplayName("ShowList"), Category("View")]
        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; OnPropertyChanged(); } }
        private bool _IsShowListView = true;
        [DisplayName("ListHeight"), Category("View")]
        public double Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private double _Height = 200;

        [DisplayName("ShowSidebar"), Category("Control")]
        public bool IsShowSideListView { get => _IsShowSideListView; set { _IsShowSideListView = value; OnPropertyChanged(); } }
        private bool _IsShowSideListView;

        [DisplayName("DataColumSavePath"),PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string SaveSideDataDirPath { get => _SaveSideDataDirPath; set { _SaveSideDataDirPath = value; OnPropertyChanged(); } }
        private string _SaveSideDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        [DisplayName("AutoSaveDataColum")]
        public bool AutoSaveSideData { get => _AutoSaveSideData; set { _AutoSaveSideData = value; OnPropertyChanged(); } }
        private bool _AutoSaveSideData;

        [DisplayName("AutoSaveRenderedImage")]
        public bool AutoSaveRendering { get => _AutoSaveRendering; set { _AutoSaveRendering = value; OnPropertyChanged(); } }
        private bool _AutoSaveRendering;

        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; OnPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;

        [DisplayName("HistoricalDataQuery")]
        public int HistoyDay { get => _HistoyDay; set { _HistoyDay = value; OnPropertyChanged(); } }
        private int _HistoyDay = 1;


        [DisplayName("ShowSiteInfo")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string ShowDateFilePath { get => _ShowDateFilePath; set { _ShowDateFilePath = value; OnPropertyChanged(); } }
        private string _ShowDateFilePath;

    }
}
