#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process.MTFHV;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectARVRPro.Process.MTFHV
{
    public class MTFHVRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("MTF_HV")]
        public double MTF_HV_H_Center_0FMin { get => _MTF_HV_H_Center_0FMin; set { _MTF_HV_H_Center_0FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_Center_0FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_H_Center_0FMax { get => _MTF_HV_H_Center_0FMax; set { _MTF_HV_H_Center_0FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_Center_0FMax;
        [Category("MTF_HV")]
        public double MTF_HV_H_LeftUp_0_4FMin { get => _MTF_HV_H_LeftUp_0_4FMin; set { _MTF_HV_H_LeftUp_0_4FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_4FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_H_LeftUp_0_4FMax { get => _MTF_HV_H_LeftUp_0_4FMax; set { _MTF_HV_H_LeftUp_0_4FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_4FMax;
        [Category("MTF_HV")]
        public double MTF_HV_H_RightUp_0_4FMin { get => _MTF_HV_H_RightUp_0_4FMin; set { _MTF_HV_H_RightUp_0_4FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_4FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_H_RightUp_0_4FMax { get => _MTF_HV_H_RightUp_0_4FMax; set { _MTF_HV_H_RightUp_0_4FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_4FMax;
        [Category("MTF_HV")]
        public double MTF_HV_H_RightDown_0_4FMin { get => _MTF_HV_H_RightDown_0_4FMin; set { _MTF_HV_H_RightDown_0_4FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_4FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_H_RightDown_0_4FMax { get => _MTF_HV_H_RightDown_0_4FMax; set { _MTF_HV_H_RightDown_0_4FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_4FMax;
        [Category("MTF_HV")]
        public double MTF_HV_H_LeftDown_0_4FMin { get => _MTF_HV_H_LeftDown_0_4FMin; set { _MTF_HV_H_LeftDown_0_4FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_4FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_H_LeftDown_0_4FMax { get => _MTF_HV_H_LeftDown_0_4FMax; set { _MTF_HV_H_LeftDown_0_4FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_4FMax;
        [Category("MTF_HV")]
        public double MTF_HV_H_LeftUp_0_8FMin { get => _MTF_HV_H_LeftUp_0_8FMin; set { _MTF_HV_H_LeftUp_0_8FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_8FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_H_LeftUp_0_8FMax { get => _MTF_HV_H_LeftUp_0_8FMax; set { _MTF_HV_H_LeftUp_0_8FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_8FMax;
        [Category("MTF_HV")]
        public double MTF_HV_H_RightUp_0_8FMin { get => _MTF_HV_H_RightUp_0_8FMin; set { _MTF_HV_H_RightUp_0_8FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_8FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_H_RightUp_0_8FMax { get => _MTF_HV_H_RightUp_0_8FMax; set { _MTF_HV_H_RightUp_0_8FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_8FMax;
        [Category("MTF_HV")]
        public double MTF_HV_H_RightDown_0_8FMin { get => _MTF_HV_H_RightDown_0_8FMin; set { _MTF_HV_H_RightDown_0_8FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_8FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_H_RightDown_0_8FMax { get => _MTF_HV_H_RightDown_0_8FMax; set { _MTF_HV_H_RightDown_0_8FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_8FMax;
        [Category("MTF_HV")]
        public double MTF_HV_H_LeftDown_0_8FMin { get => _MTF_HV_H_LeftDown_0_8FMin; set { _MTF_HV_H_LeftDown_0_8FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_8FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_H_LeftDown_0_8FMax { get => _MTF_HV_H_LeftDown_0_8FMax; set { _MTF_HV_H_LeftDown_0_8FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_8FMax;
        [Category("MTF_HV")]
        public double MTF_HV_V_Center_0FMin { get => _MTF_HV_V_Center_0FMin; set { _MTF_HV_V_Center_0FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_Center_0FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_V_Center_0FMax { get => _MTF_HV_V_Center_0FMax; set { _MTF_HV_V_Center_0FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_Center_0FMax;
        [Category("MTF_HV")]
        public double MTF_HV_V_LeftUp_0_4FMin { get => _MTF_HV_V_LeftUp_0_4FMin; set { _MTF_HV_V_LeftUp_0_4FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_4FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_V_LeftUp_0_4FMax { get => _MTF_HV_V_LeftUp_0_4FMax; set { _MTF_HV_V_LeftUp_0_4FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_4FMax;
        [Category("MTF_HV")]
        public double MTF_HV_V_RightUp_0_4FMin { get => _MTF_HV_V_RightUp_0_4FMin; set { _MTF_HV_V_RightUp_0_4FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_4FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_V_RightUp_0_4FMax { get => _MTF_HV_V_RightUp_0_4FMax; set { _MTF_HV_V_RightUp_0_4FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_4FMax;
        [Category("MTF_HV")]
        public double MTF_HV_V_RightDown_0_4FMin { get => _MTF_HV_V_RightDown_0_4FMin; set { _MTF_HV_V_RightDown_0_4FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_4FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_V_RightDown_0_4FMax { get => _MTF_HV_V_RightDown_0_4FMax; set { _MTF_HV_V_RightDown_0_4FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_4FMax;
        [Category("MTF_HV")]
        public double MTF_HV_V_LeftDown_0_4FMin { get => _MTF_HV_V_LeftDown_0_4FMin; set { _MTF_HV_V_LeftDown_0_4FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_4FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_V_LeftDown_0_4FMax { get => _MTF_HV_V_LeftDown_0_4FMax; set { _MTF_HV_V_LeftDown_0_4FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_4FMax;
        [Category("MTF_HV")]
        public double MTF_HV_V_LeftUp_0_8FMin { get => _MTF_HV_V_LeftUp_0_8FMin; set { _MTF_HV_V_LeftUp_0_8FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_8FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_V_LeftUp_0_8FMax { get => _MTF_HV_V_LeftUp_0_8FMax; set { _MTF_HV_V_LeftUp_0_8FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_8FMax;
        [Category("MTF_HV")]
        public double MTF_HV_V_RightUp_0_8FMin { get => _MTF_HV_V_RightUp_0_8FMin; set { _MTF_HV_V_RightUp_0_8FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_8FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_V_RightUp_0_8FMax { get => _MTF_HV_V_RightUp_0_8FMax; set { _MTF_HV_V_RightUp_0_8FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_8FMax;
        [Category("MTF_HV")]
        public double MTF_HV_V_RightDown_0_8FMin { get => _MTF_HV_V_RightDown_0_8FMin; set { _MTF_HV_V_RightDown_0_8FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_8FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_V_RightDown_0_8FMax { get => _MTF_HV_V_RightDown_0_8FMax; set { _MTF_HV_V_RightDown_0_8FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_8FMax;
        [Category("MTF_HV")]
        public double MTF_HV_V_LeftDown_0_8FMin { get => _MTF_HV_V_LeftDown_0_8FMin; set { _MTF_HV_V_LeftDown_0_8FMin = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_8FMin = 0.5;
        [Category("MTF_HV")]
        public double MTF_HV_V_LeftDown_0_8FMax { get => _MTF_HV_V_LeftDown_0_8FMax; set { _MTF_HV_V_LeftDown_0_8FMax = value; OnPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_8FMax;
    }
}