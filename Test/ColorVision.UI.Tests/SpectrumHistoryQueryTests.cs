using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Themes;
using ColorVision.UI;
using SqlSugar;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class SpectrumHistoryQueryTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RecentHistoryKeepsTheLatestQueryAndPreservesTheListOnFaultOrEndpointChange(bool retireEndpoint) => WpfTestHost.Invoke(async () =>
    {
        IConfigService previous = ConfigService.Instance;
        List<ResourceDictionary> dictionaries = [];
        string samples = Path.GetTempFileName();
        File.WriteAllText(samples, "[1,0.5]");
        try
        {
            TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
            foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
            {
                var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                dictionaries.Add(dictionary);
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }

            var device = (DeviceSpectrum)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSpectrum));
            string code = $"spectrum-history-{Guid.NewGuid():N}";
            device.SysResourceModel = new SysResourceModel { Id = 1, Code = code };
            device.Config = new ConfigSpectrum { Code = code };
            device.DisplayConfig.IsLuminousFluxMode = false;
            using var view = new ViewSpectrum(device);
            var firstOpening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int openings = 0;
            bool originalCurrent = true;
            device.ResultDatabaseEndpoint = new MySqlConnectedEndpoint(_ => Volatile.Read(ref originalCurrent), () =>
            {
                Assert.False(view.Dispatcher.CheckAccess());
                int attempt = Interlocked.Increment(ref openings);
                if (attempt == 1)
                {
                    firstOpening.SetResult();
                    releaseFirst.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
                }
                if (attempt == 3) throw new IOException("Synthetic history read failure.");
                return OpenHistory(samples);
            });

            var previousResult = new ViewResultSpectrum { Id = 99 };
            view.AddViewResultSpectrum(previousResult);
            Task oldRequest = view.LoadRecentHistoryAsync();
            try
            {
                await firstOpening.Task.WaitAsync(Deadline);
                Assert.Same(previousResult, Assert.Single(view.ViewResults));
                if (retireEndpoint)
                {
                    Volatile.Write(ref originalCurrent, false);
                    device.ResultDatabaseEndpoint = new MySqlConnectedEndpoint(_ => true,
                        () => throw new InvalidOperationException("An old query must never open a replacement database."));
                }
                else
                {
                    device.DisplayConfig.IsLuminousFluxMode = true;
                    await view.LoadRecentHistoryAsync().WaitAsync(Deadline);
                }
            }
            finally { releaseFirst.TrySetResult(); }

            await oldRequest.WaitAsync(Deadline);
            if (retireEndpoint)
            {
                Assert.Same(previousResult, Assert.Single(view.ViewResults));
                Assert.Equal(1, openings);
                return;
            }
            ViewResultSpectrum latest = Assert.Single(view.ViewResults);
            Assert.True(latest.AcquisitionFluxMode);
            Assert.Equal(new float[] { 1, .5f }, latest.fPL);
            await Assert.ThrowsAsync<IOException>(() => view.LoadRecentHistoryAsync());
            Assert.Same(latest, Assert.Single(view.ViewResults));
        }
        finally
        {
            File.Delete(samples);
            foreach (ResourceDictionary dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            TestEnvironment.SetConfig(previous);
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AdvancedBackgroundQueryCapturesFiltersAndPreservesRowsOnCancellation(bool cancel) => WpfTestHost.Invoke(async () =>
    {
        using var dialogDb = OpenHistory();
        using var lifetime = new CancellationTokenSource();
        var dispatcher = Application.Current.Dispatcher;
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new List<SpectumResultEntity> { new() { Id = 99 } };
        var query = new GenericQuery<SpectumResultEntity, SpectumResultEntity>(dialogDb, output, row =>
        {
            Assert.False(dispatcher.CheckAccess());
            return row;
        })
        {
            BackgroundQueryClientFactory = () =>
            {
                Assert.False(dispatcher.CheckAccess());
                reading.SetResult();
                releaseRead.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
                return OpenHistory();
            },
        };
        query.AddPropertyInfo(typeof(SpectumResultEntity).GetProperty(nameof(SpectumResultEntity.DataType))!);
        QueryCondition condition = Assert.Single(query.QueryConditions);
        condition.Value = false;

        Task loading = query.QueryDBAsync(lifetime.Token);
        try
        {
            await reading.Task.WaitAsync(Deadline);
            Assert.Equal(99, Assert.Single(output).Id);
            condition.Value = true;
            if (cancel) lifetime.Cancel();
        }
        finally { releaseRead.TrySetResult(); }

        if (cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loading.WaitAsync(Deadline));
            Assert.Equal(99, Assert.Single(output).Id);
        }
        else
        {
            await loading.WaitAsync(Deadline);
            Assert.False(Assert.Single(output).DataType);
        }
    });

    private static SqlSugarClient OpenHistory(string? samples = null)
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            DbType = DbType.Sqlite, ConnectionString = "Data Source=:memory:", IsAutoCloseConnection = false,
            InitKeyType = InitKeyType.Attribute,
        });
        try
        {
            db.CodeFirst.InitTables<SpectumResultEntity>();
            db.Insertable(new[]
            {
                new SpectumResultEntity { DataType = false, fPL = "[0,1]", fSpect1 = 500, fSpect2 = 501, fInterval = 1 },
                new SpectumResultEntity { DataType = true, fPL_file_name = samples, fSpect1 = 500, fSpect2 = 501, fInterval = 1 },
            }).ExecuteCommand();
            return db;
        }
        catch
        {
            db.Dispose();
            throw;
        }
    }
}
