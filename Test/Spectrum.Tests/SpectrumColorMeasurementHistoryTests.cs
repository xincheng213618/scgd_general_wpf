using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.UI;
using SqlSugar;
using System.IO;
using System.Runtime.CompilerServices;

namespace Spectrum.Tests;

[Collection("LocalSpectrumDriver")]
public sealed class SpectrumColorMeasurementHistoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedHistoryCannotLoadAnIdenticalRowIdFromAReplacementDatabase(bool switchWhileLoading)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-spectrum-history-");
        var previousConfig = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        try
        {
            const string code = "SPECTRUM.HISTORY";
            const string linkedSpectrum = "measurement.json";
            const string alternateSpectrum = "replacement.json";
            File.WriteAllText(Path.Combine(directory.FullName, linkedSpectrum), "[12,15]");
            File.WriteAllText(Path.Combine(directory.FullName, alternateSpectrum), "[90,92]");

            var device = (DeviceSpectrum)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSpectrum));
            device.Config = new ConfigSpectrum { Code = code };
            device.Config.FileServerCfg.DataBasePath = directory.FullName;
            device.SysResourceModel = new SysResourceModel { Id = 1, Code = code };

            MySqlConnectedEndpoint? active = null;
            int secondOpenCount = 0;
            Action? onFirstOpen = null;
            MySqlConnectedEndpoint Endpoint(string name, Action onOpen) => new(candidate => ReferenceEquals(candidate, active),
                () =>
                {
                    onOpen();
                    return new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = $"Data Source={Path.Combine(directory.FullName, name)};Pooling=False",
                        DbType = DbType.Sqlite, InitKeyType = InitKeyType.Attribute, IsAutoCloseConnection = true,
                    });
                });
            var first = Endpoint("a.db", () => onFirstOpen?.Invoke());
            var second = Endpoint("b.db", () => secondOpenCount++);
            void SwitchTo(MySqlConnectedEndpoint endpoint)
            {
                active = endpoint;
                device.ResultDatabaseEndpoint = endpoint;
            }

            int AddRow(MySqlConnectedEndpoint endpoint, float y, string spectrum)
            {
                using var db = endpoint.CreateDbClient();
                db.CodeFirst.InitTables<SpectumResultEntity>();
                return db.Insertable(new SpectumResultEntity
                {
                    DeviceCode = code, fPh = y, fx = 0.3f, fy = 0.32f, fPL_file_name = spectrum,
                }).ExecuteReturnIdentity();
            }
            int firstId = AddRow(first, 12, linkedSpectrum);
            Assert.Equal(firstId, AddRow(second, 90, alternateSpectrum));
            secondOpenCount = 0;
            SwitchTo(first);

            var history = SpectrumColorMeasurementHistory.Capture(device);
            var row = Assert.Single(await history.ReadRecentAsync());
            Assert.Equal(firstId, row.ResultId);
            Assert.Equal(12f, row.Y);
            var measurement = await history.LoadAsync(row.ResultId);
            Assert.Equal(12d, measurement.Y);
            Assert.Equal(12d, measurement.Spectrum[0].Value);

            if (switchWhileLoading) onFirstOpen = () => SwitchTo(second);
            else SwitchTo(second);

            await Assert.ThrowsAsync<InvalidOperationException>(() => history.LoadAsync(row.ResultId));
            Assert.Equal(0, secondOpenCount);

            var replacement = Assert.Single(await device.GetRecentColorMeasurementsAsync());
            Assert.Equal(firstId, replacement.ResultId);
            var replacementMeasurement = await device.LoadColorMeasurementAsync(replacement.ResultId);
            Assert.Equal(90d, replacementMeasurement.Y);
            Assert.Equal(90d, replacementMeasurement.Spectrum[0].Value);
        }
        finally
        {
            ConfigService.SetInstance(previousConfig);
            foreach (var file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }
}
