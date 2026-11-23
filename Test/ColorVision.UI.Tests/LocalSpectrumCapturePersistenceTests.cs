using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using cvColorVision;
using SqlSugar;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class LocalSpectrumCapturePersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandaloneSamplesUseTheCapturedEndpointAndNeverRetryOnItsReplacement(bool rejectSecondSample)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-spectrum-panel-db-");
        try
        {
            MySqlConnectedEndpoint? current = null;
            string firstPath = Path.Combine(directory.FullName, "a.db");
            string secondPath = Path.Combine(directory.FullName, "b.db");
            int replacementOpens = 0;
            var original = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(endpoint, current), () => Open(firstPath));
            var replacement = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(endpoint, current), () =>
            {
                replacementOpens++;
                return Open(secondPath);
            });
            using var a = Open(firstPath);
            using var b = Open(secondPath);
            a.CodeFirst.InitTables<SpectumResultEntity>();
            b.CodeFirst.InitTables<SpectumResultEntity>();
            int replacementId = b.Insertable(new SpectumResultEntity { DeviceCode = "panel-spectrum", fPh = 420 })
                .ExecuteReturnIdentity();

            MySqlConnectedEndpoint? offlineEndpoint = current;
            current = original;
            MySqlConnectedEndpoint seriesEndpoint = current;
            var parameters = new LocalSpectrumParameters { NumberOfAverage = 2 };
            LocalSpectrumCapture first = Sample(1);
            SpectumResultEntity firstModel = LocalSpectrumResultService.Save(first, parameters, seriesEndpoint);
            Assert.Equal(replacementId, firstModel.Id);
            Assert.Equal(first.MeasurementId, firstModel.Id);
            if (rejectSecondSample)
                a.Ado.ExecuteCommand("CREATE TRIGGER reject_sample BEFORE INSERT ON t_scgd_measure_result_spectrometer BEGIN SELECT RAISE(ABORT, 'synthetic spectrum failure'); END");

            current = replacement;
            Assert.False(original.IsCurrent);
            LocalSpectrumCapture second = Sample(2);
            if (rejectSecondSample)
            {
                Exception error = Assert.ThrowsAny<Exception>(() => LocalSpectrumResultService.Save(second, parameters, seriesEndpoint));
                Assert.Contains("synthetic spectrum failure", error.ToString());
                Assert.Equal(0, second.MeasurementId);
            }
            else
            {
                SpectumResultEntity secondModel = LocalSpectrumResultService.Save(second, parameters, seriesEndpoint);
                Assert.True(secondModel.Id > firstModel.Id);
                Assert.Equal(second.MeasurementId, secondModel.Id);
            }

            LocalSpectrumCapture offline = Sample(3);
            SpectumResultEntity offlineModel = LocalSpectrumResultService.Save(offline, parameters, offlineEndpoint);
            Assert.Equal((0, 0), (offlineModel.Id, offline.MeasurementId));
            Assert.Equal(3f, offlineModel.fPh);
            var stored = a.Queryable<SpectumResultEntity>().OrderBy(row => row.Id).ToList();
            Assert.Equal(rejectSecondSample ? new float?[] { 1 } : new float?[] { 1, 2 }, stored.Select(row => row.fPh));
            Assert.All(stored, row =>
            {
                Assert.Null(row.BatchId);
                Assert.Equal(17f, row.IntTime);
                Assert.Equal(2, row.iAveNum);
            });
            Assert.Equal(420f, Assert.Single(b.Queryable<SpectumResultEntity>().ToList()).fPh);
            Assert.Equal(0, replacementOpens);
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private static LocalSpectrumCapture Sample(float luminance) => new()
    {
        DeviceCode = "panel-spectrum",
        IntegralTime = 17,
        Data = new COLOR_PARA { fPh = luminance, fPL = [1f, 2f] },
    };

    private static SqlSugarClient Open(string path) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source={path};Pooling=False",
        DbType = DbType.Sqlite,
        InitKeyType = InitKeyType.Attribute,
        IsAutoCloseConnection = true,
    });
}
