using ColorVision.Engine;
using log4net;
using ColorVision.ImageEditor;

namespace ProjectARVRPro.Process
{
    public class IProcessExecutionContext
    {
        public ILog Log { get; } = LogManager.GetLogger(typeof(IProcessExecutionContext));

        public MeasureBatchModel Batch { get; set; } = null!;
        public ProjectARVRReuslt Result { get; set; } = null!;
        public ObjectiveTestResult ObjectiveTestResult { get; set; } = null!;

        public RecipeConfig RecipeConfig { get;  } = RecipeManager.GetInstance().RecipeConfig;

        public ImageView ImageView { get; set; } = null!;
    }
}
