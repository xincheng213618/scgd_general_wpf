using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalibrationCorrection
{
    public class CalibrationPlugin : IPluginBase
    {
        public override string Header { get; set; } = "校正工具";
        public override string? UpdateUrl { get; set; }
        public override string Description { get; set; } = "视彩校正工具";

        public override void Execute()
        {
            new ExporCalibrationCorrection().Execute();
        }
    }
}
