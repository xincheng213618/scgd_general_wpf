using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProjectKB
{
    public enum KBCsvDataType
    {
        Lv,
        Lc,
    }

    public static class KBItemMasterExtensions
    {
        private const string FalloutLabel = "Fallout=";
        private static readonly object CsvWriteGate = new();

        public static void SaveCsv(this KBItemMaster KBItems, string FileName, KBCsvDataType dataType, bool appendFalloutSummary = false)
            => WriteCsv(KBItems, FileName, dataType, appendFalloutSummary, automatic: false);

        internal static string SaveDailyCsv(this KBItemMaster item, string fileName, KBCsvDataType dataType, bool appendFalloutSummary)
            => WriteCsv(item, fileName, dataType, appendFalloutSummary, automatic: true);

        private static string WriteCsv(KBItemMaster item, string fileName, KBCsvDataType dataType, bool appendFalloutSummary, bool automatic)
        {
            // LvFailures 是单键亮度上下限失败的按键数量
            // LocalContrastFailures 是局部对比度上下限失败的按键数量
            // DarkKeyLocalContrast 是最暗Key的Lc
            // BrightKeyLocalContrast 是最亮Key的Lc

            //LocalDarkestKey  判定的是Lc 是最小Key
            //LocalBrightestKey  判定的是Lc 是最大Key

            //ColorDifference 是彩色才有，目前还是空

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

            for (int i = 0; i < item.Items.Count; i++)
            {
                properties.Add(EscapeCsv(item.Items[i].Name));
            }
            properties.AddRange(properyties1);

            string newHeaders = string.Join(",", properties);
            KBRecipeSnapshot? recipeSnapshot = item.RecipeSnapshot;
            KBRecipeConfig? recipe = recipeSnapshot?.Recipe;
            var darkestByLv = item.Items.OrderBy(x => x.Lv).FirstOrDefault();
            var brightestByLv = item.Items.OrderByDescending(x => x.Lv).FirstOrDefault();
            var darkestByLc = item.Items.OrderBy(x => x.Lc).FirstOrDefault();
            var brightestByLc = item.Items.OrderByDescending(x => x.Lc).FirstOrDefault();

            string lvFailures = recipe == null
                ? string.Empty
                : (recipe.EnableKeyLvLimit
                    ? item.Items.Count(key => key.Lv < recipe.MinKeyLv || key.Lv > recipe.MaxKeyLv)
                    : 0).ToString(CultureInfo.InvariantCulture);

            string localContrastFailures = recipe == null
                ? string.Empty
                : (recipe.EnableKeyLcLimit
                    ? item.Items.Count(key => key.Lc < recipe.MinKeyLc / 100 || key.Lc > recipe.MaxKeyLc / 100)
                    : 0).ToString(CultureInfo.InvariantCulture);
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
                    lvFailures,
                    localContrastFailures,
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
                double value = dataType == KBCsvDataType.Lv
                    ? item.Items[i].Lv
                    : item.Items[i].Lc * 100;
                values.Add(value.ToString("F2", CultureInfo.InvariantCulture));
            }
            values.Add(EscapeCsv(GetLimitProfile(recipeSnapshot)));
            values.Add(FormatRecipeValue(recipe, value => value.MinKeyLv));
            values.Add(FormatRecipeValue(recipe, value => value.MaxKeyLv));
            values.Add(FormatRecipeValue(recipe, value => value.MinAvgLv));
            values.Add(FormatRecipeValue(recipe, value => value.MaxAvgLv));
            values.Add(FormatRecipeValue(recipe, value => value.MinUniformity));
            values.Add(FormatRecipeValue(recipe, value => value.MinKeyLc));
            values.Add(FormatRecipeValue(recipe, value => value.MaxKeyLc));
            values.Add(string.Empty);
            values.Add(string.Empty);
            values.Add(string.Empty);
            values.Add(string.Empty);
            values.Add(string.Empty);

            string destination = Path.GetFullPath(fileName);
            string directory = Path.GetDirectoryName(destination)!;
            // Serialize read, append and replacement across automatic and manual exports.
            lock (CsvWriteGate)
            {
                if (automatic)
                    Directory.CreateDirectory(directory);

                List<string>? records = LoadAppendableRecords(destination, newHeaders);
                if (records == null && automatic)
                {
                    string layout = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(newHeaders)))[..12];
                    string stem = Path.GetFileNameWithoutExtension(destination);
                    string typeSuffix = dataType == KBCsvDataType.Lv ? "-LV" : "-LC";
                    int suffixStart = stem.EndsWith(typeSuffix, StringComparison.OrdinalIgnoreCase) ? stem.Length - typeSuffix.Length : stem.Length;
                    destination = Path.Combine(directory, $"{stem[..suffixStart]}_{layout}{stem[suffixStart..]}{Path.GetExtension(destination)}");
                    records = LoadAppendableRecords(destination, newHeaders);
                }

                if (records == null)
                    throw new IOException($"现有 CSV 的表头与本次结果不同，请选择新的文件名或移动原文件。{Environment.NewLine}{destination}");

                records.Add(string.Join(",", values));
                if (appendFalloutSummary)
                    records.Add(BuildFalloutSummary(records, properties.IndexOf("Result")));

                // Stage beside the destination to keep replacement on the same volume.
                string stagingPath = Path.Combine(directory, $".{Guid.NewGuid():N}.kbcsv.tmp");
                try
                {
                    File.WriteAllLines(stagingPath, records.Prepend(newHeaders), Encoding.UTF8);
                    File.Move(stagingPath, destination, overwrite: true);
                }
                finally
                {
                    if (File.Exists(stagingPath))
                        File.Delete(stagingPath);
                }

                return destination;
            }
        }

        private static string GetLimitProfile(KBRecipeSnapshot? snapshot)
        {
            if (snapshot == null)
                return string.Empty;

            return snapshot.Origin == KBRecipeSnapshotOrigin.RebuiltFromCurrentRecipe
                ? $"{snapshot.RecipeName} (RebuiltFromCurrentRecipe)"
                : snapshot.RecipeName;
        }

        private static string FormatRecipeValue(KBRecipeConfig? recipe, Func<KBRecipeConfig, double> selector)
        {
            return recipe == null
                ? string.Empty
                : selector(recipe).ToString("F2", CultureInfo.InvariantCulture);
        }

        private static List<string>? LoadAppendableRecords(string fileName, string headers)
        {
            string csv;
            try
            {
                csv = File.ReadAllText(fileName, Encoding.UTF8);
            }
            catch (FileNotFoundException)
            {
                return [];
            }

            if (csv.Length == 0 || csv.Equals(headers, StringComparison.Ordinal))
                return [];
            if (!csv.StartsWith(headers, StringComparison.Ordinal) || csv[headers.Length] is not ('\r' or '\n'))
                return null;

            int start = headers.Length + 1;
            if (csv[headers.Length] == '\r' && start < csv.Length && csv[start] == '\n')
                start++;

            List<string> records = [];
            bool inQuotes = false;
            for (int index = start; index < csv.Length; index++)
            {
                char character = csv[index];
                if (character == '"')
                    inQuotes = !inQuotes;
                if (inQuotes || character is not ('\r' or '\n'))
                    continue;

                records.Add(csv[start..index]);
                if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                    index++;
                start = index + 1;
            }

            if (inQuotes)
                throw new IOException($"现有 CSV 包含未闭合的引号，请检查原文件或选择新的文件名。{Environment.NewLine}{fileName}");
            if (start < csv.Length)
                records.Add(csv[start..]);

            TrimTrailingSummaryLines(records);
            return records;
        }

        private static void TrimTrailingSummaryLines(List<string> lines)
        {
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }

            if (lines.Count > 0 && IsFalloutSummaryLine(lines[^1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
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
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> fields = ParseCsvLine(line);
                if (fields.Count <= resultColumnIndex || string.Equals(fields[0], FalloutLabel, StringComparison.OrdinalIgnoreCase))
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
