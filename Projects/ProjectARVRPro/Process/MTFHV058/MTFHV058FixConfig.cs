using ColorVision.Common.MVVM;
using ProjectARVRPro.Fix;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTFHV058
{
    public class MTFHV058FixConfig : ViewModelBase, IFixConfig
    {
        [Category("MTFHV058")]
        public double MTF_HV_H_Center_0F { get => _MTF_HV_H_Center_0F; set { _MTF_HV_H_Center_0F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_Center_0F = 1;
        [Category("MTFHV058")]
        public double MTF_HV_V_Center_0F { get => _MTF_HV_V_Center_0F; set { _MTF_HV_V_Center_0F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_Center_0F = 1;

        [Category("MTFHV058")]
        public double MTF_HV_H_LeftUp_0_5F { get => _MTF_HV_H_LeftUp_0_5F; set { _MTF_HV_H_LeftUp_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_5F = 1;
        [Category("MTFHV058")]
        public double MTF_HV_V_LeftUp_0_5F { get => _MTF_HV_V_LeftUp_0_5F; set { _MTF_HV_V_LeftUp_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_5F = 1;

        [Category("MTFHV058")]
        public double MTF_HV_H_RightUp_0_5F { get => _MTF_HV_H_RightUp_0_5F; set { _MTF_HV_H_RightUp_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_5F = 1;
        [Category("MTFHV058")]
        public double MTF_HV_V_RightUp_0_5F { get => _MTF_HV_V_RightUp_0_5F; set { _MTF_HV_V_RightUp_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_5F = 1;

        [Category("MTFHV058")]
        public double MTF_HV_H_RightDown_0_5F { get => _MTF_HV_H_RightDown_0_5F; set { _MTF_HV_H_RightDown_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_5F = 1;
        [Category("MTFHV058")]
        public double MTF_HV_V_RightDown_0_5F { get => _MTF_HV_V_RightDown_0_5F; set { _MTF_HV_V_RightDown_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_5F = 1;

        [Category("MTFHV058")]
        public double MTF_HV_H_LeftDown_0_5F { get => _MTF_HV_H_LeftDown_0_5F; set { _MTF_HV_H_LeftDown_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_5F = 1;
        [Category("MTFHV058")]
        public double MTF_HV_V_LeftDown_0_5F { get => _MTF_HV_V_LeftDown_0_5F; set { _MTF_HV_V_LeftDown_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_5F = 1;


        [Category("MTFHV058")]
        public double MTF_HV_H_LeftUp_0_8F { get => _MTF_HV_H_LeftUp_0_8F; set { _MTF_HV_H_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_8F = 1;
        [Category("MTFHV058")]
        public double MTF_HV_V_LeftUp_0_8F { get => _MTF_HV_V_LeftUp_0_8F; set { _MTF_HV_V_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_8F = 1;

        [Category("MTFHV058")]
        public double MTF_HV_H_RightUp_0_8F { get => _MTF_HV_H_RightUp_0_8F; set { _MTF_HV_H_RightUp_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_8F = 1;
        [Category("MTFHV058")]
        public double MTF_HV_V_RightUp_0_8F { get => _MTF_HV_V_RightUp_0_8F; set { _MTF_HV_V_RightUp_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_8F = 1;

        [Category("MTFHV058")]
        public double MTF_HV_H_RightDown_0_8F { get => _MTF_HV_H_RightDown_0_8F; set { _MTF_HV_H_RightDown_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_8F = 1;
        [Category("MTFHV058")]
        public double MTF_HV_V_RightDown_0_8F { get => _MTF_HV_V_RightDown_0_8F; set { _MTF_HV_V_RightDown_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_8F = 1;

        [Category("MTFHV058")]
        public double MTF_HV_H_LeftDown_0_8F { get => _MTF_HV_H_LeftDown_0_8F; set { _MTF_HV_H_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_8F = 1;
        [Category("MTFHV058")]
        public double MTF_HV_V_LeftDown_0_8F { get => _MTF_HV_V_LeftDown_0_8F; set { _MTF_HV_V_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_8F = 1;
    }

}
