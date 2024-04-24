using ColorVision.Common.MVVM;
using ColorVision.Services.Dao;
using ColorVision.Services.PhyCameras.Templates;
using ColorVision.Services.Type;
using Newtonsoft.Json;
using System.Windows;

namespace ColorVision.Services.Core
{
    public class ConfigGroup :ViewModelBase
    {
        public int Gain { get => _Gain; set { _Gain = value; NotifyPropertyChanged(); } }
        private int _Gain;

        public int ExpTime { get => _ExpTime; set { _ExpTime = value; NotifyPropertyChanged(); } }
        private int _ExpTime = 10;

        public double Aperturein { get => _Aperturein; set { _Aperturein = value; NotifyPropertyChanged(); } }
        private double _Aperturein;

        public double ND { get => _ND; set { _ND = value; NotifyPropertyChanged(); } }
        private double _ND;

        public double ShotType { get => _ShotType; set { _ShotType = value; NotifyPropertyChanged(); } }
        private double _ShotType;

        public double Focallength { get => _Focallength; set { _Focallength = value; NotifyPropertyChanged(); } }
        private double _Focallength;

        public double GetImgMode { get => _GetImgMode; set { _GetImgMode = value; NotifyPropertyChanged(); } }
        private double _GetImgMode;

        public double ImgBpp { get => _ImgBpp; set { _ImgBpp = value; NotifyPropertyChanged(); } }
        private double _ImgBpp; 
    }

    public class GroupResource: BaseFileResource
    {
        public static GroupResource? AddGroupResource(ICalibrationService<BaseResourceObject> deviceService , string Name)
        {
            SysResourceModel sysResourceModel = new SysResourceModel() { Name = Name , Type = (int)Type.ServiceTypes.Group };
            sysResourceModel.Pid = deviceService.SysResourceModel.Id;
            sysResourceModel.TenantId = deviceService.SysResourceModel.TenantId;

            SysResourceDao.Instance.Save(sysResourceModel);

            int pkId = sysResourceModel.Id;
            if (pkId > 0 && SysResourceDao.Instance.GetById(pkId) is SysResourceModel model)
            {
                GroupResource groupResource = new GroupResource(model);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    deviceService.AddChild(groupResource);
                });
                return groupResource;
            }
            return null;
        }

        public ConfigGroup Config { get; set; }


        public GroupResource(SysResourceModel sysResourceModel):base(sysResourceModel)
        {
            SysResourceModel = sysResourceModel;
            Name = sysResourceModel.Name ?? sysResourceModel.Id.ToString();

            Config = BaseResourceObjectExtensions.TryDeserializeConfig<ConfigGroup>(SysResourceModel.Value);
        }

        public override void Save()
        {
            SysResourceModel.Name = Name;
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            SysResourceDao.Instance.Save(SysResourceModel);

            ///这里后面再优化，先全部删除在添加
            SysResourceDao.Instance.DeleteGroupRelate(SysResourceModel.Id);

            VisualChildren.Clear();
            VisualChildren.Add(DarkNoise);
            VisualChildren.Add(DSNU);
            VisualChildren.Add(DefectPoint);
            VisualChildren.Add(Uniformity);
            VisualChildren.Add(Distortion);
            VisualChildren.Add(ColorShift);
            VisualChildren.Add(Luminance);
            VisualChildren.Add(LumOneColor);
            VisualChildren.Add(LumFourColor);
            VisualChildren.Add(LumMultiColor);

            foreach (var item in VisualChildren)
            {
                if (item is CalibrationResource calibrationResource)
                {
                    SysResourceDao.Instance.ADDGroup(SysResourceModel.Id, calibrationResource.SysResourceModel.Id);
                }
            }
            base.Save();
        }

        public void SetCalibrationResource()
        {
            foreach (var item in VisualChildren)
            {
                if (item is CalibrationResource calibrationResource)
                {
                    switch ((ServiceTypes)calibrationResource.SysResourceModel.Type)
                    {
                        case ServiceTypes.DarkNoise:
                            DarkNoise = calibrationResource;
                            break;
                        case ServiceTypes.DefectPoint:
                            DefectPoint = calibrationResource;
                            break;
                        case ServiceTypes.DSNU:
                            DSNU = calibrationResource;
                            break;
                        case ServiceTypes.Uniformity:
                            Uniformity = calibrationResource;
                            break;
                        case ServiceTypes.Distortion:
                            Distortion = calibrationResource;
                            break;
                        case ServiceTypes.ColorShift:
                            ColorShift = calibrationResource;
                            break;
                        case ServiceTypes.Luminance:
                            Luminance = calibrationResource;
                            break;
                        case ServiceTypes.LumOneColor:
                            LumOneColor = calibrationResource;
                            break;
                        case ServiceTypes.LumFourColor:
                            LumFourColor = calibrationResource;
                            break;
                        case ServiceTypes.LumMultiColor:
                            LumMultiColor = calibrationResource;
                            break;
                        default:
                            break;
                    }
                }
            }
        }


        public CalibrationResource DarkNoise { get => _DarkNoise;set { _DarkNoise = value;  NotifyPropertyChanged(); } }
        private CalibrationResource _DarkNoise;
        public CalibrationResource DefectPoint { get => _DefectPoint; set { _DefectPoint = value; NotifyPropertyChanged(); } }
        private CalibrationResource _DefectPoint;
        public CalibrationResource DSNU { get => _DSNU; set { _DSNU = value; NotifyPropertyChanged(); } }
        private CalibrationResource _DSNU;
        public CalibrationResource Uniformity { get => _Uniformity; set { _Uniformity = value; NotifyPropertyChanged(); } }
        private CalibrationResource _Uniformity;
        public CalibrationResource Distortion { get => _Distortion; set { _Distortion = value; NotifyPropertyChanged(); } }
        private CalibrationResource _Distortion;
        public CalibrationResource ColorShift { get => _ColorShift; set { _ColorShift = value; NotifyPropertyChanged(); } }
        private CalibrationResource _ColorShift;
        public CalibrationResource Luminance { get => _Luminance; set { _Luminance = value; NotifyPropertyChanged(); } }
        private CalibrationResource _Luminance;
        public CalibrationResource LumOneColor { get => _LumOneColor; set { _LumOneColor = value; NotifyPropertyChanged(); } }
        private CalibrationResource _LumOneColor;
        public CalibrationResource LumFourColor { get => _LumFourColor; set { _LumFourColor = value; NotifyPropertyChanged(); } }
        private CalibrationResource _LumFourColor;
        public CalibrationResource LumMultiColor { get => _LumMultiColor; set { _LumMultiColor = value; NotifyPropertyChanged(); } }
        private CalibrationResource _LumMultiColor;
    }
}
