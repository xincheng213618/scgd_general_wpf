using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using FlowEngineLib.Base;
using SqlSugar;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class LocalMeasurementFlowDatabaseTests
{
    [Fact]
    public void SpectrumAndSmuSaveToTheOriginalFlowDatabaseAfterReconnect()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-local-measurement-db-");
        try
        {
            MySqlConnectedEndpoint? active = null;
            MySqlConnectedEndpoint CreateEndpoint(string file) => new(candidate => ReferenceEquals(candidate, active),
                () => new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = $"Data Source={Path.Combine(directory.FullName, file)};Pooling=False",
                    DbType = DbType.Sqlite, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute,
                }));
            MySqlConnectedEndpoint first = CreateEndpoint("a.db"), second = CreateEndpoint("b.db");
            var action = new CVStartCFC("same-batch") { PersistResults = true };
            using var resources = action.RuntimeResources;
            using var a = first.CreateDbClient();
            using var b = second.CreateDbClient();
            Type[] tables = [typeof(MeasureBatchModel), typeof(SpectumResultEntity), typeof(SMUResultModel), typeof(SmuScanModel)];
            a.CodeFirst.InitTables(tables);
            b.CodeFirst.InitTables(tables);
            active = first;
            var batch = new MeasureBatchModel { Code = action.SerialNumber, Name = action.SerialNumber };
            FlowMeasurementBatch.Insert(batch, first).Bind(resources);
            int otherBatchId = b.Insertable(new MeasureBatchModel { Name = action.SerialNumber, Code = action.SerialNumber, Result = "original B" }).ExecuteReturnIdentity();
            Assert.Equal(batch.Id, otherBatchId);
            MySqlConnectedEndpoint? captured = LocalFlowResultPersistence.ResolveEndpoint(action);
            Assert.Same(first, captured);

            active = second;
            Assert.False(first.IsCurrent);
            var parameters = new LocalSmuParameters { MeasureValue = 1, BeginValue = 1, EndValue = 2, LimitValue = 10 };
            var point = new LocalSmuCapture(parameters, [1.1], [2.1], 2, false) { Details = Details([1]) };
            var scan = new LocalSmuCapture(parameters, [1.1, 2.2], [2.1, 3.2], 3, true) { Details = Details([1, 2]) };
            var spectrum = new LocalSpectrumCapture { DeviceCode = "SPECTRUM.A", IntegralTime = 17 };
            Assert.Throws<InvalidOperationException>(() => LocalSmuResultService.SaveFlow(point, "SMU.A", action, 2, second));
            Assert.Throws<InvalidOperationException>(() => LocalSpectrumResultService.SaveFlow(spectrum, new(), action, 4, second));

            int pointId = LocalSmuResultService.SaveFlow(point, "SMU.A", action, 2, captured);
            int scanId = LocalSmuResultService.SaveFlow(scan, "SMU.A", action, 3, captured);
            action.Data["SMUResult"] = new FlowEngineLib.SMUResultData(FlowEngineLib.SMUChannelType.A, point.V, point.I, pointId, LocalSmuResultService.ResultType);
            int spectrumId = LocalSpectrumResultService.SaveFlow(spectrum, new(), action, 4, captured).Id;
            Assert.True(pointId > 0 && scanId > 0 && spectrumId > 0);
            Assert.Equal(spectrumId, spectrum.MeasurementId);

            SMUResultModel storedPoint = Assert.Single(a.Queryable<SMUResultModel>().ToList());
            SmuScanModel storedScan = Assert.Single(a.Queryable<SmuScanModel>().ToList());
            SpectumResultEntity storedSpectrum = Assert.Single(a.Queryable<SpectumResultEntity>().ToList());
            Assert.Equal(((int?)batch.Id, (int?)2, (float?)1.1f), (storedPoint.BatchId, storedPoint.ZIndex, storedPoint.VResult));
            Assert.Equal(((int?)batch.Id, (int?)3, (int?)2), (storedScan.BatchId, storedScan.ZIndex, storedScan.Points));
            Assert.Equal(((int?)batch.Id, (int?)4, (int?)pointId, (float?)17f), (storedSpectrum.BatchId, storedSpectrum.Zindex, storedSpectrum.SmuDataId, storedSpectrum.IntTime));
            Assert.Equal(new[] { (false, pointId), (true, scanId) }, a.Queryable<SmuLocalDetailsModel>().OrderBy(row => row.IsScan)
                .ToList().Select(row => (row.IsScan, row.ResultId)));

            Assert.Equal("original B", Assert.Single(b.Queryable<MeasureBatchModel>().ToList()).Result);
            Assert.Empty(b.Queryable<SMUResultModel>().ToList());
            Assert.Empty(b.Queryable<SmuScanModel>().ToList());
            Assert.Empty(b.Queryable<SpectumResultEntity>().ToList());
            Assert.False(b.DbMaintenance.IsAnyTable(SmuLocalDetailsModel.TableName, false));

            int interactiveId = LocalSmuResultService.Save(point, "SMU.A", captured);
            var interactive = a.Queryable<SMUResultModel>().First(row => row.Id == interactiveId);
            Assert.NotNull(interactive);
            Assert.Null(interactive.BatchId);
            Assert.Equal(1.1f, interactive.VResult);
            Assert.Empty(b.Queryable<SMUResultModel>().ToList());
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }

        static LocalSmuDetails Details(double[] sources) => new(new LocalSmuSettings(), sources,
            new double?[sources.Length], new int[sources.Length], "offline-test");
    }

    [Fact]
    public void DisabledPersistenceAndSmuWithoutSamplesDoNotOpenADatabase()
    {
        int attempts = 0;
        var endpoint = new MySqlConnectedEndpoint(_ => true, () =>
        {
            attempts++;
            throw new InvalidOperationException("No SQL client is expected.");
        });
        var action = new CVStartCFC("offline-samples") { PersistResults = false };
        using var resources = action.RuntimeResources;
        var point = new LocalSmuCapture(new(), [1], [2], 0, false);
        var spectrum = new LocalSpectrumCapture { IntegralTime = 17 };
        Assert.Equal(0, LocalSmuResultService.SaveFlow(point, "SMU", action, 0, endpoint));
        Assert.Equal(0, LocalSpectrumResultService.SaveFlow(spectrum, new(), action, 0, endpoint).Id);

        action.PersistResults = true;
        Assert.Equal(0, LocalSmuResultService.SaveFlow(point with { Voltages = [], Currents = [] }, "SMU", action, 0, endpoint));
        Assert.Throws<InvalidOperationException>(() => LocalSmuResultService.SaveFlow(point, "SMU", action, 0, null));
        Assert.Throws<InvalidOperationException>(() => LocalSpectrumResultService.SaveFlow(spectrum, new(), action, 0, null));
        Assert.Equal(0, attempts);
    }
}
