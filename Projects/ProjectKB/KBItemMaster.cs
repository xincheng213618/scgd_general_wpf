using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Templates.Flow;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Controls;

namespace ProjectKB
{
    [@SugarTable("KBItemMaster")]
    public class KBItemMaster : ViewModelBase,IPKModel
    {
        [SugarColumn(IsIgnore = true)]
        public ContextMenu ContextMenu { get; set; }

        public KBItemMaster()
        {
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Command = System.Windows.Input.ApplicationCommands.Delete });
            ContextMenu.Items.Add(new MenuItem() { Command = System.Windows.Input.ApplicationCommands.Copy, Header = "复制" });

            RelayCommand openFolderAndSelectFile = new RelayCommand(a =>
            {
                PlatformHelper.OpenFolderAndSelectFile(ResultImagFile);
            }, e => File.Exists(ResultImagFile));

            ContextMenu.Items.Add(new MenuItem() { Command = openFolderAndSelectFile, Header = "OpenFolderAndSelectFile" });
        }


        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get => _Id; set { _Id = value; OnPropertyChanged(); } }
        private int _Id;

        public int BatchId { get => _BatchId; set { _BatchId = value; OnPropertyChanged(); } }
        private int _BatchId;

        public FlowStatus FlowStatus { get; set; } = FlowStatus.Ready;

        [SugarColumn(IsIgnore = true)]
        public ObservableCollection<KBItem> Items { get; set; } = new ObservableCollection<KBItem>();

        [Browsable(false)]
        public string ItemsJson
        {
            get => JsonConvert.SerializeObject(Items);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    Items = JsonConvert.DeserializeObject<ObservableCollection<KBItem>>(value);
            }
        }



        public string ResultImagFile { get => _ResultImagFile; set { _ResultImagFile = value; OnPropertyChanged(); } }
        private string _ResultImagFile = string.Empty;

        public string Model { get => _Model; set { _Model = value; OnPropertyChanged(); } }
        private string _Model =string.Empty;

        public string SN { get => _SN; set { _SN = value; OnPropertyChanged(); } }
        private string _SN = string.Empty;

        public string Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string _Code = string.Empty;

        public string Exposure { get => _Exposure; set { _Exposure = value; OnPropertyChanged(); } }
        private string _Exposure = string.Empty;

        public double AvgLv { get => _AvgLv; set { _AvgLv = value; OnPropertyChanged(); } }
        private double _AvgLv;

        public double AvgC1 { get => _AvgC1; set { _AvgC1 = value; OnPropertyChanged(); } }
        private double _AvgC1;

        public double AvgC2 { get => _AvgC2; set { _AvgC2 = value; OnPropertyChanged(); } }
        private double _AvgC2;

        public double MinLv { get => _MinLv; set { _MinLv = value; OnPropertyChanged(); } }
        private double _MinLv;

        public double MaxLv { get => _MaxLv; set { _MaxLv = value; OnPropertyChanged(); } }
        private double _MaxLv;

        public string DrakestKey { get => _DrakestKey; set { _DrakestKey = value; OnPropertyChanged(); } }
        private string _DrakestKey = string.Empty;

        public string BrightestKey { get => _BrightestKey; set { _BrightestKey = value; OnPropertyChanged(); } }
        private string _BrightestKey = string.Empty;

        public int NbrFailPoints { get => _NbrFailPoints; set { _NbrFailPoints = value; OnPropertyChanged(); } }
        private int _NbrFailPoints;

        public double LvUniformity { get => _LvUniformity; set { _LvUniformity = value; OnPropertyChanged(); } }
        private double _LvUniformity;

        public double ColorUniformity { get => _ColorUniformity; set { _ColorUniformity = value; OnPropertyChanged(); } }
        private double _ColorUniformity;

        public bool Result { get => _Result; set { _Result = value; OnPropertyChanged(); } }
        private bool _Result;

        public long RunTime { get; set; }
        public string Msg { get; set; } = string.Empty;

        public DateTime CreateTime { get => _CreateTime; set { _CreateTime = value; OnPropertyChanged(); } }
        private DateTime _CreateTime = DateTime.Now;
    }
}