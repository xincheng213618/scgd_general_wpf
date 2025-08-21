#pragma warning disable

using ColorVision.Common.MVVM;
using Microsoft.VisualBasic;
using System.ComponentModel;

namespace ProjectARVR
{
    public class ObjectiveTestResultFix : ViewModelBase
    {
        [Category("White")]
        public double CenterCorrelatedColorTemperature { get => _CenterCorrelatedColorTemperature; set { _CenterCorrelatedColorTemperature = value; NotifyPropertyChanged(); } }
        private double _CenterCorrelatedColorTemperature = 1;

        [Category("White")]
        public double CenterLuminace { get => _CenterLuminace; set { _CenterLuminace = value; NotifyPropertyChanged(); } }
        private double _CenterLuminace = 1;
        [Category("White")]
        public double LuminanceUniformity { get => _LuminanceUniformity; set { _LuminanceUniformity = value; NotifyPropertyChanged(); } }
        private double _LuminanceUniformity = 1;
        [Category("White")]
        public double ColorUniformity { get => _ColorUniformity; set { _ColorUniformity = value; NotifyPropertyChanged(); } }
        private double _ColorUniformity = 1;


        [Category("White")]
        public double HorizontalFieldOfViewAngle { get => _HorizontalFieldOfViewAngle; set { _HorizontalFieldOfViewAngle = value; NotifyPropertyChanged(); } }
        private double _HorizontalFieldOfViewAngle = 1;
        [Category("White")]
        public double VerticalFieldOfViewAngle { get => _VerticalFieldOfViewAngle; set { _VerticalFieldOfViewAngle = value; NotifyPropertyChanged(); } }
        private double _VerticalFieldOfViewAngle = 1;

        [Category("White")]
        public double DiagonalFieldOfViewAngle { get => _DiagonalFieldOfViewAngle; set { _DiagonalFieldOfViewAngle = value; NotifyPropertyChanged(); } }
        private double _DiagonalFieldOfViewAngle = 1;



        [Category("Black")]
        public double FOFOContrast { get => _FOFOContrast; set { _FOFOContrast = value; NotifyPropertyChanged(); } }
        private double _FOFOContrast = 1;

        [Category("Chessboard")]
        public double ChessboardContrast { get => _ChessboardContrast; set { _ChessboardContrast = value; NotifyPropertyChanged(); } }
        private double _ChessboardContrast = 1;

        [Category("MTFH")]
        public double MTF_H_Center_0F { get => _MTF_H_Center_0F; set { _MTF_H_Center_0F = value; NotifyPropertyChanged(); } }
        private double _MTF_H_Center_0F = 1;
        [Category("MTFH")]
        public double MTF_H_LeftUp_0_5F { get => _MTF_H_LeftUp_0_5F; set { _MTF_H_LeftUp_0_5F = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftUp_0_5F = 1;
        [Category("MTFH")]
        public double MTF_H_RightUp_0_5F { get => _MTF_H_RightUp_0_5F; set { _MTF_H_RightUp_0_5F = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightUp_0_5F = 1;
        [Category("MTFH")]
        public double MTF_H_LeftDown_0_5F { get => _MTF_H_LeftDown_0_5F; set { _MTF_H_LeftDown_0_5F = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftDown_0_5F = 1;
        [Category("MTFH")]
        public double MTF_H_RightDown_0_5F { get => _MTF_H_RightDown_0_5F; set { _MTF_H_RightDown_0_5F = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightDown_0_5F = 1;
        [Category("MTFH")]
        public double MTF_H_LeftUp_0_8F { get => _MTF_H_LeftUp_0_8F; set { _MTF_H_LeftUp_0_8F = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftUp_0_8F = 1;
        [Category("MTFH")]
        public double MTF_H_RightUp_0_8F { get => _MTF_H_RightUp_0_8F; set { _MTF_H_RightUp_0_8F = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightUp_0_8F = 1;
        [Category("MTFH")]
        public double MTF_H_LeftDown_0_8F { get => _MTF_H_LeftDown_0_8F; set { _MTF_H_LeftDown_0_8F = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftDown_0_8F = 1;
        [Category("MTFH")]
        public double MTF_H_RightDown_0_8F { get => _MTF_H_RightDown_0_8F; set { _MTF_H_RightDown_0_8F = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightDown_0_8F = 1;

        [Category("MTFV")]
        public double MTF_V_Center_0F { get => _MTF_V_Center_0F; set { _MTF_V_Center_0F = value; NotifyPropertyChanged(); } }
        private double _MTF_V_Center_0F = 1;
        [Category("MTFV")]
        public double MTF_V_LeftUp_0_5F { get => _MTF_V_LeftUp_0_5F; set { _MTF_V_LeftUp_0_5F = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftUp_0_5F = 1;
        [Category("MTFV")]
        public double MTF_V_RightUp_0_5F { get => _MTF_V_RightUp_0_5F; set { _MTF_V_RightUp_0_5F = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightUp_0_5F = 1;
        [Category("MTFV")]
        public double MTF_V_LeftDown_0_5F { get => _MTF_V_LeftDown_0_5F; set { _MTF_V_LeftDown_0_5F = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftDown_0_5F = 1;
        [Category("MTFV")]
        public double MTF_V_RightDown_0_5F { get => _MTF_V_RightDown_0_5F; set { _MTF_V_RightDown_0_5F = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightDown_0_5F = 1;
        [Category("MTFV")]
        public double MTF_V_LeftUp_0_8F { get => _MTF_V_LeftUp_0_8F; set { _MTF_V_LeftUp_0_8F = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftUp_0_8F = 1;
        [Category("MTFV")]
        public double MTF_V_RightUp_0_8F { get => _MTF_V_RightUp_0_8F; set { _MTF_V_RightUp_0_8F = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightUp_0_8F = 1;
        [Category("MTFV")]
        public double MTF_V_LeftDown_0_8F { get => _MTF_V_LeftDown_0_8F; set { _MTF_V_LeftDown_0_8F = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftDown_0_8F = 1;
        [Category("MTFV")]
        public double MTF_V_RightDown_0_8F { get => _MTF_V_RightDown_0_8F; set { _MTF_V_RightDown_0_8F = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightDown_0_8F = 1;

        [Category("Distortion")]
        public double HorizontalTVDistortion { get => _HorizontalTVDistortion; set { _HorizontalTVDistortion = value; NotifyPropertyChanged(); } }
        private double _HorizontalTVDistortion = 1;
        [Category("Distortion")]
        public double VerticalTVDistortion { get => _VerticalTVDistortion; set { _VerticalTVDistortion = value; NotifyPropertyChanged(); } }
        private double _VerticalTVDistortion = 1;

        [Category("OpticCenter")]
        public double Rotation { get => _Rotation; set { _Rotation = value; NotifyPropertyChanged(); } }
        private double _Rotation = 1;
        [Category("OpticCenter")]
        public double XTilt { get => _XTilt; set { _XTilt = value; NotifyPropertyChanged(); } }
        private double _XTilt = 1;
        [Category("OpticCenter")]
        public double YTilt { get => _YTilt; set { _YTilt = value; NotifyPropertyChanged(); } }
        private double _YTilt = 1;

        public double Ghost { get => _Ghost; set { _Ghost = value; NotifyPropertyChanged(); } }
        private double _Ghost = 1;
    }

}