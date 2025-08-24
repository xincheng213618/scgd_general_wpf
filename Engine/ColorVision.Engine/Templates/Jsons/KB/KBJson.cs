using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class KBKeyRect : ViewModelBase
    {
        [JsonProperty("h")]
        public int Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private int _Height;
        [JsonProperty("w")]
        public int Width { get => _Width; set { _Width = value; OnPropertyChanged(); } }
        private int _Width;
        [JsonProperty("x")]
        public int X { get => _X; set { _X = value; OnPropertyChanged(); } }
        private int _X;
        [JsonProperty("y")]
        public int Y { get => _Y; set { _Y = value; OnPropertyChanged(); } }
        private int _Y;

        [JsonProperty("key")]
        public KBKey KBKey { get => _KBKey; set { _KBKey = value; OnPropertyChanged(); } }
        private KBKey _KBKey;
        [JsonProperty("doKey")]
        public bool DoKey { get => _DoKey; set { _DoKey = value; OnPropertyChanged(); } }
        private bool _DoKey = true;

        [JsonProperty("halo")]
        public KBHalo KBHalo { get => _KBHalo; set { _KBHalo = value; OnPropertyChanged(); } }
        private KBHalo _KBHalo;
        [JsonProperty("doHalo")]
        public bool DoHalo { get => _DoHalo; set { _DoHalo = value; OnPropertyChanged(); } }
        private bool _DoHalo = true;

        [JsonProperty("name")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = string.Empty;

    }


    public class KBKey : ViewModelBase
    {
        public double KeyScale { get => _KeyScale; set { _KeyScale = value; OnPropertyChanged(); } }
        private double _KeyScale = 1;


        public double Area { get => _Area; set { _Area = value; OnPropertyChanged(); } }
        private double _Area = 1;

        [JsonProperty("move")]
        public int Move { get => _Move; set { _Move = value; OnPropertyChanged(); } }
        private int _Move = 20;

        [JsonProperty("offset_X")]
        public int OffsetX { get => _OffsetX; set { _OffsetX = value; OnPropertyChanged(); } }
        private int _OffsetX;

        [JsonProperty("offset_Y")]
        public int OffsetY { get => _OffsetY; set { _OffsetY = value; OnPropertyChanged(); } }
        private int _OffsetY;

        [JsonProperty("thresholdV")]
        public int ThresholdV { get => _ThresholdV; set { _ThresholdV = value; OnPropertyChanged(); } }
        private int _ThresholdV = 5000;
    }

    public class KBHalo : ViewModelBase
    {
        public double HaloScale { get => _HaloScale; set { _HaloScale = value; OnPropertyChanged(); } }
        private double _HaloScale = 1;

        [JsonProperty("move")]
        public int Move { get => _Move; set { _Move = value; OnPropertyChanged(); } }
        private int _Move = 20;

        [JsonProperty("haloSize")]
        public int HaloSize { get => _HaloSize; set { _HaloSize = value; OnPropertyChanged(); } }
        private int _HaloSize = 15;

        [JsonProperty("offset_X")]
        public int OffsetX { get => _OffsetX; set { _OffsetX = value; OnPropertyChanged(); } }
        private int _OffsetX;

        [JsonProperty("offset_Y")]
        public int OffsetY { get => _OffsetY; set { _OffsetY = value; OnPropertyChanged(); } }
        private int _OffsetY;

        [JsonProperty("thresholdV")]
        public int ThresholdV { get => _ThresholdV; set { _ThresholdV = value; OnPropertyChanged(); } }
        private int _ThresholdV = 5000;
    }


    public class KBJson : ViewModelBase
    {
        [JsonProperty("keyRect")]
        public ObservableCollection<KBKeyRect> KBKeyRects { get => _KBKeyRects; set { _KBKeyRects = value; OnPropertyChanged(); } }
        private ObservableCollection<KBKeyRect> _KBKeyRects = new ObservableCollection<KBKeyRect>();

        [JsonProperty("debugPath")]
        public string DebugPath { get => _DebugPath; set { _DebugPath = value; OnPropertyChanged(); } }
        private string _DebugPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        [JsonProperty("saveProcessData")]
        public bool SaveProcessData { get => _SaveProcessData; set { _SaveProcessData = value; OnPropertyChanged(); } }
        private bool _SaveProcessData;

        public int Width { get => _Width; set { _Width = value; OnPropertyChanged(); } }
        private int _Width = 5544;
        public int Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private int _Height = 3692;

        public KBPoiConfig PoiConfig { get => _PoiConfig; set { _PoiConfig = value; OnPropertyChanged(); } }
        private KBPoiConfig _PoiConfig = new KBPoiConfig();

        public double KBLVSacle { get => _KBLVSacle; set { _KBLVSacle = value; OnPropertyChanged(); } }
        private double _KBLVSacle = AlgorithmKBConfig.Instance.KBLVSacle;


    }
}
