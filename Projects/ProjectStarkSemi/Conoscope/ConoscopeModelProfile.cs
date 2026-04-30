using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Special;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ProjectStarkSemi.Conoscope
{
    /// <summary>
    /// Conoscope 型号配置模型，包含每个型号特有的参数。
    /// 每个型号独立维护，全局运行时可按当前型号切换。
    /// </summary>
    public class ConoscopeModelProfile : ViewModelBase
    {
        /// <summary>
        /// 所属型号类型标识
        /// </summary>
        public ConoscopeModelType ModelType
        {
            get => _ModelType;
            set { _ModelType = value; OnPropertyChanged(); }
        }
        private ConoscopeModelType _ModelType = ConoscopeModelType.VA60;

        /// <summary>
        /// UI 显示名称
        /// </summary>
        public string DisplayName
        {
            get => _DisplayName;
            set { _DisplayName = value; OnPropertyChanged(); }
        }
        private string _DisplayName = "VA60";

        /// <summary>
        /// 视场角范围（度）
        /// </summary>
        [Category("型号"), DisplayName("视场角(度)"), Description("VA60 默认 60，VA80 默认 80。系数按 计算直径 / (视场角 * 2) 自动计算。")]
        public int MaxAngle
        {
            get => _MaxAngle;
            set { _MaxAngle = Math.Max(1, value); OnPropertyChanged(); OnPropertyChanged(nameof(ConoscopeCoefficient)); }
        }
        private int _MaxAngle = 60;

        /// <summary>
        /// 用于计算的图像直径（像素）。0 表示使用图像短边。
        /// </summary>
        [Category("型号"), DisplayName("计算直径(像素)"), Description("填 0 时使用当前图像短边；系数 = 计算直径 / (视场角 * 2)。")]
        public double CalculationDiameterPixels
        {
            get => _CalculationDiameterPixels;
            set { _CalculationDiameterPixels = Math.Max(0, value); OnPropertyChanged(); OnPropertyChanged(nameof(ConoscopeCoefficient)); }
        }
        private double _CalculationDiameterPixels;

        /// <summary>
        /// 视场系数（像素/度），由计算直径和视场角自动推导。
        /// </summary>
        [Browsable(false), JsonIgnore]
        public double ConoscopeCoefficient => CalculateConoscopeCoefficient(CalculationDiameterPixels, MaxAngle);

        /// <summary>
        /// 是否包含观察相机
        /// </summary>
        [Category("观察相机"), DisplayName("启用观察相机"), Description("VA60 默认启用，VA80 默认关闭。关闭后 Conoscope 窗口不会显示打开观察相机按钮。")]
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
        [Category("观察相机"), DisplayName("尺寸系数(mm/像素)"), Description("红圈像素直径 = 选择尺寸(mm) / 尺寸系数。例如 0.5mm / 0.02 = 25px。")]
        public double ObservationCameraScaleCoefficient
        {
            get => _ObservationCameraScaleCoefficient;
            set { _ObservationCameraScaleCoefficient = Math.Max(0, value); OnPropertyChanged(); }
        }
        private double _ObservationCameraScaleCoefficient = 0.02;

        /// <summary>
        /// 观察相机中心点 X 坐标（像素）
        /// </summary>
        [Category("观察相机"), DisplayName("中心点X(像素)"), Description("红色圆形在观察相机图像中的中心点 X 坐标。")]
        public double ObservationCameraCenterX
        {
            get => _ObservationCameraCenterX;
            set { _ObservationCameraCenterX = value; OnPropertyChanged(); }
        }
        private double _ObservationCameraCenterX = 640;

        /// <summary>
        /// 观察相机中心点 Y 坐标（像素）
        /// </summary>
        [Category("观察相机"), DisplayName("中心点Y(像素)"), Description("红色圆形在观察相机图像中的中心点 Y 坐标。")]
        public double ObservationCameraCenterY
        {
            get => _ObservationCameraCenterY;
            set { _ObservationCameraCenterY = value; OnPropertyChanged(); }
        }
        private double _ObservationCameraCenterY = 512;


        /// <summary>
        /// Conoscope 图像坐标轴参数（每个型号独立）
        /// </summary>
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

        public double GetConoscopeCoefficient(int imageWidth, int imageHeight)
        {
            return CalculateConoscopeCoefficient(GetCalculationDiameterPixels(imageWidth, imageHeight), MaxAngle);
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
