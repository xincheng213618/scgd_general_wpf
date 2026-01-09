using ColorVision.Common.MVVM;
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
        FilterWheel = 8,
        Calibration = 9,
        Motor = 10,
        FocusRing =11,
        Flow = 12,
        Archived =13,
        ThirdPartyAlgorithms = 14,
        ThirdPartyAlgorithms32 = 15,
        PowerControl =16,
        LightingControl =17,
        FlowTemp = 21,
        [Description("DarkNoise")]
        DarkNoise = 31,
        [Description("DefectPoint")]
        DefectPoint = 32,
        [Description("DSNU")]
        DSNU = 33,
        [Description("Uniformity")]
        Uniformity = 34,
        [Description("Distortion")]
        Distortion = 35,
        [Description("ColorShift")]
        ColorShift = 36,
        [Description("Brightness")]
        Luminance = 37,
        [Description("OneColor")]
        LumOneColor = 38,
        [Description("FourColor")]
        LumFourColor = 39,
        [Description("MultiColor")]
        LumMultiColor = 40,
        [Description("LineArity")]
        LineArity = 41,
        [Description("ColorDiff")]
        ColorDiff = 42,
        Group = 1000,
        SpCalibration =201,
        PhyCamera =101,
        PhySpectrums =103
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
                if (MessageBox.Show("如果非必要情况，请勿创建新的服务", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    CreateType createType = new CreateType(this);
                    createType.Owner = Application.Current.GetActiveWindow();
                    createType.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    createType.ShowDialog();
                }
            });

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Create ,Command = OpenCreateWindowCommand });
        }

        public override void Delete()
        {
            if (MessageBox.Show("如果非必要情况，请勿删除服务","ColorVision",MessageBoxButton.YesNo)==MessageBoxResult.Yes)
            {
                base.Delete();
            }
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
