using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.UI;
using CsvHelper;
using ProjectARVRPro.Services;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Tests;

[Collection(nameof(SocketControlTestGroup))]
public sealed class LuxFluxOutcomeTests
{
    [Theory]
    [InlineData(true, true, "H0331,SN001,00;0")]
    [InlineData(true, false, "H0331,SN001,00;")]
    [InlineData(false, false, "H0331,SN001,00;0")]
    [InlineData(false, true, "H0331,SN001,00;0")]
    public async Task T0031OnlyReplacesTheReportForAnOwnedMeasurement(bool measured, bool hasModel, string expectedReply)
    {
        string directory = Directory.CreateTempSubdirectory("LuxFluxOutcome-").FullName;
        string report = Path.Combine(directory, "D_SN001.csv");
        const string previousReport = "prior measurement for this serial number";
        var previousConfig = ConfigService.Instance;
        try
        {
            File.WriteAllText(report, previousReport);
            ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
            LuxRequest request = Assert.IsType<LuxRequest>(LuxRequest.Parse("T0031,SN001;", "H03", out string? error));
            Assert.Null(error);
            var replies = new List<string>();
            var session = new LuxCommandSession(request, replies.Add);
            SpectumResultEntity? model = hasModel ? new()
            {
                Id = 41, BatchId = 17, DataType = true, LuminousFlux = measured ? 0 : 87,
                fSpect1 = 500, fSpect2 = 501, fInterval = 1, fPL = "[1,0.5]"
            } : null;

            bool completed = await ARVRWindow.CompleteLuxFluxMeasurementAsync(session, directory, (measured, model), CancellationToken.None);
            session.Complete("duplicate");
            Assert.Equal(expectedReply, Assert.Single(replies));
            Assert.Equal(measured && hasModel, completed);
            Assert.Equal([report], Directory.GetFiles(directory));

            if (!completed)
            {
                Assert.Equal(previousReport, File.ReadAllText(report));
                return;
            }

            using var stream = File.OpenText(report);
            using var csv = new CsvReader(stream, CultureInfo.InvariantCulture);
            Assert.True(csv.Read());
            Assert.True(csv.ReadHeader());
            Assert.True(csv.Read());
            Assert.Equal("41", csv.GetField(0));
            Assert.Equal("17", csv.GetField("Lot"));
            Assert.Equal("0", csv.GetField("LuminousFlux(lm)"));
            Assert.False(csv.Read());
        }
        finally
        {
            ConfigService.SetInstance(previousConfig);
            File.Delete(report);
            Directory.Delete(directory);
        }
    }
}
