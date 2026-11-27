using ColorVision.Engine.Templates.Jsons.KB;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Xunit;

namespace ProjectKB.Tests;

public class KBItemMasterExportTests
{
    [Fact]
    public void CsvExportUsesCapturedRecipeSnapshotAndWritesACompleteRow()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-Export-{Guid.NewGuid():N}.csv");
        try
        {
            var recipe = new KBRecipeConfig
            {
                EnableKeyLvLimit = true,
                MinKeyLv = 10,
                MaxKeyLv = 20,
                EnableKeyLcLimit = true,
                MinKeyLc = 15,
                MaxKeyLc = 25,
            };
            var result = new KBItemMaster
            {
                Model = "MODEL-A",
                SN = "SN-1",
                Items = new ObservableCollection<KBItem>
                {
                    new() { Name = "A", Lv = 5, Lc = 0.10, Result = false },
                    new() { Name = "B", Lv = 15, Lc = 0.20, Result = true },
                },
                RecipeSnapshot = KBRecipeSnapshot.Capture("MODEL-A", recipe),
            };
            recipe.MinKeyLv = 0;
            recipe.MinKeyLc = 0;

            result.SaveCsv(filePath, KBCsvDataType.Lv);

            string[] lines = File.ReadAllLines(filePath);
            string[] headers = lines[0].Split(',');
            string[] values = lines[1].Split(',');
            Assert.Equal(headers.Length, values.Length);
            Assert.Equal("1", values[Array.IndexOf(headers, "LvFailures")]);
            Assert.Equal("1", values[Array.IndexOf(headers, "LocalContrastFailures")]);
            Assert.Equal("MODEL-A", values[Array.IndexOf(headers, "LimitProfile")]);
            Assert.Equal("10.00", values[Array.IndexOf(headers, "MinKeyLv")]);
            Assert.Equal("15.00", values[Array.IndexOf(headers, "MaxDarkLocalContrast")]);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void LegacyCsvExportLeavesRecipeDerivedFieldsBlank()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-LegacyExport-{Guid.NewGuid():N}.csv");
        try
        {
            var result = new KBItemMaster
            {
                Model = "LEGACY",
                Items = new ObservableCollection<KBItem>
                {
                    new() { Name = "A", Lv = 5, Lc = 0.10, Result = false },
                },
            };

            result.SaveCsv(filePath, KBCsvDataType.Lv);

            string[] lines = File.ReadAllLines(filePath);
            string[] headers = lines[0].Split(',');
            string[] values = lines[1].Split(',');
            Assert.Equal(string.Empty, values[Array.IndexOf(headers, "LvFailures")]);
            Assert.Equal(string.Empty, values[Array.IndexOf(headers, "LocalContrastFailures")]);
            Assert.Equal(string.Empty, values[Array.IndexOf(headers, "LimitProfile")]);
            Assert.Equal(string.Empty, values[Array.IndexOf(headers, "MinKeyLv")]);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CsvExportWritesLvAndLcKeyValuesInTheirRequestedUnits()
    {
        string lvFilePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-LvExport-{Guid.NewGuid():N}.csv");
        string lcFilePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-LcExport-{Guid.NewGuid():N}.csv");
        try
        {
            var result = new KBItemMaster
            {
                Model = "MODEL-A",
                Items = new ObservableCollection<KBItem>
                {
                    new() { Name = "A", Lv = 12.34, Lc = -0.1234 },
                    new() { Name = "B", Lv = 56.78, Lc = 0.5678 },
                },
            };

            result.SaveCsv(lvFilePath, KBCsvDataType.Lv);
            result.SaveCsv(lcFilePath, KBCsvDataType.Lc);

            string[] lvValues = File.ReadAllLines(lvFilePath)[1].Split(',');
            string[] lcValues = File.ReadAllLines(lcFilePath)[1].Split(',');
            string[] headers = File.ReadAllLines(lvFilePath)[0].Split(',');
            int firstKeyIndex = Array.IndexOf(headers, "A");

            Assert.Equal("12.34", lvValues[firstKeyIndex]);
            Assert.Equal("56.78", lvValues[firstKeyIndex + 1]);
            Assert.Equal("-12.34", lcValues[firstKeyIndex]);
            Assert.Equal("56.78", lcValues[firstKeyIndex + 1]);
            Assert.Equal(lvValues[..firstKeyIndex], lcValues[..firstKeyIndex]);
            Assert.Equal(lvValues[(firstKeyIndex + 2)..], lcValues[(firstKeyIndex + 2)..]);
        }
        finally
        {
            if (File.Exists(lvFilePath))
                File.Delete(lvFilePath);
            if (File.Exists(lcFilePath))
                File.Delete(lcFilePath);
        }
    }

    [Fact]
    public void CsvPathsUseLvAndLcSuffixes()
    {
        var result = new KBItemMaster
        {
            Model = "MODEL:A",
            CreateTime = new DateTime(2026, 8, 12),
        };

        string lvPath = ViewResultManager.BuildCsvPath(result, @"C:\Exports\Lv", KBCsvDataType.Lv);
        string lcPath = ViewResultManager.BuildCsvPath(result, @"C:\Exports\Lc", KBCsvDataType.Lc);

        Assert.Equal(@"C:\Exports\Lv\MODELA_20260812-LV.csv", lvPath);
        Assert.Equal(@"C:\Exports\Lc\MODELA_20260812-LC.csv", lcPath);
    }

    [Fact]
    public void DailyCsvKeepsTheExistingLayoutAndAppendsToTheMatchingAlternate()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("ProjectKB-Layout-");
        string directory = Path.Combine(root.FullName, "exports");
        try
        {
            var original = CreateResult("ORIGINAL", "A");
            var first = CreateResult("FIRST,\n\"quoted\"", "B\n1", result: false);
            var second = CreateResult("SECOND", "B\n1");
            string path = ViewResultManager.BuildCsvPath(original, directory, KBCsvDataType.Lv);

            Assert.Equal(path, original.SaveDailyCsv(path, KBCsvDataType.Lv, appendFalloutSummary: true));
            byte[] originalBytes = File.ReadAllBytes(path);

            IOException error = Assert.Throws<IOException>(() => first.SaveCsv(path, KBCsvDataType.Lv));
            Assert.Contains(path, error.Message);
            string alternate = first.SaveDailyCsv(path, KBCsvDataType.Lv, appendFalloutSummary: true);
            Assert.Equal(alternate, second.SaveDailyCsv(path, KBCsvDataType.Lv, appendFalloutSummary: true));

            Assert.Matches(@"^MODEL-A_20261120_[0-9A-F]{12}-LV\.csv$", Path.GetFileName(alternate));
            Assert.Equal(originalBytes, File.ReadAllBytes(path));
            Assert.True(File.ReadAllBytes(alternate).AsSpan().StartsWith(Encoding.UTF8.Preamble));
            string csv = File.ReadAllText(alternate);
            Assert.Contains("\"B\n1\"", csv);
            Assert.Contains("\"FIRST,\n\"\"quoted\"\"\"", csv);
            Assert.Contains(",SECOND,", csv);
            Assert.EndsWith($"Fallout=,50.00% (1/2){Environment.NewLine}", csv);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (string file in Directory.EnumerateFiles(directory))
                    File.Delete(file);
                Directory.Delete(directory);
            }
            root.Delete();
        }
    }

