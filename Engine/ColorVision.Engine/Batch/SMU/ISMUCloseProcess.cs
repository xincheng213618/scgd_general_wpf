using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.SMU;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Batch.SMU
{
    [BatchProcess("源表关闭", "关闭源表")]
    public class ISMUCloseProcess : IBatchProcess
    {
        public bool Process(IBatchContext ctx)
        {
            if (ctx?.Batch == null) return false;


            foreach (var item in ServiceManager.GetInstance().DeviceServices.OfType<DeviceSMU>())
            {
                Task.Run(async () => 
                {
                    item.DService.CloseOutput();
                    item.Config.V = null;
                    item.Config.I = null;
                    await Task.Delay(500);
                    item.DService.CloseOutput();
                });

            }
            return true;

        }
    }
}
