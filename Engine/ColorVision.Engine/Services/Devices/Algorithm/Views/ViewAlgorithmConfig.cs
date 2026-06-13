using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.EditorTools.Filters;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ColorVision.Engine.Utilities;
using ColorVision.Engine.Properties;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    [LocalizedDisplayName(nameof(Resources.AlgorithmViewConfig))]
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

        [Browsable(false)]
        public DisplayShaderFilterState DisplayShaderFilter { get => _DisplayShaderFilter; set { _DisplayShaderFilter = value ?? new DisplayShaderFilterState(); OnPropertyChanged(); } }
        private DisplayShaderFilterState _DisplayShaderFilter = new DisplayShaderFilterState();

        [LocalizedDisplayName(nameof(Resources.ShowList)), Category("View")]
        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; OnPropertyChanged(); } }
        private bool _IsShowListView = true;
        [LocalizedDisplayName(nameof(Resources.ListHeight)), Category("View")]
        public double Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private double _Height = 200;

        [LocalizedDisplayName(nameof(Resources.ShowSidebar)), Category("Control")]
        public bool IsShowSideListView { get => _IsShowSideListView; set { _IsShowSideListView = value; OnPropertyChanged(); } }
        private bool _IsShowSideListView;

        [LocalizedDisplayName(nameof(Resources.DataColumSavePath)), PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string SaveSideDataDirPath { get => _SaveSideDataDirPath; set { _SaveSideDataDirPath = value; OnPropertyChanged(); } }
        private string _SaveSideDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        [LocalizedDisplayName(nameof(Resources.AutoSaveDataColum))]
        public bool AutoSaveSideData { get => _AutoSaveSideData; set { _AutoSaveSideData = value; OnPropertyChanged(); } }
        private bool _AutoSaveSideData;

        [LocalizedDisplayName(nameof(Resources.AutoSaveRenderedImage))]
        public bool AutoSaveRendering { get => _AutoSaveRendering; set { _AutoSaveRendering = value; OnPropertyChanged(); } }
        private bool _AutoSaveRendering;

        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; OnPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;

        [LocalizedDisplayName(nameof(Resources.HistoricalDataQuery))]
        public int HistoyDay { get => _HistoyDay; set { _HistoyDay = value; OnPropertyChanged(); } }
        private int _HistoyDay = 1;


        [LocalizedDisplayName(nameof(Resources.ShowSiteInfo))]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string ShowDateFilePath { get => _ShowDateFilePath; set { _ShowDateFilePath = value; OnPropertyChanged(); } }
        private string _ShowDateFilePath;

    }
}
