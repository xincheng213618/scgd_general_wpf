using ColorVision.Common.MVVM;
using log4net;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace ProjectKB
{
    public class KBItemMaster : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(KBItemMaster));

        public static void SaveCsv(KBItemMaster KBItems, string FileName)
        {
             var csvBuilder = new StringBuilder();
            List<string> properties = new()
    {
        "Id","Model", "SerialNumber", "POISet", "AvgLv", "MinLv", "MaxLv", "LvUniformity",
        "DarkestKey", "BrightestKey", "ColorDifference", "NbrFailedPts", "LvFailures",
        "LocalContrastFailures", "DarkKeyLocalContrast", "BrightKeyLocalContrast",
        "LocalDarkestKey", "LocalBrightestKey", "StrayLight", "Result", "DateTime"
    };

        //    "ESC", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        //"HOME", "END", "DELETE", "calculator", "(", ")", "MOON", "~", "1", "2", "3", "4",
        //"5", "6", "7", "8", "9", "0", "-", "=", "Backspace", "Num lock", "NUM /",
        //"NUM *", "NUM -", "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[",
        //"]", "\\", "Num 7", "Num 8", "Num 9", "Num +", "Capslk", "A", "S", "D", "F", "G",
        //"H", "J", "K", "L", ";", "R68", "Enter", "Num 4", "Num 5", "Num 6", "L-Shift",
        //"Z", "X", "C", "V", "B", "N", "M", "comma", ".", "/", "R-Shift", "Num 1", "Num 2",
        //"Num 3", "Num enter", "L-ctrl", "Fn", "Win", "L-alt", "Space", "R-alt", "R-ctrl",
        //"Pgup", "Up", "Pgdn", "Num 0", "Num .", "LEFT", "DN", "RIGHT",
            List<string> properyties1 = new List<string>()
            { "LimitProfile",
        "MinKeyLv", "MaxKeyLv", "MinAvgLv", "MaxAvgLv", "MinLvUniformity",
        "MaxDarkLocalContrast", "MaxBrightLocalContrast", "MaxNbrFailedPoints",
        "MaxColorDifference", "MaxStrayLight", "MinInterKeyUniformity",
        "MinInterKeyColorUniformity"
            };

            for (int i = 0; i < KBItems.Items.Count; i++)
            {
                string name = KBItems.Items[i].Name;
                if (name.Contains(',') || name.Contains('"'))
                {
                    name = $"\"{name.Replace("\"", "\"\"")}\"";
                }
                properties.Add(name);
            }
            properties.AddRange(properyties1);

            string newHeaders = string.Join(",", properties);

            bool appendData = false;

            if (File.Exists(FileName))
            {
                using var reader = new StreamReader(FileName);
                string existingHeaders = reader.ReadLine();
                if (existingHeaders == newHeaders)
                {
                    appendData = true;
                }
            }

            if (!appendData)
            {
                csvBuilder.AppendLine(newHeaders);
            }
            var item = KBItems;
            if (item.SN.Contains(',') || item.SN.Contains('"'))
            {
                item.SN = $"\"{item.SN.Replace("\"", "\"\"")}\"";
            }
            List<string> values = new()
                {
                    item.Id.ToString(),
                    item.Model,
                    item.SN,
                    "",
                    item.AvgLv.ToString("F2",CultureInfo.InvariantCulture),
                    item.MinLv.ToString("F2",CultureInfo.InvariantCulture),
                    item.MaxLv.ToString("F2",CultureInfo.InvariantCulture),
                    item.LvUniformity.ToString("F2",CultureInfo.InvariantCulture),
                    item.DrakestKey.ToString(CultureInfo.InvariantCulture),
                    item.BrightestKey.ToString(CultureInfo.InvariantCulture),
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    item.Result.ToString(),
                    item.DateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                };

            for (int i = 0; i < item.Items.Count; i++)
            {
                values.Add(item.Items[i].Lv.ToString("F2"));
            }
            values.Add("");
            values.Add(item.MaxLv.ToString("F2"));
            values.Add(item.MinLv.ToString("F2"));

            csvBuilder.AppendLine(string.Join(",", values));

            log.Debug(csvBuilder.ToString());
            if (appendData)
            {
                File.AppendAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
            }
            else
            {
                File.WriteAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
            }
        }

        public KBItemMaster()
        {
            DateTime = DateTime.Now;
        }


        public ObservableCollection<KBItem> Items { get; set; } = new ObservableCollection<KBItem>();

        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        public string ResultImagFile { get => _ResultImagFile; set { _ResultImagFile = value; NotifyPropertyChanged(); } }
        private string _ResultImagFile = string.Empty;

        public string Model { get => _Model; set { _Model = value; NotifyPropertyChanged(); } }
        private string _Model =string.Empty;

        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN;
        public string Exposure { get => _Exposure; set { _Exposure = value; NotifyPropertyChanged(); } }
        private string _Exposure;

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

        public DateTime DateTime { get => __DateTime; set { __DateTime = value; NotifyPropertyChanged(); } }
        private DateTime __DateTime = DateTime.Now;
    }
}