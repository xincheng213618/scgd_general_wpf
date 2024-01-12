#pragma warning disable CS8603,CS0649
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.Sorts;
using ColorVision.Templates;
using cvColorVision;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;

namespace ColorVision.Services.Device.Camera.Calibrations
{

    public enum ResouceType
    {
        [Description("暗噪声")]
        DarkNoise = 31,
        [Description("缺陷点")]
        DefectPoint = 32,
        [Description("DSNU")]
        DSNU = 33,
        [Description("均匀场")]
        Uniformity = 34,
        [Description("畸变")]
        Distortion = 35,
        [Description("色偏")]
        ColorShift = 36,
        [Description("亮度")]
        Luminance = 37,
        [Description("单色")]
        LumOneColor = 38,
        [Description("四色")]
        LumFourColor = 39,
        [Description("多色")]
        LumMultiColor = 40,
        [Description("校正压缩文件")]
        ColorVisionCalibration = 1001
    }



    public class CalibrationRsource : ViewModelBase, ISortID, ISortName, ISortFilePath
    {
        public SysResourceModel SysResourceModel { get; set; }
        public CalibrationRsource(SysResourceModel SysResourceModel)
        {
            this.SysResourceModel = SysResourceModel;
            Name = SysResourceModel.Name;
            FilePath = SysResourceModel.Value;
            Id = SysResourceModel.Id;
        }

        public string? Name { get; set; }
        public string? FilePath { get; set; }
        public int Id { get; set; }
        public int Pid { get; set; }
    }


    public class CalibrationRsourceService
    {
        private static CalibrationRsourceService _instance;
        private static readonly object _locker = new();
        public static CalibrationRsourceService GetInstance() { lock (_locker) { return _instance ??= new CalibrationRsourceService(); } }

        private SysResourceDao resourceDao;

        public CalibrationRsourceService()
        {
            resourceDao = new SysResourceDao();
            DarkNoiseList = new List<string>();
            DefectPointList = new List<string>();
            DSNUList = new List<string>();
            UniformityList = new List<string>();
            DistortionList = new List<string>();
            ColorShiftList = new List<string>();
            LuminanceList = new List<string>();
            LumOneColorList = new List<string>();
            LumFourColorList = new List<string>();
            LumMultiColorList = new List<string>();
            CalibrationModeList = new List<string>();
        }







        public ObservableCollection<CalibrationRsource> GetAllCalibrationRsources(ResouceType resouceType, int id)
        {
            ObservableCollection<CalibrationRsource> ObservableCollections = new ObservableCollection<CalibrationRsource>();
            var resouces = resourceDao.GetAllTypeCamera((int)resouceType, id);
            foreach (var item in resouces)
            {
                ObservableCollections.Add(new CalibrationRsource(item));
            }
            return ObservableCollections;
        }

        private void GetCalibrationRsourceList(List<string> strings, ResouceType resouceType)
        {
            strings.Clear();
            var resouces = resourceDao.GetAllType((int)resouceType);
            foreach (var item in resouces)
            {
                strings.Add(item.Name ?? string.Empty);
            }
        }

        public int Delete(int id) => resourceDao.DeleteById(id);

        public int Save(SysResourceModel value) => resourceDao.Save(value);

        public void Refresh()
        {
            GetCalibrationRsourceList(DarkNoiseList, ResouceType.DarkNoise);
            GetCalibrationRsourceList(DefectPointList, ResouceType.DefectPoint);
            GetCalibrationRsourceList(DSNUList, ResouceType.DSNU);
            GetCalibrationRsourceList(UniformityList, ResouceType.Uniformity);
            GetCalibrationRsourceList(DistortionList, ResouceType.Distortion);
            GetCalibrationRsourceList(ColorShiftList, ResouceType.ColorShift);
            GetCalibrationRsourceList(LuminanceList, ResouceType.Luminance);
            GetCalibrationRsourceList(LumOneColorList, ResouceType.LumOneColor);
            GetCalibrationRsourceList(LumFourColorList, ResouceType.LumFourColor);
            GetCalibrationRsourceList(LumMultiColorList, ResouceType.LumMultiColor);
            GetCalibrationRsourceList(CalibrationModeList, ResouceType.ColorVisionCalibration);
        }


