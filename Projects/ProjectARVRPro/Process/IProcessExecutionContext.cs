using ColorVision.Engine;
using log4net;
using ColorVision.ImageEditor;
using ColorVision.Database;
using SqlSugar;

namespace ProjectARVRPro.Process
{
    public class IProcessExecutionContext
    {
        private readonly Func<int, List<MeasureResultImgModel>>? _measureResultLoader;
        private readonly Func<SqlSugarClient>? _openBatchDatabase;
        private readonly bool _scopedReads;
        private List<MeasureResultImgModel>? _measureResults;
        private bool _measureResultsLoaded;

        public IProcessExecutionContext()
            : this(batchId => MeasureImgResultDao.Instance.GetAllByBatchId(batchId))
        {
        }

        internal IProcessExecutionContext(Func<int, List<MeasureResultImgModel>> measureResultLoader)
        {
            _measureResultLoader = measureResultLoader ?? throw new ArgumentNullException(nameof(measureResultLoader));
        }

        internal IProcessExecutionContext(Func<SqlSugarClient>? openBatchDatabase)
        {
            _scopedReads = true;
            _openBatchDatabase = openBatchDatabase;
        }

        public ILog Log { get; } = LogManager.GetLogger(typeof(IProcessExecutionContext));

        public MeasureBatchModel Batch { get; set; } = null!;
        public ProjectARVRReuslt Result { get; set; } = null!;
        public ObjectiveTestResult ObjectiveTestResult { get; set; } = null!;

        public RecipeConfig RecipeConfig { get; } = ProcessManager.GetInstance().RecipeConfig;

        public ImageView ImageView { get; set; } = null!;

        internal List<MeasureResultImgModel> GetMeasureResults()
        {
            if (_measureResultsLoaded)
                return _measureResults!;

            if (_scopedReads)
            {
                int batchId = Batch?.Id ?? 0;
                _measureResults = [];
                if (batchId > 0)
                {
                    using var db = OpenBatchDatabase();
                    _measureResults = db.Queryable<MeasureResultImgModel>().Where(row => row.BatchId == batchId).ToList();
                }
            }
            else
            {
                _measureResults = _measureResultLoader!(Batch.Id) ?? [];
            }
            _measureResultsLoaded = true;
            return _measureResults;
        }

        internal List<AlgResultMasterModel> ReadMasterResults()
        {
            int batchId = Batch?.Id ?? 0;
            if (batchId <= 0) return [];
            if (!_scopedReads) return AlgResultMasterDao.Instance.GetAllByBatchId(batchId);

            using var db = OpenBatchDatabase();
            return db.Queryable<AlgResultMasterModel>().Where(row => row.BatchId == batchId).ToList();
        }

        internal List<T> ReadDetails<T>(BaseTableDao<T> dao, int masterId) where T : class, IEntity, new()
        {
            if (!_scopedReads) return dao.GetAllByPid(masterId);

            using var db = OpenBatchDatabase();
            return db.Queryable<T>().Where("pid = @masterId", new { masterId }).ToList();
        }

        internal List<T>? TryReadDetails<T>(BaseTableDao<T> dao, int masterId) where T : class, IEntity, new()
        {
            if (!_scopedReads) return dao.TryGetAllByPid(masterId, out var details) ? details : null;

            try
            {
                return ReadDetails(dao, masterId);
            }
            catch (Exception error)
            {
                Log.Warn($"读取流程结果明细失败：masterId={masterId}, table={typeof(T).Name}", error);
                return null;
            }
        }

        internal Func<SqlSugarClient>? ScopedDatabaseReader => _scopedReads ? OpenBatchDatabase : null;

        private SqlSugarClient OpenBatchDatabase() =>
            (_openBatchDatabase ?? throw new InvalidOperationException("缺少本轮流程的数据库连接。"))();

        internal bool TryPopulateResultImageDimensions()
        {
            return _measureResultsLoaded
                && _measureResults != null
                && ResultImageDimensions.TryPopulate(Result, _ => _measureResults);
        }
    }
}
