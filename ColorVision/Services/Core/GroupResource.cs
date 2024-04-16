using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Calibration.Templates;
using ColorVision.Services.Type;
using System.ServiceProcess;

namespace ColorVision.Services.Core
{
    public class GroupResource: BaseResource
    {
        public static GroupResource? AddGroupResource(ICalibrationService<BaseResourceObject> deviceService , string Name)
        {
            SysResourceModel sysResourceModel = new SysResourceModel() { Name = Name , Type = (int)Type.ServiceTypes.Group };
            sysResourceModel.Pid = deviceService.SysResourceModel.Id;
            sysResourceModel.TenantId = deviceService.SysResourceModel.TenantId;

            SysResourceDao.Instance.Save(sysResourceModel);

            int pkId = sysResourceModel.PKId;
            if (pkId > 0 && SysResourceDao.Instance.GetById(pkId) is SysResourceModel model)
            {
                GroupResource groupResource = new GroupResource(model);
                deviceService.AddChild(groupResource);
                return groupResource;
            }
            return null;
        }

        public GroupResource(SysResourceModel sysResourceModel):base(sysResourceModel)
        {
            SysResourceModel = sysResourceModel;
            Name = sysResourceModel.Name ?? sysResourceModel.Id.ToString();
        }

        public override void Save()
        {
            SysResourceModel.Name = Name;
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

        public void SetCalibrationResource(ICalibrationService<BaseResourceObject> calibrationService)
        {
            foreach (var item in VisualChildren)
            {
                if (item is CalibrationResource calibrationResource)
                {

                    switch ((ServiceTypes)calibrationResource.SysResourceModel.Type)
                    {
                        case ServiceTypes.DarkNoise:
                            foreach (var item1 in calibrationService.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    DarkNoise = calibrationResource1;
                                }
                            }
                            break;
                        case ServiceTypes.DefectPoint:
                            foreach (var item1 in calibrationService.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    DefectPoint = calibrationResource1;
                                }
                            }
                            break;
                        case ServiceTypes.DSNU:
                            foreach (var item1 in calibrationService.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    DSNU = calibrationResource1;
                                }
                            }
                            break;
                        case ServiceTypes.Uniformity:
                            foreach (var item1 in calibrationService.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    Uniformity = calibrationResource1;
                                }
                            }
                            break;
                        case ServiceTypes.Distortion:
                            foreach (var item1 in calibrationService.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    Distortion = calibrationResource1;
                                }
                            }
                            break;
                        case ServiceTypes.ColorShift:
                            foreach (var item1 in calibrationService.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    ColorShift = calibrationResource1;
                                }
                            }
                            break;
                        case ServiceTypes.Luminance:
                            foreach (var item1 in calibrationService.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    Luminance = calibrationResource1;
                                }
                            }
                            break;
                        case ServiceTypes.LumOneColor:
                            foreach (var item1 in calibrationService.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    LumOneColor = calibrationResource1;
                                }
                            }
                            break;
                        case ServiceTypes.LumFourColor:
                            foreach (var item1 in calibrationService.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    LumFourColor = calibrationResource1;
                                }
                            }
                            break;
                        case ServiceTypes.LumMultiColor:
                            foreach (var item1 in calibrationService.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id )
                                {
                                    LumMultiColor = calibrationResource1;
                                }
                            }
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
