using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.FlowProcessing.PostProcess.IVL;
using ColorVision.Engine.FlowProcessing.PostProcess.Poi;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Themes;
using ColorVision.UI;
using Newtonsoft.Json;
using SqlSugar;
using System.IO;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed partial class PostProcessDatabaseAffinityTests
{
    private const int BatchId = 21;
    private const int MasterId = 1;

    [Fact]
    public void OwnedBatchFactoryStillReadsItsDatabaseAfterReconnect()
    {
        using var databases = new ResultDatabases();
        var batch = new MeasureBatchModel { Id = BatchId, DatabaseEndpoint = databases.First };
        Func<SqlSugarClient>? original = BatchResultMasterDao.Instance.TryGetOwnedDatabaseFactory(batch);
        Assert.NotNull(original);

        databases.Current = databases.Second;
        Assert.False(databases.First.IsCurrent);
        var replay = BatchResultMasterDao.Instance.TryGetOwnedDatabaseFactory(batch);
        Assert.Equal(original, replay);
        var replacement = new MeasureBatchModel { Id = BatchId, DatabaseEndpoint = databases.Second };
        Assert.NotEqual(replay, BatchResultMasterDao.Instance.TryGetOwnedDatabaseFactory(replacement));
        using var db = replay!();
        var result = Assert.Single(db.Queryable<AlgResultMasterModel>().Where(item => item.BatchId == batch.Id).ToList());
        Assert.Equal("original-master", result.Result);
        Assert.Null(BatchResultMasterDao.Instance.TryGetOwnedDatabaseFactory(new MeasureBatchModel { Id = BatchId }));
    }

    [Fact]
    public void PoiExportReadsMasterAndDetailsFromTheBatchDatabaseAfterReconnect()
    {
        using var databases = new ResultDatabases();
        databases.Current = databases.Second;
        var context = new PostProcessContext(new PostProcessConfig { SavePath = databases.DirectoryPath })
        {
            Batch = new MeasureBatchModel { Id = BatchId, FlowStatus = FlowStatus.Completed, DatabaseEndpoint = databases.First },
        };

        Assert.True(new PoiPostProcessor().Process(context));

        string csv = File.ReadAllText(Assert.Single(Directory.GetFiles(databases.DirectoryPath, "PoiY_*.csv")));
        Assert.Contains("original-poi", csv);
        Assert.DoesNotContain("replacement-poi", csv);
    }

    [Fact]
    public void IvScanExportReadsFromTheBatchDatabaseAfterReconnect()
    {
        using var databases = new ResultDatabases();
        databases.Current = databases.Second;
        var context = new PostProcessContext(new PostProcessConfig { SavePath = databases.DirectoryPath })
        {
            FlowName = "synthetic-iv-scan",
            Batch = new MeasureBatchModel { Id = BatchId, FlowStatus = FlowStatus.Completed, DatabaseEndpoint = databases.First },
        };
        var processor = new IvScanPostProcessor();
        processor.Config.ShowPlot = false;

        Assert.True(processor.Process(context));

        string[] csv = File.ReadAllLines(Assert.Single(Directory.GetFiles(databases.DirectoryPath, "IV_Scan_*.csv")));
        Assert.Contains(",7,8,", Assert.Single(csv.Skip(1)));
    }

    [Fact]
    public Task PendingAlgorithmResultCannotReadOrPublishFromAnotherDatabase()
    {
        return WpfTestHost.Invoke(async () =>
        {
            var previousConfig = ConfigService.Instance;
            var dictionaries = new List<ResourceDictionary>();
            try
            {
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }

                using var databases = new ResultDatabases();
                using var device = new DeviceAlgorithm(new SysResourceModel { Id = -2, Code = "synthetic-affinity-algorithm", Value = "{}" });
                device.ViewRegistration.IsVisible = true;
                device.ResultDatabaseEndpoint = databases.First;
                var view = device.ViewShell;
                view.Config.AutoRefreshView = false;
                view.Config.AutoSaveSideData = false;

                var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                databases.BeforeFirstRead = () =>
                {
                    readStarted.TrySetResult();
                    releaseRead.Task.WaitAsync(TimeSpan.FromSeconds(8)).GetAwaiter().GetResult();
                };
                Task pending = view.ProcessResultAsync(LocalMessage(databases.First, "original-batch"));
                try
                {
                    await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(8));
                    databases.Current = databases.Second;
                    device.ResultDatabaseEndpoint = databases.Second;
                }
                finally { releaseRead.TrySetResult(); }
                await pending.WaitAsync(TimeSpan.FromSeconds(8));

                await view.ProcessResultAsync(LocalMessage(databases.First, "late-original-batch"));
                Assert.Empty(view.ViewResults);
                Assert.Equal(1, databases.FirstReadCount);

                await view.ProcessResultAsync(LocalMessage(databases.Second, "replacement-batch"));
                var result = Assert.Single(view.ViewResults);
                Assert.Equal("replacement-master", result.ResultDesc);
                Assert.Equal("replacement-batch", result.Batch);
                Assert.Same(databases.Second, result.AlgResultMasterModel.DatabaseEndpoint);

                databases.Current = databases.First;
                Assert.True(PoiHistoryDetails.TryLoad(result, _ => throw new InvalidOperationException("The bound details must not use the legacy query.")));
                Assert.Equal("replacement-poi", Assert.IsType<PoiPointResultModel>(Assert.Single(result.ViewResults)).PoiName);
            }
            finally
            {
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                TestEnvironment.SetConfig(previousConfig);
            }
        });
    }

    private static ResultMessage LocalMessage(MySqlConnectedEndpoint endpoint, string serialNumber) => new(
        ResultRoutes.LocalFlow, ResultKinds.Algorithm, string.Empty, "RealPOI", serialNumber, "synthetic-node", 0,
        new ResultReference(MasterId, (int)ViewResultAlgType.POI_Y), attachment: null, databaseEndpoint: endpoint);

    private sealed class ResultDatabases : IDisposable
    {
        private readonly string firstPath;
        private readonly string secondPath;
        private MySqlConnectedEndpoint current = null!;
        private int firstReadCount;
        private int secondReadCount;

        internal ResultDatabases(string? mtfDeviceCode = null)
        {
            DirectoryPath = Directory.CreateTempSubdirectory("ColorVision-post-process-db-").FullName;
            firstPath = Path.Combine(DirectoryPath, "first.db");
            secondPath = Path.Combine(DirectoryPath, "second.db");
            if (mtfDeviceCode == null)
            {
                Seed(firstPath, "original");
                Seed(secondPath, "replacement");
            }
            else
            {
                SeedMtf(firstPath, "original", mtfDeviceCode);
                SeedMtf(secondPath, "replacement", mtfDeviceCode);
            }
            First = new(candidate => ReferenceEquals(candidate, Current), () =>
            {
                Interlocked.Increment(ref firstReadCount);
                BeforeFirstRead?.Invoke();
                var db = CreateDatabase(firstPath);
                if (BeforeFirstQuery is { } inspect) db.Aop.OnLogExecuting = (sql, _) => inspect(sql);
                return db;
            });
            Second = new(candidate => ReferenceEquals(candidate, Current), () =>
            {
                Interlocked.Increment(ref secondReadCount);
                return CreateDatabase(secondPath);
            });
            Current = First;
        }

        internal string DirectoryPath { get; }
        internal MySqlConnectedEndpoint First { get; }
        internal MySqlConnectedEndpoint Second { get; }
        internal MySqlConnectedEndpoint Current { get => Volatile.Read(ref current); set => Volatile.Write(ref current, value); }
        internal int FirstReadCount => Volatile.Read(ref firstReadCount);
        internal int SecondReadCount => Volatile.Read(ref secondReadCount);
        internal Action? BeforeFirstRead { get; set; }
        internal Action<string>? BeforeFirstQuery { get; set; }

        private static void Seed(string path, string marker)
        {
            using var db = CreateDatabase(path);
            db.CodeFirst.InitTables<AlgResultMasterModel>();
            db.CodeFirst.InitTables<PoiPointResultModel>();
            db.CodeFirst.InitTables<SMUResultModel>();
            var master = new AlgResultMasterModel
            {
                BatchId = BatchId, ImgFileType = ViewResultAlgType.POI_Y, Result = $"{marker}-master", CreateDate = DateTime.Now,
            };
            Assert.Equal(MasterId, db.Insertable(master).ExecuteReturnIdentity());
            db.Insertable(new PoiPointResultModel
            {
                Pid = MasterId, PoiId = 1, PoiName = $"{marker}-poi", PoiX = 1, PoiY = 2, PoiWidth = 1, PoiHeight = 1, Value = "{\"Y\":7}",
            }).ExecuteCommand();
            bool fromOriginal = marker == "original";
            db.Insertable(new SMUResultModel { BatchId = BatchId, ZIndex = -1, VResult = fromOriginal ? 7 : 27, IResult = fromOriginal ? 8 : 28, CreateDate = DateTime.Now })
                .ExecuteCommand();
        }

        private void SeedMtf(string path, string marker, string deviceCode)
        {
            using var db = CreateDatabase(path);
            db.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(AlgResultMasterModel), typeof(DetailCommonModel));
            string code = $"{marker}-owned-batch";
            int batchId = db.Insertable(new MeasureBatchModel { Name = code, Code = code }).ExecuteReturnIdentity();
            Assert.Equal(MasterId, db.Insertable(new AlgResultMasterModel
            {
                BatchId = batchId, DeviceCode = deviceCode, ImgFileType = ViewResultAlgType.MTF, version = "2.0", Result = $"{marker}-master",
                CreateDate = DateTime.UnixEpoch,
            }).ExecuteReturnIdentity());
            string detailPath = Path.Combine(DirectoryPath, $"{marker}-mtf.json");
            File.WriteAllText(detailPath, JsonConvert.SerializeObject(new MTFResult
            {
                result = [new MTFItem { name = $"{marker}-mtf", x = 1, y = 2, w = 3, h = 4, mtfValue = marker == "original" ? 7 : 27 }]
            }));
            db.Insertable(new DetailCommonModel { PId = MasterId, ResultJson = JsonConvert.SerializeObject(new ResultFile { ResultFileName = detailPath }) })
                .ExecuteCommand();
        }

        private static SqlSugarClient CreateDatabase(string path) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={path};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        });

        public void Dispose()
        {
            foreach (string path in Directory.GetFiles(DirectoryPath)) File.Delete(path);
            Directory.Delete(DirectoryPath);
        }
    }
}
