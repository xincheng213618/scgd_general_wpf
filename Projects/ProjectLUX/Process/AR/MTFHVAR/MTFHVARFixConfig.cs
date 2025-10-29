using ColorVision.Common.MVVM;
using ProjectLUX.Fix;
using System.ComponentModel;

namespace ProjectLUX.Process.MTFHVAR
{
    public class MTFHVARFixConfig : ViewModelBase, IFixConfig
    {

        // 0F Center
        [Category("MTFHVAR")]
        public double MTF0F_Center_H1 { get => _MTF0F_Center_H1; set { _MTF0F_Center_H1 = value; OnPropertyChanged(); } }
        private double _MTF0F_Center_H1 = 1;

        [Category("MTFHVAR")]
        public double MTF0F_Center_V1 { get => _MTF0F_Center_V1; set { _MTF0F_Center_V1 = value; OnPropertyChanged(); } }
        private double _MTF0F_Center_V1 = 1;

        [Category("MTFHVAR")]
        public double MTF0F_Center_H2 { get => _MTF0F_Center_H2; set { _MTF0F_Center_H2 = value; OnPropertyChanged(); } }
        private double _MTF0F_Center_H2 = 1;

        [Category("MTFHVAR")]
        public double MTF0F_Center_V2 { get => _MTF0F_Center_V2; set { _MTF0F_Center_V2 = value; OnPropertyChanged(); } }
        private double _MTF0F_Center_V2 = 1;

        [Category("MTFHVAR")]
        public double MTF0F_Center_horizontal { get => _MTF0F_Center_horizontal; set { _MTF0F_Center_horizontal = value; OnPropertyChanged(); } }
        private double _MTF0F_Center_horizontal = 1;

        [Category("MTFHVAR")]
        public double MTF0F_Center_Vertical { get => _MTF0F_Center_Vertical; set { _MTF0F_Center_Vertical = value; OnPropertyChanged(); } }
        private double _MTF0F_Center_Vertical = 1;

