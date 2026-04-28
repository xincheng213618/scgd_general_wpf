using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;

namespace ProjectKB
{
    public static class KBItemMasterExtensions
    {
        public static void SaveCsv(this KBItemMaster KBItems, string FileName)
        {
            // LvFailures 是单键亮度上下限失败的按键数量
            // LocalContrastFailures 是局部对比度上下限失败的按键数量
            // DarkKeyLocalContrast 是最暗Key的Lc
            // BrightKeyLocalContrast 是最亮Key的Lc

            //LocalDarkestKey  判定的是Lc 是最小Key
            //LocalBrightestKey  判定的是Lc 是最大Key

            //ColorDifference 是彩色才有，目前还是空

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
            RecipeManager recipeManager = RecipeManager.GetInstance();
            KBRecipeConfig recipe = recipeManager.RecipeConfigs.TryGetValue(item.Model, out KBRecipeConfig? matchedRecipe)
                ? matchedRecipe
                : recipeManager.RecipeConfig;
            var darkestByLv = item.Items.OrderBy(x => x.Lv).FirstOrDefault();
            var brightestByLv = item.Items.OrderByDescending(x => x.Lv).FirstOrDefault();
            var darkestByLc = item.Items.OrderBy(x => x.Lc).FirstOrDefault();
            var brightestByLc = item.Items.OrderByDescending(x => x.Lc).FirstOrDefault();

            int lvFailures = item.Items.Count(key => key.Lv < recipe.MinKeyLv || key.Lv > recipe.MaxKeyLv);
            int localContrastFailures = item.Items.Count(key => key.Lc < recipe.MinKeyLc / 100 || key.Lc > recipe.MaxKeyLc / 100);

            if (item.SN.Contains(',') || item.SN.Contains('"'))
            {
                item.SN = $"\"{item.SN.Replace("\"", "\"\"")}\"";
            }
            if (item.KBTemplate.Contains(',') || item.KBTemplate.Contains('"'))
            {
                item.KBTemplate = $"\"{item.KBTemplate.Replace("\"", "\"\"")}\"";
            }
            List<string> values = new()
                {
                    item.Id.ToString(),
                    item.Model,
                    item.SN,
                    item.KBTemplate,
                    item.AvgLv.ToString("F2",CultureInfo.InvariantCulture),
                    item.MinLv.ToString("F2",CultureInfo.InvariantCulture),
                    item.MaxLv.ToString("F2",CultureInfo.InvariantCulture),
                    item.LvUniformity.ToString("F2",CultureInfo.InvariantCulture),
                    item.DrakestKey.ToString(CultureInfo.InvariantCulture),
                    item.BrightestKey.ToString(CultureInfo.InvariantCulture),
                    "",
                    item.NbrFailPoints.ToString(CultureInfo.InvariantCulture),
                    lvFailures.ToString(CultureInfo.InvariantCulture),
                    localContrastFailures.ToString(CultureInfo.InvariantCulture),
                    ((darkestByLv?.Lc ?? 0) * 100).ToString("F2", CultureInfo.InvariantCulture),
                    ((brightestByLv?.Lc ?? 0) * 100).ToString("F2", CultureInfo.InvariantCulture),
                    darkestByLc?.Name ?? string.Empty,
                    brightestByLc?.Name ?? string.Empty,
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