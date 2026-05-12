using System.Globalization;
using System.IO;
using System.Text;

namespace ProjectKB
{
    public static class KBItemMasterExtensions
    {
        private const string FalloutLabel = "Fallout=";

        public static void SaveCsv(this KBItemMaster KBItems, string FileName, bool appendFalloutSummary = false)
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
                properties.Add(EscapeCsv(KBItems.Items[i].Name));
            }
            properties.AddRange(properyties1);

            string newHeaders = string.Join(",", properties);
            var item = KBItems;
            RecipeManager recipeManager = RecipeManager.GetInstance();
            KBRecipeConfig recipe = recipeManager.RecipeConfigs.TryGetValue(item.Model, out KBRecipeConfig? matchedRecipe)
                ? matchedRecipe
                : recipeManager.RecipeConfig;
            var darkestByLv = item.Items.OrderBy(x => x.Lv).FirstOrDefault();
            var brightestByLv = item.Items.OrderByDescending(x => x.Lv).FirstOrDefault();
            var darkestByLc = item.Items.OrderBy(x => x.Lc).FirstOrDefault();
            var brightestByLc = item.Items.OrderByDescending(x => x.Lc).FirstOrDefault();

            int lvFailures = recipe.EnableKeyLvLimit
                ? item.Items.Count(key => key.Lv < recipe.MinKeyLv || key.Lv > recipe.MaxKeyLv)
                : 0;

            int localContrastFailures = recipe.EnableKeyLcLimit
                ? item.Items.Count(key => key.Lc < recipe.MinKeyLc / 100 || key.Lc > recipe.MaxKeyLc / 100)
                : 0;
            List<string> values = new()
                {
                    item.Id.ToString(),
                    EscapeCsv(item.Model),
                    EscapeCsv(item.SN),
                    EscapeCsv(item.KBTemplate),
                    item.AvgLv.ToString("F2",CultureInfo.InvariantCulture),
                    item.MinLv.ToString("F2",CultureInfo.InvariantCulture),
                    item.MaxLv.ToString("F2",CultureInfo.InvariantCulture),
                    item.LvUniformity.ToString("F2",CultureInfo.InvariantCulture),
                    EscapeCsv(item.DrakestKey),
                    EscapeCsv(item.BrightestKey),
                    "",
                    item.NbrFailPoints.ToString(CultureInfo.InvariantCulture),
                    lvFailures.ToString(CultureInfo.InvariantCulture),
                    localContrastFailures.ToString(CultureInfo.InvariantCulture),
                    ((darkestByLv?.Lc ?? 0) * 100).ToString("F2", CultureInfo.InvariantCulture),
                    ((brightestByLv?.Lc ?? 0) * 100).ToString("F2", CultureInfo.InvariantCulture),
                    EscapeCsv(darkestByLc?.Name ?? string.Empty),
                    EscapeCsv(brightestByLc?.Name ?? string.Empty),
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

            List<string> outputLines = LoadAppendableLines(FileName, newHeaders, out bool appendData);
            if (!appendData)
            {
                outputLines.Clear();
                outputLines.Add(newHeaders);
            }

            outputLines.Add(string.Join(",", values));

            if (appendFalloutSummary)
            {
                int resultColumnIndex = properties.IndexOf("Result");
                outputLines.Add(BuildFalloutSummary(outputLines.Skip(1), resultColumnIndex));
            }

            csvBuilder.Append(string.Join(Environment.NewLine, outputLines));
            csvBuilder.Append(Environment.NewLine);
            File.WriteAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
        }

        private static List<string> LoadAppendableLines(string fileName, string headers, out bool appendData)
        {
            appendData = false;

            if (!File.Exists(fileName))
            {
                return new List<string>();
            }

            List<string> lines = File.ReadAllLines(fileName, Encoding.UTF8).ToList();
            if (lines.Count == 0 || !string.Equals(lines[0], headers, StringComparison.Ordinal))
            {
                return new List<string>();
            }

            appendData = true;
            TrimTrailingSummaryLines(lines);
            return lines;
        }

        private static void TrimTrailingSummaryLines(List<string> lines)
        {
            while (lines.Count > 1 && string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }

            if (lines.Count > 1 && IsFalloutSummaryLine(lines[^1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }

            while (lines.Count > 1 && string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        private static bool IsFalloutSummaryLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            List<string> fields = ParseCsvLine(line);
            return fields.Count > 0 && string.Equals(fields[0], FalloutLabel, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFalloutSummary(IEnumerable<string> dataLines, int resultColumnIndex)
        {
            int totalCount = 0;
            int passCount = 0;

            foreach (string line in dataLines)
            {
                if (string.IsNullOrWhiteSpace(line) || IsFalloutSummaryLine(line))
                {
                    continue;
                }

                List<string> fields = ParseCsvLine(line);
                if (fields.Count <= resultColumnIndex)
                {
                    continue;
                }

                if (!bool.TryParse(fields[resultColumnIndex], out bool result))
                {
                    continue;
                }

                totalCount++;
                if (result)
                {
                    passCount++;
                }
            }

            string passRate = totalCount == 0
                ? "0.00"
                : ((double)(totalCount-passCount) / totalCount * 100).ToString("F2", CultureInfo.InvariantCulture);

            return string.Join(",", new[]
            {
                EscapeCsv(FalloutLabel),
                EscapeCsv($"{passRate}% ({totalCount-passCount}/{totalCount})")
            });
        }

        private static string EscapeCsv(string? value)
        {
            string text = value ?? string.Empty;
            if (text.Contains(',') || text.Contains('"') || text.Contains('\r') || text.Contains('\n'))
            {
                return $"\"{text.Replace("\"", "\"\"")}\"";
            }

            return text;
        }

        private static List<string> ParseCsvLine(string line)
        {
            List<string> fields = new();
            StringBuilder fieldBuilder = new();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];
                if (current == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        fieldBuilder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (current == ',' && !inQuotes)
                {
                    fields.Add(fieldBuilder.ToString());
                    fieldBuilder.Clear();
                    continue;
                }

                fieldBuilder.Append(current);
            }

            fields.Add(fieldBuilder.ToString());
            return fields;
        }
    }
}