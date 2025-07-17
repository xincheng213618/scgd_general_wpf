#pragma warning disable
using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ProjectARVRLite.Config
{
    [DisplayName("ARVR上下限判定")]
    public class SPECConfig : ViewModelBase
    {
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; NotifyPropertyChanged(); } }
        private bool _IsEnabled = true;

        [DisplayName("Horizontal Field Of View Angle(°) Min")]
        public double HorizontalFieldOfViewAngleMin { get => _HorizontalFieldOfViewAngleMin; set { _HorizontalFieldOfViewAngleMin = value; NotifyPropertyChanged(); } }
        private double _HorizontalFieldOfViewAngleMin = 23.5;

        [DisplayName("Horizontal Field Of View Angle(°) Max")]
        public double HorizontalFieldOfViewAngleMax { get => _HorizontalFieldOfViewAngleMax; set { _HorizontalFieldOfViewAngleMax = value; NotifyPropertyChanged(); } }
        private double _HorizontalFieldOfViewAngleMax = 24.5;

        [DisplayName("Vertical Field of View Angle(°) Min")]
        public double VerticalFieldOfViewAngleMin { get => _VerticalFieldOfViewAngleMin; set { _VerticalFieldOfViewAngleMin = value; NotifyPropertyChanged(); } }
        private double _VerticalFieldOfViewAngleMin = 21.5;

        [DisplayName("Vertical Field of View Angle(°) Max")]
        public double VerticalFieldOfViewAngleMax { get => _VerticalFieldOfViewAngleMax; set { _VerticalFieldOfViewAngleMax = value; NotifyPropertyChanged(); } }
        private double _VerticalFieldOfViewAngleMax = 22.5;

        [DisplayName("Diagonal  Field of View Angle(°) Min")]
        public double DiagonalFieldOfViewAngleMin { get => _DiagonalFieldOfViewAngleMin; set { _DiagonalFieldOfViewAngleMin = value; NotifyPropertyChanged(); } }
        private double _DiagonalFieldOfViewAngleMin = 11.5;

        [DisplayName("Diagonal  Field of View Angle(°) Max")]
        public double DiagonalFieldOfViewAngleMax { get => _DiagonalFieldOfViewAngleMax; set { _DiagonalFieldOfViewAngleMax = value; NotifyPropertyChanged(); } }
        private double _DiagonalFieldOfViewAngleMax = 12.5;


        [DisplayName("Luminance uniformity(%) Min")]
        public double LuminanceUniformityMin { get => _LuminanceUniformityMin; set { _LuminanceUniformityMin = value; NotifyPropertyChanged(); } }
        private double _LuminanceUniformityMin = 0.75;

        [DisplayName("Luminance uniformity(%) Max")]
        public double LuminanceUniformityMax { get => _LuminanceUniformityMax; set { _LuminanceUniformityMax = value; NotifyPropertyChanged(); } }
        private double _LuminanceUniformityMax;

        public double ColorUniformityMin { get => _ColorUniformityMin; set { _ColorUniformityMin = value; NotifyPropertyChanged(); } }
        private double _ColorUniformityMin;

        public double ColorUniformityMax { get => _ColorUniformityMax; set { _ColorUniformityMax = value; NotifyPropertyChanged(); } }
        private double _ColorUniformityMax = 0.02;

        [DisplayName("Center Correlated Color Temperature(K) Min")]
        public double CenterCorrelatedColorTemperatureMin { get => _CenterCorrelatedColorTemperatureMin; set { _CenterCorrelatedColorTemperatureMin = value; NotifyPropertyChanged(); } }
        private double _CenterCorrelatedColorTemperatureMin = 6000;

        [DisplayName("Center Correlated Color Temperature(K) Max")]
        public double CenterCorrelatedColorTemperatureMax { get => _CenterCorrelatedColorTemperatureMax; set { _CenterCorrelatedColorTemperatureMax = value; NotifyPropertyChanged(); } }
        private double _CenterCorrelatedColorTemperatureMax = 7000;


        public double W25CenterLunimanceMin { get => _W25CenterLunimanceMin; set { _W25CenterLunimanceMin = value; NotifyPropertyChanged(); } }
        private double _W25CenterLunimanceMin = 0;

        public double W25CenterLunimanceMax { get => _W25CenterLunimanceMax; set { _W25CenterLunimanceMax = value; NotifyPropertyChanged(); } }
        private double _W25CenterLunimanceMax = 0;

        public double W255CenterLunimanceMin { get => _W255CenterLunimanceMin; set { _W255CenterLunimanceMin = value; NotifyPropertyChanged(); } }
        private double _W255CenterLunimanceMin = 0;

        public double W255CenterLunimanceMax { get => _W255CenterLunimanceMax; set { _W255CenterLunimanceMax = value; NotifyPropertyChanged(); } }
        private double _W255CenterLunimanceMax = 0;

        public double FOFOContrastMin { get => _FOFOContrastMin; set { _FOFOContrastMin = value; NotifyPropertyChanged(); } }
        private double _FOFOContrastMin = 100000;

        public double FOFOContrastMax { get => _FOFOContrastMax; set { _FOFOContrastMax = value; NotifyPropertyChanged(); } }
        private double _FOFOContrastMax;

        public double ChessboardContrastMin { get => _ChessboardContrastMin; set { _ChessboardContrastMin = value; NotifyPropertyChanged(); } }
        private double _ChessboardContrastMin = 50;

        public double ChessboardContrastMax { get => _ChessboardContrastMax; set { _ChessboardContrastMax = value; NotifyPropertyChanged(); } }
        private double _ChessboardContrastMax;

        public double HorizontalTVDistortionMin { get => _HorizontalTVDistortionMin; set { _HorizontalTVDistortionMin = value; NotifyPropertyChanged(); } }
        private double _HorizontalTVDistortionMin;

        public double HorizontalTVDistortionMax { get => _HorizontalTVDistortionMax; set { _HorizontalTVDistortionMax = value; NotifyPropertyChanged(); } }
        private double _HorizontalTVDistortionMax = 2.1;

        public double VerticalTVDistortionMin { get => _VerticalTVDistortionMin; set { _VerticalTVDistortionMin = value; NotifyPropertyChanged(); } }
        private double _VerticalTVDistortionMin;

        public double VerticalTVDistortionMax { get => _VerticalTVDistortionMax; set { _VerticalTVDistortionMax = value; NotifyPropertyChanged(); } }
        private double _VerticalTVDistortionMax = 2.1;

        public double MTF_HV_H_Center_0FMin { get => _MTF_HV_H_Center_0FMin; set { _MTF_HV_H_Center_0FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_Center_0FMin = 0.5;

        public double MTF_HV_H_Center_0FMax { get => _MTF_HV_H_Center_0FMax; set { _MTF_HV_H_Center_0FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_Center_0FMax;

        public double MTF_HV_H_LeftUp_0_4FMin { get => _MTF_HV_H_LeftUp_0_4FMin; set { _MTF_HV_H_LeftUp_0_4FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_4FMin = 0.5;

        public double MTF_HV_H_LeftUp_0_4FMax { get => _MTF_HV_H_LeftUp_0_4FMax; set { _MTF_HV_H_LeftUp_0_4FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_4FMax;

        public double MTF_HV_H_RightUp_0_4FMin { get => _MTF_HV_H_RightUp_0_4FMin; set { _MTF_HV_H_RightUp_0_4FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_4FMin = 0.5;

        public double MTF_HV_H_RightUp_0_4FMax { get => _MTF_HV_H_RightUp_0_4FMax; set { _MTF_HV_H_RightUp_0_4FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_4FMax;

        public double MTF_HV_H_RightDown_0_4FMin { get => _MTF_HV_H_RightDown_0_4FMin; set { _MTF_HV_H_RightDown_0_4FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_4FMin = 0.5;

        public double MTF_HV_H_RightDown_0_4FMax { get => _MTF_HV_H_RightDown_0_4FMax; set { _MTF_HV_H_RightDown_0_4FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_4FMax;

        public double MTF_HV_H_LeftDown_0_4FMin { get => _MTF_HV_H_LeftDown_0_4FMin; set { _MTF_HV_H_LeftDown_0_4FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_4FMin = 0.5;

        public double MTF_HV_H_LeftDown_0_4FMax { get => _MTF_HV_H_LeftDown_0_4FMax; set { _MTF_HV_H_LeftDown_0_4FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_4FMax;

        public double MTF_HV_H_LeftUp_0_8FMin { get => _MTF_HV_H_LeftUp_0_8FMin; set { _MTF_HV_H_LeftUp_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_8FMin = 0.5;

        public double MTF_HV_H_LeftUp_0_8FMax { get => _MTF_HV_H_LeftUp_0_8FMax; set { _MTF_HV_H_LeftUp_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_LeftUp_0_8FMax;

        public double MTF_HV_H_RightUp_0_8FMin { get => _MTF_HV_H_RightUp_0_8FMin; set { _MTF_HV_H_RightUp_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_8FMin = 0.5;

        public double MTF_HV_H_RightUp_0_8FMax { get => _MTF_HV_H_RightUp_0_8FMax; set { _MTF_HV_H_RightUp_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_RightUp_0_8FMax;

        public double MTF_HV_H_RightDown_0_8FMin { get => _MTF_HV_H_RightDown_0_8FMin; set { _MTF_HV_H_RightDown_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_8FMin = 0.5;

        public double MTF_HV_H_RightDown_0_8FMax { get => _MTF_HV_H_RightDown_0_8FMax; set { _MTF_HV_H_RightDown_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_RightDown_0_8FMax;

        public double MTF_HV_H_LeftDown_0_8FMin { get => _MTF_HV_H_LeftDown_0_8FMin; set { _MTF_HV_H_LeftDown_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_8FMin = 0.5;

        public double MTF_HV_H_LeftDown_0_8FMax { get => _MTF_HV_H_LeftDown_0_8FMax; set { _MTF_HV_H_LeftDown_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_H_LeftDown_0_8FMax;

        public double MTF_HV_V_Center_0FMin { get => _MTF_HV_V_Center_0FMin; set { _MTF_HV_V_Center_0FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_Center_0FMin = 0.5;

        public double MTF_HV_V_Center_0FMax { get => _MTF_HV_V_Center_0FMax; set { _MTF_HV_V_Center_0FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_Center_0FMax;

        public double MTF_HV_V_LeftUp_0_4FMin { get => _MTF_HV_V_LeftUp_0_4FMin; set { _MTF_HV_V_LeftUp_0_4FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_4FMin = 0.5;

        public double MTF_HV_V_LeftUp_0_4FMax { get => _MTF_HV_V_LeftUp_0_4FMax; set { _MTF_HV_V_LeftUp_0_4FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_4FMax;

        public double MTF_HV_V_RightUp_0_4FMin { get => _MTF_HV_V_RightUp_0_4FMin; set { _MTF_HV_V_RightUp_0_4FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_4FMin = 0.5;

        public double MTF_HV_V_RightUp_0_4FMax { get => _MTF_HV_V_RightUp_0_4FMax; set { _MTF_HV_V_RightUp_0_4FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_4FMax;

        public double MTF_HV_V_RightDown_0_4FMin { get => _MTF_HV_V_RightDown_0_4FMin; set { _MTF_HV_V_RightDown_0_4FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_4FMin = 0.5;

        public double MTF_HV_V_RightDown_0_4FMax { get => _MTF_HV_V_RightDown_0_4FMax; set { _MTF_HV_V_RightDown_0_4FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_4FMax;

        public double MTF_HV_V_LeftDown_0_4FMin { get => _MTF_HV_V_LeftDown_0_4FMin; set { _MTF_HV_V_LeftDown_0_4FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_4FMin = 0.5;

        public double MTF_HV_V_LeftDown_0_4FMax { get => _MTF_HV_V_LeftDown_0_4FMax; set { _MTF_HV_V_LeftDown_0_4FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_4FMax;

        public double MTF_HV_V_LeftUp_0_8FMin { get => _MTF_HV_V_LeftUp_0_8FMin; set { _MTF_HV_V_LeftUp_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_8FMin = 0.5;

        public double MTF_HV_V_LeftUp_0_8FMax { get => _MTF_HV_V_LeftUp_0_8FMax; set { _MTF_HV_V_LeftUp_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_LeftUp_0_8FMax;

        public double MTF_HV_V_RightUp_0_8FMin { get => _MTF_HV_V_RightUp_0_8FMin; set { _MTF_HV_V_RightUp_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_8FMin = 0.5;

        public double MTF_HV_V_RightUp_0_8FMax { get => _MTF_HV_V_RightUp_0_8FMax; set { _MTF_HV_V_RightUp_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_RightUp_0_8FMax;

        public double MTF_HV_V_RightDown_0_8FMin { get => _MTF_HV_V_RightDown_0_8FMin; set { _MTF_HV_V_RightDown_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_8FMin = 0.5;

        public double MTF_HV_V_RightDown_0_8FMax { get => _MTF_HV_V_RightDown_0_8FMax; set { _MTF_HV_V_RightDown_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_RightDown_0_8FMax;

        public double MTF_HV_V_LeftDown_0_8FMin { get => _MTF_HV_V_LeftDown_0_8FMin; set { _MTF_HV_V_LeftDown_0_8FMin = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_8FMin = 0.5;

        public double MTF_HV_V_LeftDown_0_8FMax { get => _MTF_HV_V_LeftDown_0_8FMax; set { _MTF_HV_V_LeftDown_0_8FMax = value; NotifyPropertyChanged(); } }
        private double _MTF_HV_V_LeftDown_0_8FMax;

        public double OptCenterXTiltMin { get => _OptCenterXTiltMin; set { _OptCenterXTiltMin = value; NotifyPropertyChanged(); } }
        private double _OptCenterXTiltMin = -0.16;

        public double OptCenterXTiltMax { get => _OptCenterXTiltMax; set { _OptCenterXTiltMax = value; NotifyPropertyChanged(); } }
        private double _OptCenterXTiltMax = 0.16;

        public double OptCenterYTiltMin { get => _OptCenterYTiltMin; set { _OptCenterYTiltMin = value; NotifyPropertyChanged(); } }
        private double _OptCenterYTiltMin = -0.16;

        public double OptCenterYTiltMax { get => _OptCenterYTiltMax; set { _OptCenterYTiltMax = value; NotifyPropertyChanged(); } }
        private double _OptCenterYTiltMax = 0.16;

        public double OptCenterRotationMin { get => _OptCenterRotationMin; set { _OptCenterRotationMin = value; NotifyPropertyChanged(); } }
        private double _OptCenterRotationMin = -0.16;

        public double OptCenterRotationMax { get => _OptCenterRotationMax; set { _OptCenterRotationMax = value; NotifyPropertyChanged(); } }
        private double _OptCenterRotationMax = 0.16;

        public double ImageCenterXTiltMin { get => _ImageCenterXTiltMin; set { _ImageCenterXTiltMin = value; NotifyPropertyChanged(); } }
        private double _ImageCenterXTiltMin = -0.16;

        public double ImageCenterXTiltMax { get => _ImageCenterXTiltMax; set { _ImageCenterXTiltMax = value; NotifyPropertyChanged(); } }
        private double _ImageCenterXTiltMax = 0.16;

        public double ImageCenterYTiltMin { get => _ImageCenterYTiltMin; set { _ImageCenterYTiltMin = value; NotifyPropertyChanged(); } }
        private double _ImageCenterYTiltMin = -0.16;

        public double ImageCenterYTiltMax { get => _ImageCenterYTiltMax; set { _ImageCenterYTiltMax = value; NotifyPropertyChanged(); } }
        private double _ImageCenterYTiltMax = 0.16;

        public double ImageCenterRotationMin { get => _ImageCenterRotationMin; set { _ImageCenterRotationMin = value; NotifyPropertyChanged(); } }
        private double _ImageCenterRotationMin = -0.16;

        public double ImageCenterRotationMax { get => _ImageCenterRotationMax; set { _ImageCenterRotationMax = value; NotifyPropertyChanged(); } }
        private double _ImageCenterRotationMax = 0.16;





        public double GhostMin { get => _GhostMin; set { _GhostMin = value; NotifyPropertyChanged(); } }
        private double _GhostMin;

        public double GhostMax { get => _GhostMax; set { _GhostMax = value; NotifyPropertyChanged(); } }
        private double _GhostMax = 0.05;

    }
}