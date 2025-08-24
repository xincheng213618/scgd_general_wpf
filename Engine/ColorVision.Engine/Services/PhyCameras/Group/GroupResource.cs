using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Types;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.PhyCameras.Group
{

    public class ConfigGroup : ViewModelBase
    {
        public bool IsInit { get => _IsInit; set { _IsInit = value; NotifyPropertyChanged(); } }
        private bool _IsInit;

        /// <summary>
        /// 增益
        /// </summary>
        public int Gain { get => _Gain; set { _Gain = value; NotifyPropertyChanged(); } }
        private int _Gain;

        public int ExpTime { get => _ExpTime; set { _ExpTime = value; NotifyPropertyChanged(); } }
        private int _ExpTime = 10;

        /// <summary>
        /// 光圈 
        /// </summary>
        public double Aperture{ get => _Aperture; set { _Aperture = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ApertureShow)); } }
        private double _Aperture;

        public string ApertureShow { get => $"F{_Aperture}";  set { } } 

        /// <summary>
        ///  ND 滤镜
        /// </summary>
        public double ND { get => _ND; set { _ND = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(NDShow)); } }
        private double _ND;
        public string NDShow { get => $"ND{_ND}"; set { } }


        public double ShotType { get => _ShotType; set { _ShotType = value; NotifyPropertyChanged(); } }
        private double _ShotType;

        /// <summary>
        /// 焦距
        /// </summary>
        public double FocalLength { get => _FocalLength; set { _FocalLength = value; NotifyPropertyChanged(); } }
        private double _FocalLength = 95;
        public string FocalLengthShow { get => $"{_FocalLength}mm"; set { } }

        /// <summary>
        /// 对焦距离 
        /// </summary>
        public double? FocusDistance { get => _FocusDistance; set { _FocusDistance = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(FocusDistanceShow)); } }
        private double? _FocusDistance;
        public string FocusDistanceShow { get => $"{_FocusDistance}m"; set { } } 



        public double GetImgMode { get => _GetImgMode; set { _GetImgMode = value; NotifyPropertyChanged(); } }
        private double _GetImgMode;

        public double ImgBpp { get => _ImgBpp; set { _ImgBpp = value; NotifyPropertyChanged(); } }
        private double _ImgBpp;
    }

    public class GroupResource : ServiceFileBase, IEditable
    {
        public static void LoadgroupResource(GroupResource groupResource)
        {
            List<SysResourceModel> sysResourceModels = SysResourceDao.Instance.GetGroupResourceItems(groupResource.SysResourceModel.Id);
            foreach (var sysResourceModel in sysResourceModels)
            {
                if (sysResourceModel.Type == (int)ServiceTypes.Group)
                {
                    GroupResource groupResource1 = new(sysResourceModel);
                    LoadgroupResource(groupResource1);
                    groupResource.AddChild(groupResource);
                }
                else if (30 <= sysResourceModel.Type && sysResourceModel.Type <= 40)
                {
                    CalibrationResource calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                    groupResource.AddChild(calibrationResource);
                }
                else
                {
                    ServiceBase calibrationResource = new(sysResourceModel);
                    groupResource.AddChild(calibrationResource);
                }
            }
            groupResource.SetCalibrationResource();
        }

        public static GroupResource? AddGroupResource(PhyCamera deviceService, string Name)
        {
            SysResourceModel sysResourceModel = new() { Name = Name, Type = (int)ServiceTypes.Group };
            sysResourceModel.Pid = deviceService.SysResourceModel.Id;
            sysResourceModel.TenantId = deviceService.SysResourceModel.TenantId;

            SysResourceDao.Instance.Save(sysResourceModel);

            int pkId = sysResourceModel.Id;
            if (pkId > 0 && SysResourceDao.Instance.GetById(pkId) is SysResourceModel model)
            {
                GroupResource groupResource = new(model);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    deviceService.AddChild(groupResource);
                });
                return groupResource;
            }
            return null;
        }

        public ConfigGroup Config { get; set; }

        public virtual bool IsEditMode { get => _IsEditMode; set { _IsEditMode = value; NotifyPropertyChanged(); } }
        private bool _IsEditMode;

        public RelayCommand ReNameCommand { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public GroupResource(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            SysResourceModel = sysResourceModel;
            Name = sysResourceModel.Name ?? sysResourceModel.Id.ToString();
            ReNameCommand = new RelayCommand(a => IsEditMode = true);
            Config = ServiceObjectBaseExtensions.TryDeserializeConfig<ConfigGroup>(SysResourceModel.Value);
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.MenuRename, InputGestureText = "F2", Command = ReNameCommand });
            ContextMenu.Items.Add( new MenuItem() { Header = Properties.Resources.Delete, Command = ApplicationCommands.Delete });
            ContextMenu.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = true));
        }

        public override void Delete()
        {
            this.Parent?.RemoveChild(this);
            SysResourceDao.Instance.DeleteById(this.Id);
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
            VisualChildren.Add(LineArity);
            VisualChildren.Add(ColorDiff);
            VisualChildren.Add(Luminance);
            VisualChildren.Add(LumOneColor);
            VisualChildren.Add(LumFourColor);
            VisualChildren.Add(LumMultiColor);


            foreach (var item in VisualChildren.OfType<CalibrationResource>())
            {
                SysResourceDao.Instance.ADDGroup(SysResourceModel.Id, item.SysResourceModel.Id);
            }
            base.Save();
        }
        public void SetCalibrationResource()
        {
            foreach (var item in VisualChildren.OfType<CalibrationResource>())
            {
                switch ((ServiceTypes)item.SysResourceModel.Type)
                {
                    case ServiceTypes.DarkNoise:
                        DarkNoise = item;
                        break;
                    case ServiceTypes.DefectPoint:
                        DefectPoint = item;
                        break;
                    case ServiceTypes.DSNU:
                        DSNU = item;
                        break;
                    case ServiceTypes.Uniformity:
                        Uniformity = item;
                        break;
                    case ServiceTypes.Distortion:
                        Distortion = item;
                        break;
                    case ServiceTypes.ColorShift:
                        ColorShift = item;
                        break;
                    case ServiceTypes.Luminance:
                        Luminance = item;
                        break;
                    case ServiceTypes.LumOneColor:
                        LumOneColor = item;
                        break;
                    case ServiceTypes.LumFourColor:
                        LumFourColor = item;
                        break;
                    case ServiceTypes.LumMultiColor:
                        LumMultiColor = item;
                        break;
                    case ServiceTypes.ColorDiff:
                        ColorDiff = item;
                        break;
                    case ServiceTypes.LineArity:
                        LineArity = item;
                        break;
                    default:
                        break;
                }

            }

            if (!Config.IsInit)
            {
                Config.IsInit = true;
                if (Uniformity != null)
                {
                    ParseFileName(Uniformity.Name);
                }
            }
        }


        public CalibrationResource DarkNoise { get => _DarkNoise; set { _DarkNoise = value; NotifyPropertyChanged(); } }
        private CalibrationResource _DarkNoise;
        public CalibrationResource DefectPoint { get => _DefectPoint; set { _DefectPoint = value; NotifyPropertyChanged(); } }
        private CalibrationResource _DefectPoint;
        public CalibrationResource DSNU { get => _DSNU; set { _DSNU = value; NotifyPropertyChanged(); } }
        private CalibrationResource _DSNU;
        public CalibrationResource Uniformity { get => _Uniformity; set { _Uniformity = value; NotifyPropertyChanged();  } }
        private CalibrationResource _Uniformity;
        public CalibrationResource Distortion { get => _Distortion; set { _Distortion = value; NotifyPropertyChanged(); } }
        private CalibrationResource _Distortion;
        public CalibrationResource ColorShift { get => _ColorShift; set { _ColorShift = value; NotifyPropertyChanged(); } }
        private CalibrationResource _ColorShift;
        public CalibrationResource LineArity { get => _LineArity; set { _LineArity = value; NotifyPropertyChanged(); } }
        private CalibrationResource _LineArity;
        public CalibrationResource ColorDiff { get => _ColorDiff; set { _ColorDiff = value; NotifyPropertyChanged(); } }
        private CalibrationResource _ColorDiff;
        
        public CalibrationResource Luminance { get => _Luminance; set { _Luminance = value; NotifyPropertyChanged(); } }
        private CalibrationResource _Luminance;
        public CalibrationResource LumOneColor { get => _LumOneColor; set { _LumOneColor = value; NotifyPropertyChanged(); } }
        private CalibrationResource _LumOneColor;
        public CalibrationResource LumFourColor { get => _LumFourColor; set { _LumFourColor = value; NotifyPropertyChanged(); } }
        private CalibrationResource _LumFourColor;
        public CalibrationResource LumMultiColor { get => _LumMultiColor; set {    _LumMultiColor = value;  NotifyPropertyChanged(); } }
        private CalibrationResource _LumMultiColor;

        

        public void ParseFileName(string fileName)
        {
            // 提取焦距并转换为 double
            var focalLengthMatch = Regex.Match(fileName, @"(\d+)mm");
            if (focalLengthMatch.Success && double.TryParse(focalLengthMatch.Groups[1].Value, out double focalLength))
            {
                Config.FocalLength = focalLength;
            }
            // 提取 ND 滤镜（如果有），并转换为 int
            var ndFilterMatch = Regex.Match(fileName, @"ND(\d+)");
            if (ndFilterMatch.Success && int.TryParse(ndFilterMatch.Groups[1].Value, out int ndFilter))
            {
                Config.ND = ndFilter;
            }
            // 提取光圈并转换为 double
            var apertureMatch = Regex.Match(fileName, @"F(\d+\.?\d*)");
            if (apertureMatch.Success && double.TryParse(apertureMatch.Groups[1].Value, out double aperture))
            {
                Config.Aperture = aperture;
            }
            // 提取对焦距离并转换为 double
            var focusDistanceMatch = Regex.Match(fileName, @"F\d+\.?\d*_?(\d+\.?\d*)m");
            if (focusDistanceMatch.Success && double.TryParse(focusDistanceMatch.Groups[1].Value, out double focusDistance))
            {
                Config.FocusDistance = focusDistance;
            }
            // 提取增益并转换为 double
            var gainMatch = Regex.Match(fileName, @"(\d+\.?\d*)gain");
            if (gainMatch.Success && int.TryParse(gainMatch.Groups[1].Value, out int gain))
            {
                Config.Gain = gain;
            }
        }
    }
}
