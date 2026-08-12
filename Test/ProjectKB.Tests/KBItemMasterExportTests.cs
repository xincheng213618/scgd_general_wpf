using ColorVision.Engine.Templates.Jsons.KB;
using System.Collections.ObjectModel;
using System.IO;
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
    public void CsvPathsUseLuAndLoSuffixes()
    {
        var result = new KBItemMaster
        {
            Model = "MODEL:A",
            CreateTime = new DateTime(2026, 8, 12),
        };

        string lvPath = ViewResultManager.BuildCsvPath(result, @"C:\Exports\Lv", KBCsvDataType.Lv);
        string lcPath = ViewResultManager.BuildCsvPath(result, @"C:\Exports\Lc", KBCsvDataType.Lc);

        Assert.Equal(@"C:\Exports\Lv\MODELA_20260812-LU.csv", lvPath);
        Assert.Equal(@"C:\Exports\Lc\MODELA_20260812-LO.csv", lcPath);
    }
}
