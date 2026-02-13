using ColorVision.Common.MVVM;
using ProjectLUX.Fix;
using System.ComponentModel;

namespace ProjectLUX.Process.VR.MTFH
{
    public class VRMTFHFixConfig : ViewModelBase, IFixConfig
    {

        [Category("MTF")]
        public double Region_A_Min { get => _Region_A_Min; set { _Region_A_Min = value; OnPropertyChanged(); } }
        private double _Region_A_Min = 1;

        [Category("MTF")]
        public double Region_A_Max { get => _Region_A_Max; set { _Region_A_Max = value; OnPropertyChanged(); } }
        private double _Region_A_Max = 1;

        [Category("MTF")]
        public double Region_A_Average { get => _Region_A_Average; set { _Region_A_Average = value; OnPropertyChanged(); } }
        private double _Region_A_Average = 1;


        [Category("MTF")]
        public double Region_B_Min { get => _Region_B_Min; set { _Region_B_Min = value; OnPropertyChanged(); } }
        private double _Region_B_Min = 1;

        [Category("MTF")]
        public double Region_B_Max { get => _Region_B_Max; set { _Region_B_Max = value; OnPropertyChanged(); } }
        private double _Region_B_Max = 1;

        [Category("MTF")]
        public double Region_B_Average { get => _Region_B_Average; set { _Region_B_Average = value; OnPropertyChanged(); } }
        private double _Region_B_Average = 1;


        [Category("MTF")]
        public double Region_C_Min { get => _Region_C_Min; set { _Region_C_Min = value; OnPropertyChanged(); } }
        private double _Region_C_Min = 1;

        [Category("MTF")]
        public double Region_C_Max { get => _Region_C_Max; set { _Region_C_Max = value; OnPropertyChanged(); } }
        private double _Region_C_Max = 1;

        [Category("MTF")]
        public double Region_C_Average { get => _Region_C_Average; set { _Region_C_Average = value; OnPropertyChanged(); } }
        private double _Region_C_Average = 1;


        [Category("MTF")]
        public double Region_D_Min { get => _Region_D_Min; set { _Region_D_Min = value; OnPropertyChanged(); } }
        private double _Region_D_Min = 1;

        [Category("MTF")]
        public double Region_D_Max { get => _Region_D_Max; set { _Region_D_Max = value; OnPropertyChanged(); } }
        private double _Region_D_Max = 1;

        [Category("MTF")]
        public double Region_D_Average { get => _Region_D_Average; set { _Region_D_Average = value; OnPropertyChanged(); } }
        private double _Region_D_Average = 1;
    }

}