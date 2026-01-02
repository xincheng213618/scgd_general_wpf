using log4net;

namespace ColorVision.Engine.Batch
{
    public class CheckFlowPreProcessConfig : PreProcessConfigBase
    {

    }
    public class CheckFlowPreProcess: PreProcessBase<CheckFlowPreProcessConfig>
    {
        public static readonly ILog log = LogManager.GetLogger(typeof(CheckFlowPreProcess));
        public override bool PreProcess(IPreProcessContext ctx)
        {
            
            




            return true;
        }
    }
}
