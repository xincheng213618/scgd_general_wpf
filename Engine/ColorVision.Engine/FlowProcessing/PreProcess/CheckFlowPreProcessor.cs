using log4net;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing.PreProcess
{
    public class CheckFlowPreProcessorConfig : PreProcessConfigBase
    {

    }
    public class CheckFlowPreProcessor: PreProcessorBase<CheckFlowPreProcessorConfig>
    {
        public static readonly ILog log = LogManager.GetLogger(typeof(CheckFlowPreProcessor));
        public override Task<bool> PreProcess(PreProcessContext ctx)
        {
            
            




            return Task.FromResult(true);
        }
    }
}
