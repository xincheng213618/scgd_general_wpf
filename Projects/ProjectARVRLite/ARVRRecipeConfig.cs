#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ProjectARVRLite;
using ProjectARVRLite;
using System.ComponentModel;

namespace ProjectARVRLite
{
    [DisplayName("ARVR上下限判定")]
    public class ARVRRecipeConfig : ViewModelBase, IRecipe
    {
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled = true;

        [Category("W51")]
        [DisplayName("Horizontal Field Of View Angle(°) Min")]
        public double HorizontalFieldOfViewAngleMin { get => _HorizontalFieldOfViewAngleMin; set { _HorizontalFieldOfViewAngleMin = value; OnPropertyChanged(); } }
        private double _HorizontalFieldOfViewAngleMin = 23.5;
        [Category("W51")]
        [DisplayName("Horizontal Field Of View Angle(°) Max")]
        public double HorizontalFieldOfViewAngleMax { get => _HorizontalFieldOfViewAngleMax; set { _HorizontalFieldOfViewAngleMax = value; OnPropertyChanged(); } }
        private double _HorizontalFieldOfViewAngleMax = 24.5;
        [Category("W51")]
        [DisplayName("Vertical Field of View Angle(°) Min")]
        public double VerticalFieldOfViewAngleMin { get => _VerticalFieldOfViewAngleMin; set { _VerticalFieldOfViewAngleMin = value; OnPropertyChanged(); } }
        private double _VerticalFieldOfViewAngleMin = 21.5;
        [Category("W51")]
        [DisplayName("Vertical Field of View Angle(°) Max")]
        public double VerticalFieldOfViewAngleMax { get => _VerticalFieldOfViewAngleMax; set { _VerticalFieldOfViewAngleMax = value; OnPropertyChanged(); } }
        private double _VerticalFieldOfViewAngleMax = 22.5;
        [Category("W51")]
        [DisplayName("Diagonal  Field of View Angle(°) Min")]
        public double DiagonalFieldOfViewAngleMin { get => _DiagonalFieldOfViewAngleMin; set { _DiagonalFieldOfViewAngleMin = value; OnPropertyChanged(); } }
        private double _DiagonalFieldOfViewAngleMin = 11.5;
        [Category("W51")]
        [DisplayName("Diagonal  Field of View Angle(°) Max")]
        public double DiagonalFieldOfViewAngleMax { get => _DiagonalFieldOfViewAngleMax; set { _DiagonalFieldOfViewAngleMax = value; OnPropertyChanged(); } }
        private double _DiagonalFieldOfViewAngleMax = 12.5;

        [Category("W255")]
        [DisplayName("Luminance uniformity(%) Min")]
        public double W255LuminanceUniformityMin { get => _W255LuminanceUniformityMin; set { _W255LuminanceUniformityMin = value; OnPropertyChanged(); } }
        private double _W255LuminanceUniformityMin = 0.75;
        [Category("W255")]
        [DisplayName("Luminance uniformity(%) Max")]
        public double W255LuminanceUniformityMax { get => _W255LuminanceUniformityMax; set { _W255LuminanceUniformityMax = value; OnPropertyChanged(); } }
        private double _W255LuminanceUniformityMax;
        [Category("W255")]
        public double W255ColorUniformityMin { get => _W255ColorUniformityMin; set { _W255ColorUniformityMin = value; OnPropertyChanged(); } }
        private double _W255ColorUniformityMin;
        [Category("W255")]
        public double W255ColorUniformityMax { get => _W255ColorUniformityMax; set { _W255ColorUniformityMax = value; OnPropertyChanged(); } }
        private double _W255ColorUniformityMax = 0.02;
        [Category("W255")]
        [DisplayName("Center Correlated Color Temperature(K) Min")]
        public double CenterCorrelatedColorTemperatureMin { get => _CenterCorrelatedColorTemperatureMin; set { _CenterCorrelatedColorTemperatureMin = value; OnPropertyChanged(); } }
        private double _CenterCorrelatedColorTemperatureMin = 6000;
        [Category("W255")]
        [DisplayName("Center Correlated Color Temperature(K) Max")]
        public double CenterCorrelatedColorTemperatureMax { get => _CenterCorrelatedColorTemperatureMax; set { _CenterCorrelatedColorTemperatureMax = value; OnPropertyChanged(); } }
        private double _CenterCorrelatedColorTemperatureMax = 7000;



