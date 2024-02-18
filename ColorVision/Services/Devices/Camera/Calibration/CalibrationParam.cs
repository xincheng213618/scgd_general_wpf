#pragma warning disable CS8603,CS0649
using ColorVision.MVVM;
using ColorVision.MySql.Service;
using ColorVision.Services.Dao;
using ColorVision.Services.Interfaces;
using ColorVision.Settings;
using ColorVision.Templates;
using cvColorVision;
using NPOI.XWPF.UserModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;


namespace ColorVision.Services.Devices.Camera.Calibrations
{
    public class CalibrationRsourceService
    {
        private static CalibrationRsourceService _instance;
        private static readonly object _locker = new();
        public static CalibrationRsourceService GetInstance() { lock (_locker) { return _instance ??= new CalibrationRsourceService(); } }

        private VSysResourceDao resourceDao = new VSysResourceDao();

        public CalibrationRsourceService()
        {
        }

        public ObservableCollection<CalibrationResource> GetAllCalibrationRsources(ResouceType resouceType, int CameraId)
        {
            ObservableCollection<CalibrationResource> ObservableCollections = new ObservableCollection<CalibrationResource>();
            var resouces = resourceDao.GetResourceItems((int)resouceType, CameraId);
            foreach (var item in resouces)
            {
                ObservableCollections.Add(new CalibrationResource(item));
            }
            return ObservableCollections;
        }

        public int Delete(int id) => resourceDao.DeleteById(id);

        public void Refresh()
        {
        }
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

        public int? Id { get { if (string.IsNullOrWhiteSpace(propertyName + "Id")) return GetValue(_Id); else return GetValue(_Id, propertyName + "Id"); } set { if (string.IsNullOrWhiteSpace(propertyName + "Id")) SetProperty(ref _Id, value); else SetProperty(ref _Id, value, propertyName + "Id"); NotifyPropertyChanged(); } }
        private int? _Id;
    }



    public class CalibrationNormal
    {

        public CalibrationNormal(List<ModDetailModel> detail, string Type)
        {

            DarkNoise = new CalibrationBase(detail, nameof(DarkNoise) + Type);
            DefectPoint = new CalibrationBase(detail, nameof(DefectPoint) + Type);
            DSNU = new CalibrationBase(detail, nameof(DSNU) + Type);
            Uniformity = new CalibrationBase(detail, nameof(Uniformity) + Type);
            Distortion = new CalibrationBase(detail, nameof(Distortion) + Type);
            ColorShift = new CalibrationBase(detail, nameof(ColorShift) + Type);
        }
        public CalibrationBase DarkNoise { get; set; }
        public CalibrationBase DefectPoint { get; set; }
        public CalibrationBase DSNU { get; set; }
        public CalibrationBase Uniformity { get; set; }
        public CalibrationBase Distortion { get; set; }
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
        public CalibrationBase Luminance { get; set; }
        public CalibrationBase LumOneColor { get; set; }

        public CalibrationBase LumFourColor { get; set; }

        public CalibrationBase LumMultiColor { get; set; }
    }



    public class CalibrationParam : ParamBase
    {
        public string CalibrationMode { get { return GetValue(_CalibrationMode); } set {  SetProperty(ref _CalibrationMode, value);  } }
        private string _CalibrationMode;

        public CalibrationNormal Normal { get; set; }
        public CalibrationColor Color { get; set; }

        public DeviceCamera DeviceCamera { get; set; }

        public CalibrationParam()
        {
            Id = -1;
            Normal = new CalibrationNormal(new List<ModDetailModel>(), "");
            Color = new CalibrationColor(new List<ModDetailModel>());
        }

        public CalibrationParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name??string.Empty ,modDetails)
        {
            Normal = new CalibrationNormal(modDetails, "");
            Color = new CalibrationColor(modDetails);
        }
    }


}
