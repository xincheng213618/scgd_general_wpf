using ColorVision.Engine;
using log4net;
using ColorVision.ImageEditor;

namespace ProjectARVRPro.Process
{
    public class IProcessExecutionContext
    {
        public MeasureBatchModel Batch { get; set; }
        public ProjectARVRReuslt Result { get; set; }
        public ObjectiveTestResult ObjectiveTestResult { get; set; }
        public RecipeConfig RecipeConfig { get; set; }

        public ImageView ImageView { get; set; }

        public ILog Logger { get; set; }
    }
}
