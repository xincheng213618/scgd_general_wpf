using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace Conoscope.Core
{
    /// <summary>
    /// Core 型号配置模型，包含每个型号特有的参数。
    /// 每个型号独立维护，全局运行时可按当前型号切换。
    /// </summary>
    public class ConoscopeModelProfile : ViewModelBase
    {
        /// <summary>
        /// 所属型号类型标识
        /// </summary>
        [Browsable(false)]
        public ConoscopeModelType ModelType
        {
            get => _ModelType;
            set { _ModelType = value; OnPropertyChanged(); }
        }
        private ConoscopeModelType _ModelType = ConoscopeModelType.VA60;

        /// <summary>
        /// UI 显示名称
        /// </summary>
        [Browsable(false)]
        public string DisplayName
        {
            get => _DisplayName;
            set { _DisplayName = value; OnPropertyChanged(); }
        }
        private string _DisplayName = "VA60";

        /// <summary>
        /// 视场角范围（度）
        /// </summary>
        [DisplayName("Con_Model_FOV"), Category("Con_Category_Model"), Description("Con_Model_FOV_Description")]
        public int MaxAngle
        {
            get => _MaxAngle;
            set { _MaxAngle = Math.Max(1, value); OnPropertyChanged(); OnPropertyChanged(nameof(ConoscopeCoefficient)); }
        }
        private int _MaxAngle = 60;

        /// <summary>
        /// 用于计算的图像直径（像素）。0 表示使用图像短边。
        /// </summary>
        [Browsable(false)]
        public double CalculationDiameterPixels
        {
            get => _CalculationDiameterPixels;
            set
            {
                _CalculationDiameterPixels = Math.Max(0, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullScalePixelCount));
                OnPropertyChanged(nameof(ConoscopeCoefficient));
            }
        }
        private double _CalculationDiameterPixels;

        /// <summary>
        /// 手动指定视场系数（像素/度）。0 表示按直径和视场角自动计算。
        /// </summary>
        [Browsable(false)]
        public double ManualConoscopeCoefficient
        {
            get => _ManualConoscopeCoefficient;
            set
            {
                _ManualConoscopeCoefficient = Math.Max(0, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(DirectConoscopeCoefficient));
                OnPropertyChanged(nameof(ConoscopeCoefficient));
            }
        }
        private double _ManualConoscopeCoefficient;

        [DisplayName("Con_Model_Pixels"), Category("Con_Category_Model"), Description("Con_Model_Pixels_Description"), JsonIgnore]
        public double FullScalePixelCount
        {
            get => CalculationDiameterPixels > 0 ? CalculationDiameterPixels / 2.0 : 0;
            set => CalculationDiameterPixels = value > 0 ? value * 2.0 : 0;
        }

        [DisplayName("Con_Model_Coefficient"), Category("Con_Category_Model"), Description("Con_Model_Coefficient_Description"), JsonIgnore]
        public double DirectConoscopeCoefficient
        {
            get => ManualConoscopeCoefficient > 0 ? 1.0 / ManualConoscopeCoefficient : 0;
            set => ManualConoscopeCoefficient = value > 0 ? 1.0 / value : 0;
        }

        /// <summary>
        /// 视场系数（像素/度），由计算直径和视场角自动推导。
        /// </summary>
        [Browsable(false), JsonIgnore]
        public double ConoscopeCoefficient => ResolveConoscopeCoefficient(CalculationDiameterPixels, MaxAngle, ManualConoscopeCoefficient);

        /// <summary>
        /// 是否包含观察相机
        /// </summary>
        [DisplayName("Con_Model_EnableObsCam"), Category("Con_Category_ObserveCam"), Description("Con_Model_EnableObsCam_Description")]
        public bool HasObservationCamera
        {
            get => _HasObservationCamera;
            set { _HasObservationCamera = value; OnPropertyChanged(); }
        }
        private bool _HasObservationCamera = true;


        /// <summary>
        /// 观察相机尺寸系数（mm/像素）
        /// 用于在观察相机图像上按 mm 尺寸画圆
        /// </summary>
        [DisplayName("Con_Model_SizeCoeff"), Category("Con_Category_ObserveCam"), Description("Con_Model_SizeCoeff_Description")]
        public double ObservationCameraScaleCoefficient
        {
            get => _ObservationCameraScaleCoefficient;
            set { _ObservationCameraScaleCoefficient = Math.Max(0, value); OnPropertyChanged(); }
        }
        private double _ObservationCameraScaleCoefficient = 0.02;

        /// <summary>
        /// 观察相机中心点 X 坐标（像素）
        /// </summary>
        [DisplayName("Con_Model_CenterX"), Category("Con_Category_ObserveCam"), Description("Con_Model_CenterX_Description")]
        public double ObservationCameraCenterX
        {
            get => _ObservationCameraCenterX;
            set { _ObservationCameraCenterX = value; OnPropertyChanged(); }
        }
        private double _ObservationCameraCenterX = 640;

        /// <summary>
        /// 观察相机中心点 Y 坐标（像素）
        /// </summary>
        [DisplayName("Con_Model_CenterY"), Category("Con_Category_ObserveCam"), Description("Con_Model_CenterY_Description")]
        public double ObservationCameraCenterY
        {
            get => _ObservationCameraCenterY;
            set { _ObservationCameraCenterY = value; OnPropertyChanged(); }
        }
        private double _ObservationCameraCenterY = 512;


        /// <summary>
        /// Core 图像坐标轴参数（每个型号独立）
        /// </summary>
        [Browsable(false)]
        public ConoscopeCoordinateAxisParam CoordinateAxisParam
        {
            get => _CoordinateAxisParam ??= new ConoscopeCoordinateAxisParam();
            set { _CoordinateAxisParam = value; OnPropertyChanged(); }
        }
        private ConoscopeCoordinateAxisParam _CoordinateAxisParam = new ConoscopeCoordinateAxisParam();

        /// <summary>
        /// 创建默认配置（静态工厂）
        /// </summary>
        public static ConoscopeModelProfile CreateDefault(ConoscopeModelType type)
        {
            return type switch
            {
                ConoscopeModelType.VA60 => new ConoscopeModelProfile
                {
                    ModelType = ConoscopeModelType.VA60,
                    DisplayName = "VA60",
                    MaxAngle = 60,
                    CalculationDiameterPixels = 0,
                    ManualConoscopeCoefficient = 0,
                    HasObservationCamera = true,
                    CoordinateAxisParam = new ConoscopeCoordinateAxisParam
                    {
                        MaxAngle = 60,
                        ReferenceAngle = 90,
                        ReferenceRadiusAngle = 30
                    },
                    ObservationCameraScaleCoefficient = 0.02,
                    ObservationCameraCenterX = 640,
                    ObservationCameraCenterY = 512
                },
                ConoscopeModelType.VA80 => new ConoscopeModelProfile
                {
                    ModelType = ConoscopeModelType.VA80,
                    DisplayName = "VA80",
                    MaxAngle = 80,
                    CalculationDiameterPixels = 0,
                    ManualConoscopeCoefficient = 0,
                    HasObservationCamera = false,
                    CoordinateAxisParam = new ConoscopeCoordinateAxisParam
                    {
                        MaxAngle = 80,
                        ReferenceAngle = 90,
                        ReferenceRadiusAngle = 40
                    },
                    ObservationCameraScaleCoefficient = 0.02,
                    ObservationCameraCenterX = 640,
                    ObservationCameraCenterY = 512
                },
                _ => new ConoscopeModelProfile { ModelType = type, DisplayName = type.ToString() }
            };
        }

        public double GetCalculationDiameterPixels(int imageWidth, int imageHeight)
        {
            if (CalculationDiameterPixels > 0)
            {
                return CalculationDiameterPixels;
            }

            return Math.Max(1, Math.Min(imageWidth, imageHeight));
        }

        public double GetFullScalePixelCount(int imageWidth, int imageHeight)
        {
            return GetCalculationDiameterPixels(imageWidth, imageHeight) / 2.0;
        }

        public double GetConoscopeCoefficient(int imageWidth, int imageHeight)
        {
            return ResolveConoscopeCoefficient(GetCalculationDiameterPixels(imageWidth, imageHeight), MaxAngle, ManualConoscopeCoefficient);
        }

        public static double ResolveConoscopeCoefficient(double calculationDiameterPixels, double maxAngle, double manualConoscopeCoefficient)
        {
            if (manualConoscopeCoefficient > 0)
            {
                return manualConoscopeCoefficient;
            }

            return CalculateConoscopeCoefficient(calculationDiameterPixels, maxAngle);
        }

        public static double CalculateConoscopeCoefficient(double calculationDiameterPixels, double maxAngle)
        {
            if (calculationDiameterPixels <= 0 || maxAngle <= 0)
            {
                return 0;
            }

            return calculationDiameterPixels / (maxAngle * 2.0);
        }
    }
}