        public List<string> DarkNoiseList { get; set; }
        public List<string> DefectPointList { get; set; }
        public List<string> DSNUList { get; set; }
        public List<string> UniformityList { get; set; }
        public List<string> DistortionList { get; set; }
        public List<string> ColorShiftList { get; set; }
        public List<string> LuminanceList { get; set; }
        public List<string> LumOneColorList { get; set; }
        public List<string> LumFourColorList { get; set; }
        public List<string> LumMultiColorList { get; set; }
        public List<string> CalibrationModeList { get; set; }

    }


    public class CalibrationBase : ModelBase
    {
        public RelayCommand SelectFileCommand { get; set; }
        public CalibrationBase(List<ModDetailModel> detail, string propertyName = "") : base(detail)
        {
            SelectFileCommand = new RelayCommand((s) =>
            {
                using var dialog = new System.Windows.Forms.OpenFileDialog();
                dialog.Filter = "DAT|*.dat||";
                dialog.RestoreDirectory = true;
                dialog.FilterIndex = 1;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FilePath = dialog.FileName;
                }
            });
            this.propertyName = propertyName;
        }

        private string propertyName = string.Empty;

        public string FilePath { get { if (string.IsNullOrWhiteSpace(propertyName)) return GetValue(_FilePath); else return GetValue(_FilePath, propertyName); } set { if (string.IsNullOrWhiteSpace(propertyName)) { SetProperty(ref _FilePath, value); } else { SetProperty(ref _FilePath, value, propertyName); NotifyPropertyChanged(); } } }
        private string _FilePath;

