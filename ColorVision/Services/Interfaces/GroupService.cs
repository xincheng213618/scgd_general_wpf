#pragma warning disable  CS8604,CS8631
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Devices.Camera.Calibrations;
using ColorVision.Services.Interfaces;

namespace ColorVision.Services.Devices
{
    public class GroupService: BaseResourceObject
    {
        public static GroupService? AddGroupService(DeviceService deviceService , string Name)
        {
            SysResourceModel sysResourceModel = new SysResourceModel() { Name = Name ,Type = (int)ResourceType.Group };
            sysResourceModel.Pid = deviceService.SysResourceModel.Id;
            sysResourceModel.TenantId = deviceService.SysResourceModel.TenantId;

            SysResourceDao sysResourceDao = new SysResourceDao();
            sysResourceDao.Save(sysResourceModel);

            int pkId = sysResourceModel.GetPK();
            if (pkId > 0 && sysResourceDao.GetById(pkId) is SysResourceModel model)
            {
                GroupService groupService = new GroupService(model);
                deviceService.AddChild(groupService);
                return groupService;
            }
            return null;
        }

        public GroupService(SysResourceModel sysResourceModel)
        {
            SysResourceModel = sysResourceModel;
            Name = sysResourceModel.Name ?? sysResourceModel.Id.ToString();
        }

        SysResourceDao SysResourceDao = new SysResourceDao();

        public override void Save()
        {
            SysResourceModel.Name = Name;
            SysResourceDao.Save(SysResourceModel);

            ///这里后面再优化，先全部删除在添加
            SysResourceDao.DeleteGroupRelate(SysResourceModel.Id);

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
                    SysResourceDao.ADDGroup(SysResourceModel.Id, calibrationResource.SysResourceModel.Id);
                }
            }
            base.Save();
        }

        public void SetCalibrationResource(DeviceCamera deviceCamera)
        {
            foreach (var item in VisualChildren)
            {
                if (item is CalibrationResource calibrationResource)
                {

                    switch ((ResouceType)calibrationResource.SysResourceModel.Type)
                    {
                        case ResouceType.DarkNoise:
                            foreach (var item1 in deviceCamera.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    DarkNoise = calibrationResource1;
                                }
                            }
                            break;
                        case ResouceType.DefectPoint:
                            foreach (var item1 in deviceCamera.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    DefectPoint = calibrationResource1;
                                }
                            }
                            break;
                        case ResouceType.DSNU:
                            foreach (var item1 in deviceCamera.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    DSNU = calibrationResource1;
                                }
                            }
                            break;
                        case ResouceType.Uniformity:
                            foreach (var item1 in deviceCamera.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    Uniformity = calibrationResource1;
                                }
                            }
                            break;
                        case ResouceType.Distortion:
                            foreach (var item1 in deviceCamera.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    Distortion = calibrationResource1;
                                }
                            }
                            break;
                        case ResouceType.ColorShift:
                            foreach (var item1 in deviceCamera.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    ColorShift = calibrationResource1;
                                }
                            }
                            break;
                        case ResouceType.Luminance:
                            foreach (var item1 in deviceCamera.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    Luminance = calibrationResource1;
                                }
                            }
                            break;
                        case ResouceType.LumOneColor:
                            foreach (var item1 in deviceCamera.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    LumOneColor = calibrationResource1;
                                }
                            }
                            break;
                        case ResouceType.LumFourColor:
                            foreach (var item1 in deviceCamera.VisualChildren)
                            {
                                if (item1 is CalibrationResource calibrationResource1 && calibrationResource.Id == calibrationResource1.Id)
                                {
                                    LumFourColor = calibrationResource1;
                                }
                            }
                            break;
                        case ResouceType.LumMultiColor:
                            foreach (var item1 in deviceCamera.VisualChildren)
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

        public SysResourceModel SysResourceModel { get; set; }

        public override void AddChild(BaseResourceObject baseObject)
        {
            base.AddChild(baseObject);
        }

        public override void RemoveChild(BaseResourceObject baseObject)
        {
            base.RemoveChild(baseObject);
        }
    }
}