        [Category("W255")]
        public double W255CenterLunimanceMin { get => _W255CenterLunimanceMin; set { _W255CenterLunimanceMin = value; OnPropertyChanged(); } }
        private double _W255CenterLunimanceMin = 0;
        [Category("W255")]
        public double W255CenterLunimanceMax { get => _W255CenterLunimanceMax; set { _W255CenterLunimanceMax = value; OnPropertyChanged(); } }
        private double _W255CenterLunimanceMax = 0;
        [Category("W255")]
        public double W255CenterCIE1931ChromaticCoordinatesxMin { get => _W255CenterCIE1931ChromaticCoordinatesxMin; set { _W255CenterCIE1931ChromaticCoordinatesxMin = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesxMin = 0;
        [Category("W255")]

        public double W255CenterCIE1931ChromaticCoordinatesxMax { get => _W255CenterCIE1931ChromaticCoordinatesxMax; set { _W255CenterCIE1931ChromaticCoordinatesxMax = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesxMax = 0;
        [Category("W255")]
        public double W255CenterCIE1931ChromaticCoordinatesyMin { get => _W255CenterCIE1931ChromaticCoordinatesyMin; set { _W255CenterCIE1931ChromaticCoordinatesyMin = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesyMin = 0;
        [Category("W255")]
        public double W255CenterCIE1931ChromaticCoordinatesyMax { get => _W255CenterCIE1931ChromaticCoordinatesyMax; set { _W255CenterCIE1931ChromaticCoordinatesyMax = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesyMax = 0;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesuMin { get => _W255CenterCIE1976ChromaticCoordinatesuMin; set { _W255CenterCIE1976ChromaticCoordinatesuMin = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesuMin = 0;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesuMax { get => _W255CenterCIE1976ChromaticCoordinatesuMax; set { _W255CenterCIE1976ChromaticCoordinatesuMax = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesuMax = 0;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesvMin { get => _W255CenterCIE1976ChromaticCoordinatesvMin; set { _W255CenterCIE1976ChromaticCoordinatesvMin = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesvMin = 0;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesvMax { get => _W255CenterCIE1976ChromaticCoordinatesvMax; set { _W255CenterCIE1976ChromaticCoordinatesvMax = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesvMax = 0;

        [Category("W25")]
        public double W25CenterLunimanceMin { get => _W25CenterLunimanceMin; set { _W25CenterLunimanceMin = value; OnPropertyChanged(); } }
        private double _W25CenterLunimanceMin = 0;
        [Category("W25")]
        public double W25CenterLunimanceMax { get => _W25CenterLunimanceMax; set { _W25CenterLunimanceMax = value; OnPropertyChanged(); } }
        private double _W25CenterLunimanceMax = 0;

        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesxMin { get => _W25CenterCIE1931ChromaticCoordinatesxMin; set { _W25CenterCIE1931ChromaticCoordinatesxMin = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesxMin = 0;
        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesxMax { get => _W25CenterCIE1931ChromaticCoordinatesxMax; set { _W25CenterCIE1931ChromaticCoordinatesxMax = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesxMax = 0;
        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesyMin { get => _W25CenterCIE1931ChromaticCoordinatesyMin; set { _W25CenterCIE1931ChromaticCoordinatesyMin = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesyMin = 0;
        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesyMax { get => _W25CenterCIE1931ChromaticCoordinatesyMax; set { _W25CenterCIE1931ChromaticCoordinatesyMax = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesyMax = 0;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesuMin { get => _W25CenterCIE1976ChromaticCoordinatesuMin; set { _W25CenterCIE1976ChromaticCoordinatesuMin = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesuMin = 0;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesuMax { get => _W25CenterCIE1976ChromaticCoordinatesuMax; set { _W25CenterCIE1976ChromaticCoordinatesuMax = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesuMax = 0;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesvMin { get => _W25CenterCIE1976ChromaticCoordinatesvMin; set { _W25CenterCIE1976ChromaticCoordinatesvMin = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesvMin = 0;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesvMax { get => _W25CenterCIE1976ChromaticCoordinatesvMax; set { _W25CenterCIE1976ChromaticCoordinatesvMax = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesvMax = 0;

        [Category("Black")]

        public double FOFOContrastMin { get => _FOFOContrastMin; set { _FOFOContrastMin = value; OnPropertyChanged(); } }
        private double _FOFOContrastMin = 100000;
        [Category("Black")]
        public double FOFOContrastMax { get => _FOFOContrastMax; set { _FOFOContrastMax = value; OnPropertyChanged(); } }
        private double _FOFOContrastMax;
        [Category("Chessboard")]
        public double ChessboardContrastMin { get => _ChessboardContrastMin; set { _ChessboardContrastMin = value; OnPropertyChanged(); } }
        private double _ChessboardContrastMin = 50;
        [Category("Chessboard")]
        public double ChessboardContrastMax { get => _ChessboardContrastMax; set { _ChessboardContrastMax = value; OnPropertyChanged(); } }
        private double _ChessboardContrastMax;


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

        [Category("Distortion")]
        public double HorizontalTVDistortionMin { get => _HorizontalTVDistortionMin; set { _HorizontalTVDistortionMin = value; OnPropertyChanged(); } }
        private double _HorizontalTVDistortionMin;
        [Category("Distortion")]
        public double HorizontalTVDistortionMax { get => _HorizontalTVDistortionMax; set { _HorizontalTVDistortionMax = value; OnPropertyChanged(); } }
        private double _HorizontalTVDistortionMax = 2.1;
        [Category("Distortion")]
        public double VerticalTVDistortionMin { get => _VerticalTVDistortionMin; set { _VerticalTVDistortionMin = value; OnPropertyChanged(); } }
        private double _VerticalTVDistortionMin;
        [Category("Distortion")]
        public double VerticalTVDistortionMax { get => _VerticalTVDistortionMax; set { _VerticalTVDistortionMax = value; OnPropertyChanged(); } }
        private double _VerticalTVDistortionMax = 2.1;
        [Category("OpticCenter")]
        public double OptCenterXTiltMin { get => _OptCenterXTiltMin; set { _OptCenterXTiltMin = value; OnPropertyChanged(); } }
        private double _OptCenterXTiltMin = -0.16;
        [Category("OpticCenter")]
        public double OptCenterXTiltMax { get => _OptCenterXTiltMax; set { _OptCenterXTiltMax = value; OnPropertyChanged(); } }
        private double _OptCenterXTiltMax = 0.16;
        [Category("OpticCenter")]
        public double OptCenterYTiltMin { get => _OptCenterYTiltMin; set { _OptCenterYTiltMin = value; OnPropertyChanged(); } }
        private double _OptCenterYTiltMin = -0.16;
        [Category("OpticCenter")]
        public double OptCenterYTiltMax { get => _OptCenterYTiltMax; set { _OptCenterYTiltMax = value; OnPropertyChanged(); } }
        private double _OptCenterYTiltMax = 0.16;
        [Category("OpticCenter")]
        public double OptCenterRotationMin { get => _OptCenterRotationMin; set { _OptCenterRotationMin = value; OnPropertyChanged(); } }
        private double _OptCenterRotationMin = -0.16;
        [Category("OpticCenter")]
        public double OptCenterRotationMax { get => _OptCenterRotationMax; set { _OptCenterRotationMax = value; OnPropertyChanged(); } }
        private double _OptCenterRotationMax = 0.16;
        [Category("OpticCenter")]
        public double ImageCenterXTiltMin { get => _ImageCenterXTiltMin; set { _ImageCenterXTiltMin = value; OnPropertyChanged(); } }
        private double _ImageCenterXTiltMin = -0.16;
        [Category("OpticCenter")]
        public double ImageCenterXTiltMax { get => _ImageCenterXTiltMax; set { _ImageCenterXTiltMax = value; OnPropertyChanged(); } }
        private double _ImageCenterXTiltMax = 0.16;
        [Category("OpticCenter")]
        public double ImageCenterYTiltMin { get => _ImageCenterYTiltMin; set { _ImageCenterYTiltMin = value; OnPropertyChanged(); } }
        private double _ImageCenterYTiltMin = -0.16;
        [Category("OpticCenter")]
        public double ImageCenterYTiltMax { get => _ImageCenterYTiltMax; set { _ImageCenterYTiltMax = value; OnPropertyChanged(); } }
        private double _ImageCenterYTiltMax = 0.16;
        [Category("OpticCenter")]
        public double ImageCenterRotationMin { get => _ImageCenterRotationMin; set { _ImageCenterRotationMin = value; OnPropertyChanged(); } }
        private double _ImageCenterRotationMin = -0.16;
        [Category("OpticCenter")]
        public double ImageCenterRotationMax { get => _ImageCenterRotationMax; set { _ImageCenterRotationMax = value; OnPropertyChanged(); } }
        private double _ImageCenterRotationMax = 0.16;

        [Category("Ghost")]
        public double GhostMin { get => _GhostMin; set { _GhostMin = value; OnPropertyChanged(); } }
        private double _GhostMin;
        [Category("Ghost")]
        public double GhostMax { get => _GhostMax; set { _GhostMax = value; OnPropertyChanged(); } }
        private double _GhostMax = 0.05;

    }
}