#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    [DisplayName("算法视图配置")]
    public class ViewAlgorithmConfig : ViewConfigBase, IConfig
    {
        public static ViewAlgorithmConfig Instance => ConfigService.Instance.GetRequiredService<ViewAlgorithmConfig>();

        [JsonIgnore]
        public ObservableCollection<AlgorithmResult> ViewResults { get; set; } = new ObservableCollection<AlgorithmResult>();

        [JsonIgnore]
        public RelayCommand ClearListCommand { get; set; }
        public ViewAlgorithmConfig()
        {
            ClearListCommand = new RelayCommand(a => ViewResults.Clear());
        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        [DisplayName("显示列表"), Category("View")]
        public bool IsShowListView { get => _IsShowListView; set { _IsShowListView = value; OnPropertyChanged(); } }
        private bool _IsShowListView = true;
        [DisplayName("列表高度"), Category("View")]
        public double Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private double _Height = 200;

        [DisplayName("显示侧边栏"), Category("Control")]
        public bool IsShowSideListView { get => _IsShowSideListView; set { _IsShowSideListView = value; OnPropertyChanged(); } }
        private bool _IsShowSideListView;

        [DisplayName("数据列保存路径"),PropertyEditorType(PropertyEditorType.TextSelectFolder)]
        public string SaveSideDataDirPath { get => _SaveSideDataDirPath; set { _SaveSideDataDirPath = value; OnPropertyChanged(); } }
        private string _SaveSideDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        [DisplayName("自动保存数据列")]
        public bool AutoSaveSideData { get => _AutoSaveSideData; set { _AutoSaveSideData = value; OnPropertyChanged(); } }
        private bool _AutoSaveSideData;

        [DisplayName("自动保存渲染图")]
        public bool AutoSaveRendering { get => _AutoSaveRendering; set { _AutoSaveRendering = value; OnPropertyChanged(); } }
        private bool _AutoSaveRendering;

        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; OnPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;

        [DisplayName("历史数据查询")]
        public int HistoyDay { get => _HistoyDay; set { _HistoyDay = value; OnPropertyChanged(); } }
        private int _HistoyDay = 1;


        [DisplayName("显示位点信息")]
        [PropertyEditorType(PropertyEditorType.TextSelectFile)]
        public string ShowDateFilePath { get => _ShowDateFilePath; set { _ShowDateFilePath = value; OnPropertyChanged(); } }
        private string _ShowDateFilePath;

    }
}
