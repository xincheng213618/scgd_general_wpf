using ColorVision.Common.MVVM;
using ProjectLUX.Fix;
using System.ComponentModel;

namespace ProjectLUX.Process.MTFHV
{
    public class MTFHVFixConfig : ViewModelBase, IFixConfig
    {
        [Category("MTFHV")]
        public double MTF_HV_H_Center_0F { get => _MTF_HV_H_Center_0F; set { _MTF_HV_H_Center_0F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_Center_0F = 1;

        [Category("MTFHV")]
        public double MTF_HV_H_LeftUp_0_4F { get => _MTF_HV_H_LeftUp_0_4F; set { _MTF_HV_H_LeftUp_0_4F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_4F = 1;
        [Category("MTFHV")]
        public double MTF_HV_H_RightUp_0_4F { get => _MTF_HV_H_RightUp_0_4F; set { _MTF_HV_H_RightUp_0_4F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_4F = 1;
        [Category("MTFHV")]
        public double MTF_HV_H_RightDown_0_4F { get => _MTF_HV_H_RightDown_0_4F; set { _MTF_HV_H_RightDown_0_4F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_4F = 1;
        [Category("MTFHV")]
        public double MTF_HV_H_LeftDown_0_4F { get => _MTF_HV_H_LeftDown_0_4F; set { _MTF_HV_H_LeftDown_0_4F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_4F = 1;
        [Category("MTFHV")]
        public double MTF_HV_H_LeftUp_0_8F { get => _MTF_HV_H_LeftUp_0_8F; set { _MTF_HV_H_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_8F = 1;
        [Category("MTFHV")]
        public double MTF_HV_H_RightUp_0_8F { get => _MTF_HV_H_RightUp_0_8F; set { _MTF_HV_H_RightUp_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_8F = 1;
        [Category("MTFHV")]
        public double MTF_HV_H_RightDown_0_8F { get => _MTF_HV_H_RightDown_0_8F; set { _MTF_HV_H_RightDown_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_8F = 1;
        [Category("MTFHV")]
        public double MTF_HV_H_LeftDown_0_8F { get => _MTF_HV_H_LeftDown_0_8F; set { _MTF_HV_H_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_8F = 1;
        [Category("MTFHV")]
        public double MTF_HV_V_Center_0F { get => _MTF_HV_V_Center_0F; set { _MTF_HV_V_Center_0F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_Center_0F = 1;
        [Category("MTFHV")]
        public double MTF_HV_V_LeftUp_0_4F { get => _MTF_HV_V_LeftUp_0_4F; set { _MTF_HV_V_LeftUp_0_4F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_4F = 1;
        [Category("MTFHV")]
        public double MTF_HV_V_RightUp_0_4F { get => _MTF_HV_V_RightUp_0_4F; set { _MTF_HV_V_RightUp_0_4F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_4F = 1;
        [Category("MTFHV")]
        public double MTF_HV_V_RightDown_0_4F { get => _MTF_HV_V_RightDown_0_4F; set { _MTF_HV_V_RightDown_0_4F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_4F = 1;
        [Category("MTFHV")]
        public double MTF_HV_V_LeftDown_0_4F { get => _MTF_HV_V_LeftDown_0_4F; set { _MTF_HV_V_LeftDown_0_4F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_4F = 1;
        [Category("MTFHV")]
        public double MTF_HV_V_LeftUp_0_8F { get => _MTF_HV_V_LeftUp_0_8F; set { _MTF_HV_V_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_8F = 1;
        [Category("MTFHV")]
        public double MTF_HV_V_RightUp_0_8F { get => _MTF_HV_V_RightUp_0_8F; set { _MTF_HV_V_RightUp_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_8F = 1;
        [Category("MTFHV")]
        public double MTF_HV_V_RightDown_0_8F { get => _MTF_HV_V_RightDown_0_8F; set { _MTF_HV_V_RightDown_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_8F = 1;

        [Category("MTFHV")]
        public double MTF_HV_V_LeftDown_0_8F { get => _MTF_HV_V_LeftDown_0_8F; set { _MTF_HV_V_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_8F = 1;
    }

}