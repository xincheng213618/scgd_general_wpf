#pragma warning disable
using ColorVision.Common.MVVM;
using ProjectARVR;
using ProjectARVR;
using ProjectARVR.Config;
using System.ComponentModel;

namespace ProjectARVR
{
    [DisplayName("ARVR上下限判定")]
    public class ARVRRecipeConfig : ViewModelBase
    {
        [DisplayName("Luminance uniformity(%) Min"),Category("White")]
        public double LuminanceUniformityMin { get => _LuminanceUniformityMin; set { _LuminanceUniformityMin = value; NotifyPropertyChanged(); } }
        private double _LuminanceUniformityMin = 0.75;

        [DisplayName("Luminance uniformity(%) Max"), Category("White")]
        public double LuminanceUniformityMax { get => _LuminanceUniformityMax; set { _LuminanceUniformityMax = value; NotifyPropertyChanged(); } }
        private double _LuminanceUniformityMax;
        [Category("White")]
        public double ColorUniformityMin { get => _ColorUniformityMin; set { _ColorUniformityMin = value; NotifyPropertyChanged(); } }
        private double _ColorUniformityMin;
        [Category("White")]
        public double ColorUniformityMax { get => _ColorUniformityMax; set { _ColorUniformityMax = value; NotifyPropertyChanged(); } }
        private double _ColorUniformityMax = 0.02;
        [Category("White")]
        public double CenterLuminaceMax { get => _CenterLuminaceMax; set { _CenterLuminaceMax = value; NotifyPropertyChanged(); } }
        private double _CenterLuminaceMax = 0;
        [Category("White")]
        public double CenterLuminaceMin { get => _CenterLuminaceMin; set { _CenterLuminaceMin = value; NotifyPropertyChanged(); } }
        private double _CenterLuminaceMin = 0;
        [Category("White")]
        public double White1CenterLuminaceMax { get => _White1CenterLuminaceMax; set { _White1CenterLuminaceMax = value; NotifyPropertyChanged(); } }
        private double _White1CenterLuminaceMax = 0;
        [Category("White")]
        public double White1CenterLuminaceMin { get => _White1CenterLuminaceMin; set { _White1CenterLuminaceMin = value; NotifyPropertyChanged(); } }
        private double _White1CenterLuminaceMin = 0;

        [Category("White")]
        [DisplayName("Center Correlated Color Temperature(K) Min")]
        public double CenterCorrelatedColorTemperatureMin { get => _CenterCorrelatedColorTemperatureMin; set { _CenterCorrelatedColorTemperatureMin = value; NotifyPropertyChanged(); } }
        private double _CenterCorrelatedColorTemperatureMin = 6000;
        [Category("White")]
        [DisplayName("Center Correlated Color Temperature(K) Max")]
        public double CenterCorrelatedColorTemperatureMax { get => _CenterCorrelatedColorTemperatureMax; set { _CenterCorrelatedColorTemperatureMax = value; NotifyPropertyChanged(); } }
        private double _CenterCorrelatedColorTemperatureMax = 7000;

        [Category("White")]
        [DisplayName("White1 Center Correlated Color Temperature(K) Min")]
        public double White1CenterCorrelatedColorTemperatureMin { get => _White1CenterCorrelatedColorTemperatureMin; set { _White1CenterCorrelatedColorTemperatureMin = value; NotifyPropertyChanged(); } }
        private double _White1CenterCorrelatedColorTemperatureMin = 6000;
        [Category("White")]
        [DisplayName("White1 Center Correlated Color Temperature(K) Max")]
        public double White1CenterCorrelatedColorTemperatureMax { get => _White1CenterCorrelatedColorTemperatureMax; set { _White1CenterCorrelatedColorTemperatureMax = value; NotifyPropertyChanged(); } }
        private double _White1CenterCorrelatedColorTemperatureMax = 7000;

