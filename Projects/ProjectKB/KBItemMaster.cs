using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Flow;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ProjectKB
{
    [SugarTable("KBItemMaster")]
    public class KBItemMaster : ViewModelBase
    {
        [SugarColumn(IsIgnore = true)]
        public ContextMenu ContextMenu { get; set; }

        public KBItemMaster()
        {
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Command = System.Windows.Input.ApplicationCommands.Delete });

        }


        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        public int BatchId { get => _BatchId; set { _BatchId = value; NotifyPropertyChanged(); } }
        private int _BatchId;

        public FlowStatus FlowStatus { get; set; } = FlowStatus.Ready;

        [SugarColumn(IsIgnore = true)]
        public ObservableCollection<KBItem> Items { get; set; } = new ObservableCollection<KBItem>();

        public string ItemsJson
        {
            get => JsonConvert.SerializeObject(Items);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    Items = JsonConvert.DeserializeObject<ObservableCollection<KBItem>>(value);
            }
        }



        public string ResultImagFile { get => _ResultImagFile; set { _ResultImagFile = value; NotifyPropertyChanged(); } }
        private string _ResultImagFile = string.Empty;

        public string Model { get => _Model; set { _Model = value; NotifyPropertyChanged(); } }
        private string _Model =string.Empty;

        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN = string.Empty;

        public string Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string _Code = string.Empty;

        public string Exposure { get => _Exposure; set { _Exposure = value; NotifyPropertyChanged(); } }
        private string _Exposure = string.Empty;

        public double AvgLv { get => _AvgLv; set { _AvgLv = value; NotifyPropertyChanged(); } }
        private double _AvgLv;

        public double AvgC1 { get => _AvgC1; set { _AvgC1 = value; NotifyPropertyChanged(); } }
        private double _AvgC1;

        public double AvgC2 { get => _AvgC2; set { _AvgC2 = value; NotifyPropertyChanged(); } }
        private double _AvgC2;

        public double MinLv { get => _MinLv; set { _MinLv = value; NotifyPropertyChanged(); } }
        private double _MinLv;

        public double MaxLv { get => _MaxLv; set { _MaxLv = value; NotifyPropertyChanged(); } }
        private double _MaxLv;

        public string DrakestKey { get => _DrakestKey; set { _DrakestKey = value; NotifyPropertyChanged(); } }
        private string _DrakestKey = string.Empty;

        public string BrightestKey { get => _BrightestKey; set { _BrightestKey = value; NotifyPropertyChanged(); } }
        private string _BrightestKey = string.Empty;

        public int NbrFailPoints { get => _NbrFailPoints; set { _NbrFailPoints = value; NotifyPropertyChanged(); } }
        private int _NbrFailPoints;

        public double LvUniformity { get => _LvUniformity; set { _LvUniformity = value; NotifyPropertyChanged(); } }
        private double _LvUniformity;

        public double ColorUniformity { get => _ColorUniformity; set { _ColorUniformity = value; NotifyPropertyChanged(); } }
        private double _ColorUniformity;

        public bool Result { get => _Result; set { _Result = value; NotifyPropertyChanged(); } }
        private bool _Result;

        public DateTime CreateTime { get => _CreateTime; set { _CreateTime = value; NotifyPropertyChanged(); } }
        private DateTime _CreateTime = DateTime.Now;
    }
}