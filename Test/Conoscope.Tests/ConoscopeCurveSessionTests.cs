using Conoscope.Core;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;

namespace Conoscope.Tests;

public sealed class ConoscopeCurveSessionTests
{
    [Fact]
    public void RoundTripPreservesSourcesCaptureTimeNamesSelectionVisibilityAndGaps()
    {
        string file = Path.GetTempFileName();
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var session = new ConoscopeCurveSession(new[]
            {
                new ConoscopeCurveSessionEntry(Create("A", @"C:\first\same.cvcie"), false, 4),
                new ConoscopeCurveSessionEntry(Create("B", @"D:\second\same.cvcie"), true, 2)
            }, 1);
            ConoscopeCurveSessionFile.Save(file, session);
            ConoscopeCurveSession restored = ConoscopeCurveSessionFile.Load(file);
            Assert.Equal(1, restored.SelectedIndex);
            Assert.Equal(2, restored.Entries.Count);
            Assert.Contains("null", File.ReadAllText(file));
            Assert.DoesNotContain("NaN", File.ReadAllText(file));
            for (int index = 0; index < session.Entries.Count; index++)
            {
                var expected = session.Entries[index];
                var actual = restored.Entries[index];
                Assert.Equal(expected.IsShown, actual.IsShown);
                Assert.Equal(expected.ColorIndex, actual.ColorIndex);
                using StringWriter expectedCsv = new();
                using StringWriter actualCsv = new();
                expected.Snapshot.WriteCsv(expectedCsv);
                actual.Snapshot.WriteCsv(actualCsv);
                Assert.Equal(expectedCsv.ToString(), actualCsv.ToString());
                Assert.Equal(expected.Snapshot.SourcePath, actual.Snapshot.WithName("Renamed").SourcePath);
            }
            Assert.True(restored.Entries[0].Snapshot.IsCompatibleWith(restored.Entries[1].Snapshot));
        }
        finally { CultureInfo.CurrentCulture = previous; File.Delete(file); }
    }

    [Theory]
    [InlineData("version")]
    [InlineData("selection")]
    [InlineData("axis")]
    [InlineData("length")]
    [InlineData("name")]
    [InlineData("time")]
    [InlineData("color")]
    public void RejectsIncompleteOrUnsupportedSessions(string corruption)
    {
        string file = Path.GetTempFileName();
        try
        {
            ConoscopeCurveSessionFile.Save(file, new(new[] { new ConoscopeCurveSessionEntry(Create("A", "a"), true, 0) }, 0));
            JsonNode json = JsonNode.Parse(File.ReadAllText(file))!;
            JsonNode curve = json["Curves"]![0]!;
            switch (corruption)
            {
                case "version": json["Version"] = 2; break;
                case "selection": json["SelectedIndex"] = 3; break;
                case "axis": curve["AxisKey"] = ""; break;
                case "length": curve["Values"] = new JsonArray(1); break;
                case "name": curve["Name"] = " "; break;
                case "time": curve["CapturedAtUtc"] = "0001-01-01T00:00:00+00:00"; break;
                case "color": curve["ColorIndex"] = -1; break;
            }
            File.WriteAllText(file, json.ToJsonString());
            Assert.ThrowsAny<Exception>(() => ConoscopeCurveSessionFile.Load(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void FailedSaveLeavesExistingSessionIntact()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "previous contents");
            Assert.Throws<InvalidDataException>(() => ConoscopeCurveSessionFile.Save(file, new(Array.Empty<ConoscopeCurveSessionEntry>(), 7)));
            Assert.Equal("previous contents", File.ReadAllText(file));
        }
        finally { File.Delete(file); }
    }

    private static ConoscopeCurveSnapshot Create(string name, string path) => new(name, "same.cvcie", "VA60", "H/V", "H=20°",
        "V (°)", "Y", "cd/m²", new[] { -1.2345678901234567, 0, 1.2345678901234567 },
        new[] { 100.12345678901234, double.NaN, -2.345678901234567 },
        new DateTimeOffset(2026, 9, 12, 12, 30, 45, TimeSpan.FromHours(8)).AddTicks(1234567), path,
        "Filter=off; Exposure=10; Reference=\"source,one\"", "HorizontalVertical:V-degrees");
}
