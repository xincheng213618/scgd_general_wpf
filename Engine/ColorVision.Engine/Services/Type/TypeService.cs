using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.SysDictionary;
using ColorVision.Engine.Services.Terminal;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Types
{
    public enum ServiceTypes
    {
        None = 0,
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
        Flow = 12,
        ThirdPartyAlgorithms = 13,
        ThirdPartyAlgorithms32 = 15,
        FlowTemp = 21,
        [Description("暗噪声")]
        DarkNoise = 31,
        [Description("缺陷点")]
        DefectPoint = 32,
        [Description("DSNU")]
        DSNU = 33,
        [Description("均匀场")]
        Uniformity = 34,
        [Description("畸变")]
        Distortion = 35,
        [Description("色偏")]
        ColorShift = 36,
        [Description("亮度")]
        Luminance = 37,
        [Description("单色")]
        LumOneColor = 38,
        [Description("四色")]
        LumFourColor = 39,
        [Description("多色")]
        LumMultiColor = 40,
        Group = 1000,
        SpCalibration =201,
        PhyCamera =101,
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
                CreateType createType = new(this);
                createType.Owner = Application.Current.GetActiveWindow();
                createType.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                createType.ShowDialog();
            });

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Create ,Command = OpenCreateWindowCommand });
        }



        public List<string> ServicesCodes
        { 
            get
            {
                List<string> codes = new();
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