        [Category("White")]
        [DisplayName("Horizontal Field Of View Angle(°) Min")]
        public double HorizontalFieldOfViewAngleMin { get => _HorizontalFieldOfViewAngleMin; set { _HorizontalFieldOfViewAngleMin = value; NotifyPropertyChanged(); } }
        private double _HorizontalFieldOfViewAngleMin = 23.5;
        [Category("White")]
        [DisplayName("Horizontal Field Of View Angle(°) Max")]
        public double HorizontalFieldOfViewAngleMax { get => _HorizontalFieldOfViewAngleMax; set { _HorizontalFieldOfViewAngleMax = value; NotifyPropertyChanged(); } }
        private double _HorizontalFieldOfViewAngleMax = 24.5;
        [Category("White")]
        [DisplayName("Vertical Field of View Angle(°) Min")]
        public double VerticalFieldOfViewAngleMin { get => _VerticalFieldOfViewAngleMin; set { _VerticalFieldOfViewAngleMin = value; NotifyPropertyChanged(); } }
        private double _VerticalFieldOfViewAngleMin = 21.5;
        [Category("White")]
        [DisplayName("Vertical Field of View Angle(°) Max")]
        public double VerticalFieldOfViewAngleMax { get => _VerticalFieldOfViewAngleMax; set { _VerticalFieldOfViewAngleMax = value; NotifyPropertyChanged(); } }
        private double _VerticalFieldOfViewAngleMax = 22.5;
        [Category("White")]
        [DisplayName("Diagonal  Field of View Angle(°) Min")]
        public double DiagonalFieldOfViewAngleMin { get => _DiagonalFieldOfViewAngleMin; set { _DiagonalFieldOfViewAngleMin = value; NotifyPropertyChanged(); } }
        private double _DiagonalFieldOfViewAngleMin = 11.5;
        [Category("White")]
        [DisplayName("Diagonal  Field of View Angle(°) Max")]
        public double DiagonalFieldOfViewAngleMax { get => _DiagonalFieldOfViewAngleMax; set { _DiagonalFieldOfViewAngleMax = value; NotifyPropertyChanged(); } }
        private double _DiagonalFieldOfViewAngleMax = 12.5;
        [Category("Black")]
        public double FOFOContrastMin { get => _FOFOContrastMin; set { _FOFOContrastMin = value; NotifyPropertyChanged(); } }
        private double _FOFOContrastMin = 100000;
        [Category("Black")]
        public double FOFOContrastMax { get => _FOFOContrastMax; set { _FOFOContrastMax = value; NotifyPropertyChanged(); } }
        private double _FOFOContrastMax;
        [Category("Chessboard")]
        public double ChessboardContrastMin { get => _ChessboardContrastMin; set { _ChessboardContrastMin = value; NotifyPropertyChanged(); } }
        private double _ChessboardContrastMin = 50;
        [Category("Chessboard")]
        public double ChessboardContrastMax { get => _ChessboardContrastMax; set { _ChessboardContrastMax = value; NotifyPropertyChanged(); } }
        private double _ChessboardContrastMax;


