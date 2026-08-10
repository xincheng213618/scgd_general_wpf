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

            result.SaveCsv(filePath);

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

            result.SaveCsv(filePath);

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
}
