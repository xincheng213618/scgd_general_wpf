using ColorVision.Services.Dao;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision.Services
{
    public enum ServiceTypes
    {
        camera = 1,
        pg = 2,
        Spectum = 3,
        SMU = 4,
        Sensor = 5,
        FileServer = 6,
        Algorithm = 7,
        CfwPort = 8,
        Calibration = 9,
        Motor = 10,
        Flowtime = 101
    }

    public class TypeService : TerminalServiceBase
    {
        public SysDictionaryModel SysDictionaryModel { get; set; }

        public ServiceTypes ServiceTypes { get =>  (ServiceTypes)SysDictionaryModel.Value; }

        public TypeService() : base()
        {

        }

        public List<string> ServicesCodes
        { 
            get
            {
                List<string> codes = new List<string>();
                foreach (var item in VisualChildren)
                {
                    if (item is TerminalService serviceTerminal)
                    {
                        if (!string.IsNullOrWhiteSpace(serviceTerminal.SysResourceModel.Code))
                            codes.Add(serviceTerminal.SysResourceModel.Code);
                    }
                }
                return codes;
            }
        }

        public override UserControl GenDeviceControl() => new TypeServiceControl(this);
    }
}
