using ColorVision.Common.MVVM;
using ProjectARVRPro.Fix;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTFH
{
    /// <summary>
    /// 旧版MTFH修正系数
    /// </summary>
    public class MTFHFixConfig : ViewModelBase, IFixConfig
    {
        [Category("MTFH(旧版)")]
        public double MTF_H_Center_0F { get => _MTF_H_Center_0F; set { _MTF_H_Center_0F = value; OnPropertyChanged(); } }
        private double _MTF_H_Center_0F = 1;

        [Category("MTFH(旧版)")]
        public double MTF_H_LeftUp_0_5F { get => _MTF_H_LeftUp_0_5F; set { _MTF_H_LeftUp_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_H_LeftUp_0_5F = 1;

        [Category("MTFH(旧版)")]
        public double MTF_H_RightUp_0_5F { get => _MTF_H_RightUp_0_5F; set { _MTF_H_RightUp_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_H_RightUp_0_5F = 1;

        [Category("MTFH(旧版)")]
        public double MTF_H_LeftDown_0_5F { get => _MTF_H_LeftDown_0_5F; set { _MTF_H_LeftDown_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_H_LeftDown_0_5F = 1;

        [Category("MTFH(旧版)")]
        public double MTF_H_RightDown_0_5F { get => _MTF_H_RightDown_0_5F; set { _MTF_H_RightDown_0_5F = value; OnPropertyChanged(); } }
        private double _MTF_H_RightDown_0_5F = 1;

        [Category("MTFH(旧版)")]
        public double MTF_H_LeftUp_0_8F { get => _MTF_H_LeftUp_0_8F; set { _MTF_H_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_H_LeftUp_0_8F = 1;

        [Category("MTFH(旧版)")]
        public double MTF_H_RightUp_0_8F { get => _MTF_H_RightUp_0_8F; set { _MTF_H_RightUp_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_H_RightUp_0_8F = 1;

        [Category("MTFH(旧版)")]
        public double MTF_H_LeftDown_0_8F { get => _MTF_H_LeftDown_0_8F; set { _MTF_H_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_H_LeftDown_0_8F = 1;

        [Category("MTFH(旧版)")]
        public double MTF_H_RightDown_0_8F { get => _MTF_H_RightDown_0_8F; set { _MTF_H_RightDown_0_8F = value; OnPropertyChanged(); } }
        private double _MTF_H_RightDown_0_8F = 1;
    }
}
