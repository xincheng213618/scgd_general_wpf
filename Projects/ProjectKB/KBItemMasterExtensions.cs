using System.Globalization;
using System.IO;
using System.Text;

namespace ProjectKB
{
    public static class KBItemMasterExtensions
    {
        public static void SaveCsv(this KBItemMaster KBItems, string FileName)
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
                    item.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                };

            for (int i = 0; i < item.Items.Count; i++)
            {
                values.Add(item.Items[i].Lv.ToString("F2"));
            }
            values.Add("");
            values.Add(item.MaxLv.ToString("F2"));
            values.Add(item.MinLv.ToString("F2"));

            csvBuilder.AppendLine(string.Join(",", values));
            if (appendData)
            {
                File.AppendAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
            }
            else
            {
                File.WriteAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
            }
        }
    }
}