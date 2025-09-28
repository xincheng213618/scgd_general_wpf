#pragma warning disable

using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ProjectARVRPro
{
    public class ObjectiveTestResultFix : ViewModelBase
    {
        [Category("W51")]
        public double W51HorizontalFieldOfViewAngle { get => _W51HorizontalFieldOfViewAngle; set { _W51HorizontalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51HorizontalFieldOfViewAngle = 1;
        [Category("W51")]
        public double W51VerticalFieldOfViewAngle { get => _W51VerticalFieldOfViewAngle; set { _W51VerticalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51VerticalFieldOfViewAngle = 1;
        [Category("W51")]
        public double W51DiagonalFieldOfViewAngle { get => _W51DiagonalFieldOfViewAngle; set { _W51DiagonalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51DiagonalFieldOfViewAngle = 1;
        [Category("W255")]
        public double W255LuminanceUniformity { get => _W255LuminanceUniformity; set { _W255LuminanceUniformity = value; OnPropertyChanged(); } }
        private double _W255LuminanceUniformity = 1;
        [Category("W255")]
        public double W255ColorUniformity { get => _W255ColorUniformity; set { _W255ColorUniformity = value; OnPropertyChanged(); } }
        private double _W255ColorUniformity = 1;
        [Category("W255")]
        public double W255CenterLunimance { get => _W255CenterLunimance; set { _W255CenterLunimance = value; OnPropertyChanged(); } }
        private double _W255CenterLunimance = 1;
        [Category("W255")]
        public double W255CenterCIE1931ChromaticCoordinatesx { get => _W255CenterCIE1931ChromaticCoordinatesx; set { _W255CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesx = 1;
        [Category("W255")]
        public double W255CenterCIE1931ChromaticCoordinatesy { get => _W255CenterCIE1931ChromaticCoordinatesy; set { _W255CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1931ChromaticCoordinatesy = 1;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesu { get => _W255CenterCIE1976ChromaticCoordinatesu; set { _W255CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesu = 1;
        [Category("W255")]
        public double W255CenterCIE1976ChromaticCoordinatesv { get => _W255CenterCIE1976ChromaticCoordinatesv; set { _W255CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private double _W255CenterCIE1976ChromaticCoordinatesv = 1;
        [Category("W255")]
        public double BlackCenterCorrelatedColorTemperature { get => _BlackCenterCorrelatedColorTemperature; set { _BlackCenterCorrelatedColorTemperature = value; OnPropertyChanged(); } }
        private double _BlackCenterCorrelatedColorTemperature = 1;

        [Category("Black")]
        public double FOFOContrast { get => _FOFOContrast; set { _FOFOContrast = value; OnPropertyChanged(); } }
        private double _FOFOContrast = 1;

        [Category("W25")]
        public double W25CenterLunimance { get => _W25CenterLunimance; set { _W25CenterLunimance = value; OnPropertyChanged(); } }
        private double _W25CenterLunimance = 1;
        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesx { get => _W25CenterCIE1931ChromaticCoordinatesx; set { _W25CenterCIE1931ChromaticCoordinatesx = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesx = 1;
        [Category("W25")]
        public double W25CenterCIE1931ChromaticCoordinatesy { get => _W25CenterCIE1931ChromaticCoordinatesy; set { _W25CenterCIE1931ChromaticCoordinatesy = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1931ChromaticCoordinatesy = 1;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesu { get => _W25CenterCIE1976ChromaticCoordinatesu; set { _W25CenterCIE1976ChromaticCoordinatesu = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesu = 1;
        [Category("W25")]
        public double W25CenterCIE1976ChromaticCoordinatesv { get => _W25CenterCIE1976ChromaticCoordinatesv; set { _W25CenterCIE1976ChromaticCoordinatesv = value; OnPropertyChanged(); } }
        private double _W25CenterCIE1976ChromaticCoordinatesv = 1;

        [Category("Chessboard")]
        public double ChessboardContrast { get => _ChessboardContrast; set { _ChessboardContrast = value; OnPropertyChanged(); } }
        private double _ChessboardContrast = 1;


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

        [Category("Distortion")]
        public double HorizontalTVDistortion { get => _HorizontalTVDistortion; set { _HorizontalTVDistortion = value; OnPropertyChanged(); } }
        private double _HorizontalTVDistortion = 1;
        [Category("Distortion")]
        public double VerticalTVDistortion { get => _VerticalTVDistortion; set { _VerticalTVDistortion = value; OnPropertyChanged(); } }
        private double _VerticalTVDistortion = 1;

        [Category("OpticCenter")]
        public double ImageCenterXTilt { get => _ImageCenterXTilt; set { _ImageCenterXTilt = value; OnPropertyChanged(); } }
        private double _ImageCenterXTilt = 1;

        [Category("OpticCenter")]
        public double ImageCenterYTilt { get => _ImageCenterYTilt; set { _ImageCenterYTilt = value; OnPropertyChanged(); } }
        private double _ImageCenterYTilt = 1;

        [Category("OpticCenter")]
        public double ImageCenterRotation { get => _ImageCenterRotation; set { _ImageCenterRotation = value; OnPropertyChanged(); } }
        private double _ImageCenterRotation = 1;

        [Category("OpticCenter")]
        public double OptCenterRotation { get => _OptCenterRotation; set { _OptCenterRotation = value; OnPropertyChanged(); } }
        private double _OptCenterRotation = 1;

        [Category("OpticCenter")]
        public double OptCenterXTilt { get => _OptCenterXTilt; set { _OptCenterXTilt = value; OnPropertyChanged(); } }
        private double _OptCenterXTilt = 1;

        [Category("OpticCenter")]
        public double OptCenterYTilt { get => _OptCenterYTilt; set { _OptCenterYTilt = value; OnPropertyChanged(); } }
        private double _OptCenterYTilt = 1;

        public double Ghost { get => _Ghost; set { _Ghost = value; OnPropertyChanged(); } }
        private double _Ghost = 1;
    }

}