        public bool IsSelected { get { if (string.IsNullOrWhiteSpace(propertyName + "IsSelected")) return GetValue(_IsSelected); else return GetValue(_IsSelected, propertyName + "IsSelected"); } set { if (string.IsNullOrWhiteSpace(propertyName + "IsSelected")) SetProperty(ref _IsSelected, value); else SetProperty(ref _IsSelected, value, propertyName + "IsSelected"); NotifyPropertyChanged(); } }
        private bool _IsSelected;
    }

    public class CalibrationNormal
    {

        public CalibrationNormal(List<ModDetailModel> detail, string Type)
        {
            DarkNoiseList = CalibrationRsourceService.GetInstance().DarkNoiseList;
            DefectPointList = CalibrationRsourceService.GetInstance().DefectPointList;
            DSNUList = CalibrationRsourceService.GetInstance().DSNUList;
            UniformityList = CalibrationRsourceService.GetInstance().UniformityList;
            DistortionList = CalibrationRsourceService.GetInstance().DistortionList;
            ColorShiftList = CalibrationRsourceService.GetInstance().ColorShiftList;

            DarkNoise = new CalibrationBase(detail, nameof(DarkNoise) + Type);
            DefectPoint = new CalibrationBase(detail, nameof(DefectPoint) + Type);
            DSNU = new CalibrationBase(detail, nameof(DSNU) + Type);
            Uniformity = new CalibrationBase(detail, nameof(Uniformity) + Type);
            Distortion = new CalibrationBase(detail, nameof(Distortion) + Type);
            ColorShift = new CalibrationBase(detail, nameof(ColorShift) + Type);
        }

        public List<string> DarkNoiseList { get; set; }
        public CalibrationBase DarkNoise { get; set; }
        public List<string> DefectPointList { get; set; }
        public CalibrationBase DefectPoint { get; set; }
        public List<string> DSNUList { get; set; }
        public CalibrationBase DSNU { get; set; }
        public List<string> UniformityList { get; set; }
        public CalibrationBase Uniformity { get; set; }
        public List<string> DistortionList { get; set; }
        public CalibrationBase Distortion { get; set; }
        public List<string> ColorShiftList { get; set; }
        public CalibrationBase ColorShift { get; set; }

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
            if (DarkNoise.IsSelected)
                keyValuePairs.Add(nameof(DarkNoise), DarkNoise.FilePath);
            if (DefectPoint.IsSelected)
                keyValuePairs.Add(nameof(DefectPoint), DefectPoint.FilePath);
            if (DSNU.IsSelected)
                keyValuePairs.Add(nameof(DSNU), DSNU.FilePath);
            if (Uniformity.IsSelected)
                keyValuePairs.Add(nameof(Uniformity), Uniformity.FilePath);
            if (Distortion.IsSelected)
                keyValuePairs.Add(nameof(Distortion), Distortion.FilePath);
            if (ColorShift.IsSelected)
                keyValuePairs.Add(nameof(ColorShift), ColorShift.FilePath);
            return keyValuePairs;
        }
    }

    public class CalibrationColor
    {

        public CalibrationColor(List<ModDetailModel> detail)
        {
            Luminance = new CalibrationBase(detail, nameof(Luminance));
            LumOneColor = new CalibrationBase(detail, nameof(LumOneColor));
            LumFourColor = new CalibrationBase(detail, nameof(LumFourColor));
            LumMultiColor = new CalibrationBase(detail, nameof(LumMultiColor));

            Luminance.PropertyChanged += (s, e) =>
            {
                if (Luminance.IsSelected)
                {
                    LumOneColor.IsSelected = false;
                    LumFourColor.IsSelected = false;
                    LumMultiColor.IsSelected = false;
                }
            };
            LumOneColor.PropertyChanged += (s, e) =>
            {
                if (LumOneColor.IsSelected)
                {
                    Luminance.IsSelected = false;
                    LumFourColor.IsSelected = false;
                    LumMultiColor.IsSelected = false;
                }
            };
            LumFourColor.PropertyChanged += (s, e) =>
            {
                if (LumFourColor.IsSelected)
                {
                    Luminance.IsSelected = false;
                    LumOneColor.IsSelected = false;
                    LumMultiColor.IsSelected = false;
                }
            };
            LumMultiColor.PropertyChanged += (s, e) =>
            {
                if (LumMultiColor.IsSelected)
                {
                    Luminance.IsSelected = false;
                    LumFourColor.IsSelected = false;
                    LumOneColor.IsSelected = false;
                }
            };

            LuminanceList = CalibrationRsourceService.GetInstance().LuminanceList;
            LumOneColorList = CalibrationRsourceService.GetInstance().LumOneColorList;
            LumFourColorList = CalibrationRsourceService.GetInstance().LumFourColorList;
            LumMultiColorList = CalibrationRsourceService.GetInstance().LumMultiColorList;
        }

        public CalibrationType CalibrationType
        {
            get
            {
                if (Luminance.IsSelected)
                    return CalibrationType.Luminance;
                else if (LumOneColor.IsSelected)
                    return CalibrationType.LumOneColor;
                else if (LumFourColor.IsSelected)
                    return CalibrationType.LumFourColor;
                else if (LumMultiColor.IsSelected)
                    return CalibrationType.LumMultiColor;
                else
                    return CalibrationType.Empty_Num;
            }
        }
        public List<string> LuminanceList { get; set; }
        public CalibrationBase Luminance { get; set; }
        public List<string> LumOneColorList { get; set; }
        public CalibrationBase LumOneColor { get; set; }

        public List<string> LumFourColorList { get; set; }
        public CalibrationBase LumFourColor { get; set; }

        public List<string> LumMultiColorList { get; set; }
        public CalibrationBase LumMultiColor { get; set; }
    }

    public class CalibrationParam : ParamBase
    {
        public string CalibrationMode { get { return GetValue(_CalibrationMode); } set {  SetProperty(ref _CalibrationMode, value);  } }
        private string _CalibrationMode;
        public List<string> CalibrationModeList { get; set; }
        public CalibrationNormal Normal { get; set; }
        public CalibrationColor Color { get; set; }

        public CalibrationParam()
        {
            Id = -1;
            Normal = new CalibrationNormal(new List<ModDetailModel>(), "");
            Color = new CalibrationColor(new List<ModDetailModel>());
            CalibrationModeList = CalibrationRsourceService.GetInstance().CalibrationModeList;
        }

        public CalibrationParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name??string.Empty ,modDetails)
        {
            Normal = new CalibrationNormal(modDetails, "");
            Color = new CalibrationColor(modDetails);
            CalibrationModeList = CalibrationRsourceService.GetInstance().CalibrationModeList;
        }
    }


}