    [Fact]
    public void ConcurrentCsvAppendsKeepEveryResultAndOneFalloutSummary()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ProjectKB-Concurrent-{Guid.NewGuid():N}.csv");
        try
        {
            Parallel.For(0, 16, index =>
                CreateResult($"SN-{index}", "A", result: index % 2 == 0)
                    .SaveCsv(path, KBCsvDataType.Lv, appendFalloutSummary: true));

            string[] lines = File.ReadAllLines(path);
            Assert.Equal(18, lines.Length);
            Assert.Equal("Fallout=,50.00% (8/16)", lines[^1]);
            Assert.Equal(16, lines[1..^1].Select(line => line.Split(',')[2]).Distinct().Count());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CsvAppendPreservesHistoricalEscapesAndCountsOnlyValidFalloutResults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ProjectKB-Historical-{Guid.NewGuid():N}.csv");
        try
        {
            KBItemMaster result = CreateResult("NEW", "A", result: false);
            result.SaveCsv(path, KBCsvDataType.Lv);
            string[] template = File.ReadAllLines(path);
            string[] fields = template[1].Split(',');
            int resultColumn = Array.IndexOf(template[0].Split(','), "Result");
            fields[2] = "\"历史,\r\n\"\"旧\"\"\"";
            fields[resultColumn] = "T\"ru\"e";
            string passed = string.Join(",", fields);
            fields[2] = "Legacy";
            fields[resultColumn] = "\" False \"";
            string failed = string.Join(",", fields);
            fields[resultColumn] = "unknown";
            string unknown = string.Join(",", fields);
            fields[0] = "\"faLLout=\"";
            fields[resultColumn] = "True";
            string formerSummary = string.Join(",", fields);

            File.WriteAllText(path, $"{template[0]}\r{passed}\n{formerSummary}\r\n{failed}\r\n{unknown}\n \r\"Fallout=\",removed\r\n\t", Encoding.UTF8);
            result.SaveCsv(path, KBCsvDataType.Lv, appendFalloutSummary: true);

            string expected = string.Join(Environment.NewLine,
                template[0], passed, formerSummary, failed, unknown, template[1], "Fallout=,66.67% (2/3)", string.Empty);
            Assert.Equal(expected, File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FailedCsvReplacementLeavesExistingBytesAndRemovesTheStagingFile()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ProjectKB-FileInUse-");
        string path = Path.Combine(directory.FullName, "results.csv");
        try
        {
            CreateResult("ORIGINAL", "A").SaveCsv(path, KBCsvDataType.Lv);
            byte[] original = File.ReadAllBytes(path);

            Exception? error;
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                error = Record.Exception(() => CreateResult("SECOND", "A").SaveCsv(path, KBCsvDataType.Lv));

            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(path, Assert.Single(Directory.EnumerateFiles(directory.FullName)));
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(directory.FullName))
                File.Delete(file);
            directory.Delete();
        }
    }

    private static KBItemMaster CreateResult(string serialNumber, string keyName, bool result = true)
        => new()
        {
            Model = "MODEL-A",
            SN = serialNumber,
            CreateTime = new DateTime(2026, 11, 20),
            Result = result,
            Items = new ObservableCollection<KBItem>
            {
                new() { Name = keyName, Lv = 5, Lc = 0.10, Result = result },
            },
        };
}
