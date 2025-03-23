using ColorVision.UI;

namespace CalibrationCorrection
{
    public class CalibrationPlugin : IPluginBase
    {
        public override string Header { get; set; } = "校正工具";
        public override string Description { get; set; } = "视彩校正工具";

        public override void Execute()
        {
            new ExporCalibrationCorrection().Execute();
        }
    }
}