        [Category("MTFH")]
        public double MTF_H_Center_0FMin { get => _MTF_H_Center_0FMin; set { _MTF_H_Center_0FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_H_Center_0FMin = 0.5;
        [Category("MTFH")]
        public double MTF_H_Center_0FMax { get => _MTF_H_Center_0FMax; set { _MTF_H_Center_0FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_H_Center_0FMax;
        [Category("MTFH")]
        public double MTF_H_LeftUp_0_5FMin { get => _MTF_H_LeftUp_0_5FMin; set { _MTF_H_LeftUp_0_5FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftUp_0_5FMin = 0.5;
        [Category("MTFH")]
        public double MTF_H_LeftUp_0_5FMax { get => _MTF_H_LeftUp_0_5FMax; set { _MTF_H_LeftUp_0_5FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftUp_0_5FMax;
        [Category("MTFH")]
        public double MTF_H_RightUp_0_5FMin { get => _MTF_H_RightUp_0_5FMin; set { _MTF_H_RightUp_0_5FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightUp_0_5FMin = 0.5;
        [Category("MTFH")]
        public double MTF_H_RightUp_0_5FMax { get => _MTF_H_RightUp_0_5FMax; set { _MTF_H_RightUp_0_5FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightUp_0_5FMax;
        [Category("MTFH")]
        public double MTF_H_RightDown_0_5FMin { get => _MTF_H_RightDown_0_5FMin; set { _MTF_H_RightDown_0_5FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightDown_0_5FMin = 0.5;
        [Category("MTFH")]
        public double MTF_H_RightDown_0_5FMax { get => _MTF_H_RightDown_0_5FMax; set { _MTF_H_RightDown_0_5FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightDown_0_5FMax;
        [Category("MTFH")]
        public double MTF_H_LeftDown_0_5FMin { get => _MTF_H_LeftDown_0_5FMin; set { _MTF_H_LeftDown_0_5FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftDown_0_5FMin = 0.5;
        [Category("MTFH")]
        public double MTF_H_LeftDown_0_5FMax { get => _MTF_H_LeftDown_0_5FMax; set { _MTF_H_LeftDown_0_5FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftDown_0_5FMax;
        [Category("MTFH")]
        public double MTF_H_LeftUp_0_8FMin { get => _MTF_H_LeftUp_0_8FMin; set { _MTF_H_LeftUp_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftUp_0_8FMin = 0.5;
        [Category("MTFH")]
        public double MTF_H_LeftUp_0_8FMax { get => _MTF_H_LeftUp_0_8FMax; set { _MTF_H_LeftUp_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftUp_0_8FMax;
        [Category("MTFH")]
        public double MTF_H_RightUp_0_8FMin { get => _MTF_H_RightUp_0_8FMin; set { _MTF_H_RightUp_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightUp_0_8FMin = 0.5;
        [Category("MTFH")]
        public double MTF_H_RightUp_0_8FMax { get => _MTF_H_RightUp_0_8FMax; set { _MTF_H_RightUp_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightUp_0_8FMax;
        [Category("MTFH")]
        public double MTF_H_RightDown_0_8FMin { get => _MTF_H_RightDown_0_8FMin; set { _MTF_H_RightDown_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightDown_0_8FMin = 0.5;
        [Category("MTFH")]

        public double MTF_H_RightDown_0_8FMax { get => _MTF_H_RightDown_0_8FMax; set { _MTF_H_RightDown_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_H_RightDown_0_8FMax;
        [Category("MTFH")]

        public double MTF_H_LeftDown_0_8FMin { get => _MTF_H_LeftDown_0_8FMin; set { _MTF_H_LeftDown_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftDown_0_8FMin = 0.5;
        [Category("MTFH")]
        public double MTF_H_LeftDown_0_8FMax { get => _MTF_H_LeftDown_0_8FMax; set { _MTF_H_LeftDown_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_H_LeftDown_0_8FMax;
        [Category("MTFV")]
        public double MTF_V_Center_0FMin { get => _MTF_V_Center_0FMin; set { _MTF_V_Center_0FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_V_Center_0FMin = 0.5;
        [Category("MTFV")]
        public double MTF_V_Center_0FMax { get => _MTF_V_Center_0FMax; set { _MTF_V_Center_0FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_V_Center_0FMax;
        [Category("MTFV")]

        public double MTF_V_LeftUp_0_5FMin { get => _MTF_V_LeftUp_0_5FMin; set { _MTF_V_LeftUp_0_5FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftUp_0_5FMin = 0.5;
        [Category("MTFV")]

        public double MTF_V_LeftUp_0_5FMax { get => _MTF_V_LeftUp_0_5FMax; set { _MTF_V_LeftUp_0_5FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftUp_0_5FMax;
        [Category("MTFV")]

        public double MTF_V_RightUp_0_5FMin { get => _MTF_V_RightUp_0_5FMin; set { _MTF_V_RightUp_0_5FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightUp_0_5FMin = 0.5;
        [Category("MTFV")]

        public double MTF_V_RightUp_0_5FMax { get => _MTF_V_RightUp_0_5FMax; set { _MTF_V_RightUp_0_5FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightUp_0_5FMax;
        [Category("MTFV")]

        public double MTF_V_RightDown_0_5FMin { get => _MTF_V_RightDown_0_5FMin; set { _MTF_V_RightDown_0_5FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightDown_0_5FMin = 0.5;
        [Category("MTFV")]

        public double MTF_V_RightDown_0_5FMax { get => _MTF_V_RightDown_0_5FMax; set { _MTF_V_RightDown_0_5FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightDown_0_5FMax;
        [Category("MTFV")]

        public double MTF_V_LeftDown_0_5FMin { get => _MTF_V_LeftDown_0_5FMin; set { _MTF_V_LeftDown_0_5FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftDown_0_5FMin = 0.5;
        [Category("MTFV")]

        public double MTF_V_LeftDown_0_5FMax { get => _MTF_V_LeftDown_0_5FMax; set { _MTF_V_LeftDown_0_5FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftDown_0_5FMax;
        [Category("MTFV")]

        public double MTF_V_LeftUp_0_8FMin { get => _MTF_V_LeftUp_0_8FMin; set { _MTF_V_LeftUp_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftUp_0_8FMin = 0.5;
        [Category("MTFV")]

        public double MTF_V_LeftUp_0_8FMax { get => _MTF_V_LeftUp_0_8FMax; set { _MTF_V_LeftUp_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftUp_0_8FMax;
        [Category("MTFV")]

        public double MTF_V_RightUp_0_8FMin { get => _MTF_V_RightUp_0_8FMin; set { _MTF_V_RightUp_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightUp_0_8FMin = 0.5;
        [Category("MTFV")]

        public double MTF_V_RightUp_0_8FMax { get => _MTF_V_RightUp_0_8FMax; set { _MTF_V_RightUp_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightUp_0_8FMax;
        [Category("MTFV")]

        public double MTF_V_RightDown_0_8FMin { get => _MTF_V_RightDown_0_8FMin; set { _MTF_V_RightDown_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightDown_0_8FMin = 0.5;
        [Category("MTFV")]

        public double MTF_V_RightDown_0_8FMax { get => _MTF_V_RightDown_0_8FMax; set { _MTF_V_RightDown_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_V_RightDown_0_8FMax;
        [Category("MTFV")]
        public double MTF_V_LeftDown_0_8FMin { get => _MTF_V_LeftDown_0_8FMin; set { _MTF_V_LeftDown_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftDown_0_8FMin = 0.5;
        [Category("MTFV")]
        public double MTF_V_LeftDown_0_8FMax { get => _MTF_V_LeftDown_0_8FMax; set { _MTF_V_LeftDown_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_V_LeftDown_0_8FMax;
        [Category("Distortion")]
        public double HorizontalTVDistortionMin { get => _HorizontalTVDistortionMin; set { _HorizontalTVDistortionMin = value; NotifyPropertyChanged(); } }
        private double _HorizontalTVDistortionMin;
        [Category("Distortion")]

        public double HorizontalTVDistortionMax { get => _HorizontalTVDistortionMax; set { _HorizontalTVDistortionMax = value; NotifyPropertyChanged(); } }
        private double _HorizontalTVDistortionMax = 2.1;
        [Category("Distortion")]
        public double VerticalTVDistortionMin { get => _VerticalTVDistortionMin; set { _VerticalTVDistortionMin = value; NotifyPropertyChanged(); } }
        private double _VerticalTVDistortionMin;
        [Category("Distortion")]
        public double VerticalTVDistortionMax { get => _VerticalTVDistortionMax; set { _VerticalTVDistortionMax = value; NotifyPropertyChanged(); } }
        private double _VerticalTVDistortionMax = 2.1;


        [Category("OpticCenter")]
        public double XTiltMin { get => _XTiltMin; set { _XTiltMin = value; NotifyPropertyChanged(); } }
        private double _XTiltMin = -0.16;
        [Category("OpticCenter")]
        public double XTiltMax { get => _XTiltMax; set { _XTiltMax = value; NotifyPropertyChanged(); } }
        private double _XTiltMax = 0.16;
        [Category("OpticCenter")]
        public double YTiltMin { get => _YTiltMin; set { _YTiltMin = value; NotifyPropertyChanged(); } }
        private double _YTiltMin = -0.16;
        [Category("OpticCenter")]
        public double YTiltMax { get => _YTiltMax; set { _YTiltMax = value; NotifyPropertyChanged(); } }
        private double _YTiltMax = 0.16;
        [Category("OpticCenter")]
        public double RotationMin { get => _RotationMin; set { _RotationMin = value; NotifyPropertyChanged(); } }
        private double _RotationMin = -0.16;

        [Category("OpticCenter")]
        public double RotationMax { get => _RotationMax; set { _RotationMax = value; NotifyPropertyChanged(); } }
        private double _RotationMax = 0.16;

        [Category("Ghost")]
        public double GhostMin { get => _GhostMin; set { _GhostMin = value; NotifyPropertyChanged(); } }
        private double _GhostMin;
        [Category("Ghost")]
        public double GhostMax { get => _GhostMax; set { _GhostMax = value; NotifyPropertyChanged(); } }
        private double _GhostMax = 0.05;

    }
}