using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Views
{
    public class ViewThirdPartyAlgorithmsConfig : ViewConfigBase,IConfig
    {
        public static  ViewThirdPartyAlgorithmsConfig Instance => ConfigService.Instance.GetRequiredService<ViewThirdPartyAlgorithmsConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        public ImageViewConfig ImageViewConfig { get; set; } = new ImageViewConfig();

        [DisplayName("ShowDataColumns"), Category("Control")]
        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; OnPropertyChanged(); } }
        private bool _IsShowListView = true;

        [DisplayName("ShowSidebar"), Category("Control")]
        public bool IsShowSideListView { get => _IsShowSideListView; set { _IsShowSideListView = value; OnPropertyChanged(); } }
        private bool _IsShowSideListView = true;

        [DisplayName("DataColumSavePath"), PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string SaveSideDataDirPath { get => _SaveSideDataDirPath; set { _SaveSideDataDirPath = value; OnPropertyChanged(); } }
        private string _SaveSideDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        [DisplayName("AutoSaveDataColum")]
        public bool AutoSaveSideData { get => _AutoSaveSideData; set { _AutoSaveSideData = value; OnPropertyChanged(); } }
        private bool _AutoSaveSideData;

    }
}
