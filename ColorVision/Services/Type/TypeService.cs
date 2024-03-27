using ColorVision.Common.MVVM;
using ColorVision.Services.Dao;
using ColorVision.Services.Terminal;
using ColorVision.Utilities;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Type
{
    public enum ServiceTypes
    {
        Camera = 1,
        PG = 2,
        Spectrum = 3,
        SMU = 4,
        Sensor = 5,
        FileServer = 6,
        Algorithm = 7,
        CfwPort = 8,
        Calibration = 9,
        Motor = 10,
        Flowtime = 101,
        Flow = 12,
    }


    public enum ResourceType
    {
        Group = 1000
    }

    public class TypeService : TerminalServiceBase
    {
        public SysDictionaryModel SysDictionaryModel { get; set; }

        public ServiceTypes ServiceTypes { get =>  (ServiceTypes)SysDictionaryModel.Value; }

        public RelayCommand OpenCreateWindowCommand { get; set; }

        public RelayCommand CreateCommand { get; set; }
        public TypeService() : base()
        {

            OpenCreateWindowCommand = new RelayCommand(a =>
            {
                CreateType createType = new CreateType(this);
                createType.Owner = WindowHelpers.GetActiveWindow();
                createType.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                createType.ShowDialog();
            });
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
