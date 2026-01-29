using log4net;
using System.Threading.Tasks;

namespace ColorVision.Engine.Batch.PreProcess
{
    public class CheckFlowPreProcessConfig : PreProcessConfigBase
    {

    }
    public class CheckFlowPreProcess: PreProcessBase<CheckFlowPreProcessConfig>
    {
        public static readonly ILog log = LogManager.GetLogger(typeof(CheckFlowPreProcess));
        public override Task<bool> PreProcess(IPreProcessContext ctx)
        {
            
            




            return Task.FromResult(true);
        }
    }
}
