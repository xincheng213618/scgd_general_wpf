using ColorVision.Engine;
using log4net;
using ColorVision.ImageEditor;
using ColorVision.Database;

namespace ProjectARVRPro.Process
{
    public class IProcessExecutionContext
    {
        private readonly Func<int, List<MeasureResultImgModel>> _measureResultLoader;
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

            _measureResults = _measureResultLoader(Batch.Id) ?? [];
            _measureResultsLoaded = true;
            return _measureResults;
        }

        internal bool TryPopulateResultImageDimensions()
        {
            return _measureResultsLoaded
                && _measureResults != null
                && ResultImageDimensions.TryPopulate(Result, _ => _measureResults);
        }
    }
}
