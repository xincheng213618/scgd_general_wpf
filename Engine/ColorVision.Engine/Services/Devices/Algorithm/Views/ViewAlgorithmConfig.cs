using ColorVision.UI;
using ColorVision.UI.Sorts;
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

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        [LocalizedDisplayName(nameof(Resources.ShowList)), Category("View")]
        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; OnPropertyChanged(); } }
        private bool _IsShowListView = true;

        [LocalizedDisplayName(nameof(Resources.ListHeight)), Category("View")]
        public double Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private double _Height = 200;

        [LocalizedDisplayName(nameof(Resources.ShowSidebar)), Category("Control")]
        public bool IsShowSideListView { get => _IsShowSideListView; set { _IsShowSideListView = value; OnPropertyChanged(); } }
        private bool _IsShowSideListView;

        [LocalizedDisplayName(nameof(Resources.DataColumSavePath)), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string SaveSideDataDirPath { get => _SaveSideDataDirPath; set { _SaveSideDataDirPath = value; OnPropertyChanged(); } }
        private string _SaveSideDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        [LocalizedDisplayName(nameof(Resources.AutoSaveDataColum))]
        public bool AutoSaveSideData { get => _AutoSaveSideData; set { _AutoSaveSideData = value; OnPropertyChanged(); } }
        private bool _AutoSaveSideData;

        [LocalizedDisplayName(nameof(Resources.ShowSiteInfo))]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string ShowDateFilePath { get => _ShowDateFilePath; set { _ShowDateFilePath = value; OnPropertyChanged(); } }
        private string _ShowDateFilePath;

    }
}
