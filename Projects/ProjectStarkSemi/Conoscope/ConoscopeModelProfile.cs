using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Special;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

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
        /// 最大角度范围（度）
        /// </summary>
        public int MaxAngle
        {
            get => _MaxAngle;
            set { _MaxAngle = value; OnPropertyChanged(); }
        }
        private int _MaxAngle = 60;

        /// <summary>
        /// 视场系数（像素/角度或角度/像素，按具体业务定义）
        /// </summary>
        public double ConoscopeCoefficient
        {
            get => _ConoscopeCoefficient;
            set { _ConoscopeCoefficient = value; OnPropertyChanged(); }
        }
        private double _ConoscopeCoefficient = 0.02840;

        /// <summary>
        /// 是否包含观察相机
        /// </summary>
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
        public double ObservationCameraScaleCoefficient
        {
            get => _ObservationCameraScaleCoefficient;
            set { _ObservationCameraScaleCoefficient = value; OnPropertyChanged(); }
        }
        private double _ObservationCameraScaleCoefficient = 0.02;

        /// <summary>
        /// 观察相机中心点 X 坐标（像素）
        /// </summary>
        public double ObservationCameraCenterX
        {
            get => _ObservationCameraCenterX;
            set { _ObservationCameraCenterX = value; OnPropertyChanged(); }
        }
        private double _ObservationCameraCenterX = 640;

        /// <summary>
        /// 观察相机中心点 Y 坐标（像素）
        /// </summary>
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
                    ConoscopeCoefficient = 0.02645,
                    HasObservationCamera = true,
                    CoordinateAxisParam = new ConoscopeCoordinateAxisParam
                    {
                        MaxAngle = 60,
                        ConoscopeCoefficient = 0.02645,
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
                    ConoscopeCoefficient = 0.022,
                    HasObservationCamera = false,
                    CoordinateAxisParam = new ConoscopeCoordinateAxisParam
                    {
                        MaxAngle = 80,
                        ConoscopeCoefficient = 0.022,
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
    }
}