        // 0.4F LeftUp
        [Category("MTFHVAR")]
        public double MTF0_4F_LeftUp_H1 { get => _MTF0_4F_LeftUp_H1; set { _MTF0_4F_LeftUp_H1 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftUp_H1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_LeftUp_V1 { get => _MTF0_4F_LeftUp_V1; set { _MTF0_4F_LeftUp_V1 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftUp_V1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_LeftUp_H2 { get => _MTF0_4F_LeftUp_H2; set { _MTF0_4F_LeftUp_H2 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftUp_H2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_LeftUp_V2 { get => _MTF0_4F_LeftUp_V2; set { _MTF0_4F_LeftUp_V2 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftUp_V2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_LeftUp_horizontal { get => _MTF0_4F_LeftUp_horizontal; set { _MTF0_4F_LeftUp_horizontal = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftUp_horizontal = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_LeftUp_Vertical { get => _MTF0_4F_LeftUp_Vertical; set { _MTF0_4F_LeftUp_Vertical = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftUp_Vertical = 1;

        // 0.4F RightUp
        [Category("MTFHVAR")]
        public double MTF0_4F_RightUp_H1 { get => _MTF0_4F_RightUp_H1; set { _MTF0_4F_RightUp_H1 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightUp_H1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_RightUp_V1 { get => _MTF0_4F_RightUp_V1; set { _MTF0_4F_RightUp_V1 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightUp_V1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_RightUp_H2 { get => _MTF0_4F_RightUp_H2; set { _MTF0_4F_RightUp_H2 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightUp_H2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_RightUp_V2 { get => _MTF0_4F_RightUp_V2; set { _MTF0_4F_RightUp_V2 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightUp_V2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_RightUp_horizontal { get => _MTF0_4F_RightUp_horizontal; set { _MTF0_4F_RightUp_horizontal = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightUp_horizontal = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_RightUp_Vertical { get => _MTF0_4F_RightUp_Vertical; set { _MTF0_4F_RightUp_Vertical = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightUp_Vertical = 1;

        // 0.4F RightDown
        [Category("MTFHVAR")]
        public double MTF0_4F_RightDown_H1 { get => _MTF0_4F_RightDown_H1; set { _MTF0_4F_RightDown_H1 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightDown_H1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_RightDown_V1 { get => _MTF0_4F_RightDown_V1; set { _MTF0_4F_RightDown_V1 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightDown_V1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_RightDown_H2 { get => _MTF0_4F_RightDown_H2; set { _MTF0_4F_RightDown_H2 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightDown_H2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_RightDown_V2 { get => _MTF0_4F_RightDown_V2; set { _MTF0_4F_RightDown_V2 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightDown_V2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_RightDown_horizontal { get => _MTF0_4F_RightDown_horizontal; set { _MTF0_4F_RightDown_horizontal = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightDown_horizontal = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_RightDown_Vertical { get => _MTF0_4F_RightDown_Vertical; set { _MTF0_4F_RightDown_Vertical = value; OnPropertyChanged(); } }
        private double _MTF0_4F_RightDown_Vertical = 1;

        // 0.4F LeftDown
        [Category("MTFHVAR")]
        public double MTF0_4F_LeftDown_H1 { get => _MTF0_4F_LeftDown_H1; set { _MTF0_4F_LeftDown_H1 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftDown_H1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_LeftDown_V1 { get => _MTF0_4F_LeftDown_V1; set { _MTF0_4F_LeftDown_V1 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftDown_V1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_LeftDown_H2 { get => _MTF0_4F_LeftDown_H2; set { _MTF0_4F_LeftDown_H2 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftDown_H2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_LeftDown_V2 { get => _MTF0_4F_LeftDown_V2; set { _MTF0_4F_LeftDown_V2 = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftDown_V2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_LeftDown_horizontal { get => _MTF0_4F_LeftDown_horizontal; set { _MTF0_4F_LeftDown_horizontal = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftDown_horizontal = 1;

        [Category("MTFHVAR")]
        public double MTF0_4F_LeftDown_Vertical { get => _MTF0_4F_LeftDown_Vertical; set { _MTF0_4F_LeftDown_Vertical = value; OnPropertyChanged(); } }
        private double _MTF0_4F_LeftDown_Vertical = 1;

        // 0.8F LeftUp
        [Category("MTFHVAR")]
        public double MTF0_8F_LeftUp_H1 { get => _MTF0_8F_LeftUp_H1; set { _MTF0_8F_LeftUp_H1 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftUp_H1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_LeftUp_V1 { get => _MTF0_8F_LeftUp_V1; set { _MTF0_8F_LeftUp_V1 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftUp_V1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_LeftUp_H2 { get => _MTF0_8F_LeftUp_H2; set { _MTF0_8F_LeftUp_H2 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftUp_H2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_LeftUp_V2 { get => _MTF0_8F_LeftUp_V2; set { _MTF0_8F_LeftUp_V2 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftUp_V2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_LeftUp_horizontal { get => _MTF0_8F_LeftUp_horizontal; set { _MTF0_8F_LeftUp_horizontal = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftUp_horizontal = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_LeftUp_Vertical { get => _MTF0_8F_LeftUp_Vertical; set { _MTF0_8F_LeftUp_Vertical = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftUp_Vertical = 1;

        // 0.8F RightUp
        [Category("MTFHVAR")]
        public double MTF0_8F_RightUp_H1 { get => _MTF0_8F_RightUp_H1; set { _MTF0_8F_RightUp_H1 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightUp_H1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_RightUp_V1 { get => _MTF0_8F_RightUp_V1; set { _MTF0_8F_RightUp_V1 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightUp_V1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_RightUp_H2 { get => _MTF0_8F_RightUp_H2; set { _MTF0_8F_RightUp_H2 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightUp_H2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_RightUp_V2 { get => _MTF0_8F_RightUp_V2; set { _MTF0_8F_RightUp_V2 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightUp_V2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_RightUp_horizontal { get => _MTF0_8F_RightUp_horizontal; set { _MTF0_8F_RightUp_horizontal = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightUp_horizontal = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_RightUp_Vertical { get => _MTF0_8F_RightUp_Vertical; set { _MTF0_8F_RightUp_Vertical = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightUp_Vertical = 1;

        // 0.8F RightDown
        [Category("MTFHVAR")]
        public double MTF0_8F_RightDown_H1 { get => _MTF0_8F_RightDown_H1; set { _MTF0_8F_RightDown_H1 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightDown_H1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_RightDown_V1 { get => _MTF0_8F_RightDown_V1; set { _MTF0_8F_RightDown_V1 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightDown_V1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_RightDown_H2 { get => _MTF0_8F_RightDown_H2; set { _MTF0_8F_RightDown_H2 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightDown_H2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_RightDown_V2 { get => _MTF0_8F_RightDown_V2; set { _MTF0_8F_RightDown_V2 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightDown_V2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_RightDown_horizontal { get => _MTF0_8F_RightDown_horizontal; set { _MTF0_8F_RightDown_horizontal = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightDown_horizontal = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_RightDown_Vertical { get => _MTF0_8F_RightDown_Vertical; set { _MTF0_8F_RightDown_Vertical = value; OnPropertyChanged(); } }
        private double _MTF0_8F_RightDown_Vertical = 1;

        // 0.8F LeftDown
        [Category("MTFHVAR")]
        public double MTF0_8F_LeftDown_H1 { get => _MTF0_8F_LeftDown_H1; set { _MTF0_8F_LeftDown_H1 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftDown_H1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_LeftDown_V1 { get => _MTF0_8F_LeftDown_V1; set { _MTF0_8F_LeftDown_V1 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftDown_V1 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_LeftDown_H2 { get => _MTF0_8F_LeftDown_H2; set { _MTF0_8F_LeftDown_H2 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftDown_H2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_LeftDown_V2 { get => _MTF0_8F_LeftDown_V2; set { _MTF0_8F_LeftDown_V2 = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftDown_V2 = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_LeftDown_horizontal { get => _MTF0_8F_LeftDown_horizontal; set { _MTF0_8F_LeftDown_horizontal = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftDown_horizontal = 1;

        [Category("MTFHVAR")]
        public double MTF0_8F_LeftDown_Vertical { get => _MTF0_8F_LeftDown_Vertical; set { _MTF0_8F_LeftDown_Vertical = value; OnPropertyChanged(); } }
        private double _MTF0_8F_LeftDown_Vertical = 1;
    }
}