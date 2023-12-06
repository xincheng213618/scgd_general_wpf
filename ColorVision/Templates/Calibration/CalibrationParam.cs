#pragma warning disable CS8603,CS0649
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using cvColorVision;
using System;
using System.Collections.Generic;

namespace ColorVision.Templates
{

    public class CalibrationBase : ModelBase
    {

        public RelayCommand SelectFileCommand { get; set; }
        public CalibrationBase(List<ModDetailModel> detail,string propertyName = "") :base(detail)
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

        public string FileName { get; set; } 

        public string FilePath { get {  if (string.IsNullOrWhiteSpace(propertyName)) return GetValue(_FilePath); else return GetValue(_FilePath, propertyName); } set { if (string.IsNullOrWhiteSpace(propertyName)) SetProperty(value); else SetProperty(value, propertyName);} }
        private string _FilePath;

        public bool IsSelected { get => _IsSelected; set { if (value == _IsSelected) return; _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;
    }

    public class CalibrationNormal
    {
        public CalibrationNormal(List<ModDetailModel> detail,string Type)
        {
            DarkNoiseList = new List<string>() { "111", "222" };
            DefectPointList = new List<string>() { "111", "222" };
            DSNUList = new List<string>() { "111", "222" };
            UniformityList = new List<string>() { "111", "222" };
            DistortionList = new List<string>() { "111", "222" };
            ColorShiftList = new List<string>() { "111", "222" };


            DarkNoise = new CalibrationBase(detail, nameof(DarkNoise) +Type);
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

        public Dictionary<string,object> ToDictionary()
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
            Luminance = new CalibrationBase(detail,nameof(Luminance));
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

            LuminanceList = new List<string>() { "111", "222" };
            LumOneColorList = new List<string>() { "111", "222" };
            LumFourColorList = new List<string>() { "111", "222" };
            LumMultiColorList = new List<string>() { "111", "222" };


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

    public class CalibrationParam: ParamBase
    {
        public CalibrationNormal NormalR { get; set; } 

        public CalibrationNormal NormalG { get; set; }

        public CalibrationNormal NormalB { get; set; }

        public CalibrationColor Color { get; set; }
        public CalibrationParam() 
        {
            NormalR = new CalibrationNormal(new List<ModDetailModel>(),"R");
            NormalG = new CalibrationNormal(new List<ModDetailModel>(),"G");
            NormalB = new CalibrationNormal(new List<ModDetailModel>(),"B");
            Color = new CalibrationColor(new List<ModDetailModel>());

        }
        public CalibrationParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name??string.Empty, modDetails)
        {
            NormalR = new CalibrationNormal(modDetails,"R");
            NormalG = new CalibrationNormal(modDetails,"G");
            NormalB = new CalibrationNormal(modDetails,"B");
            Color = new CalibrationColor(modDetails);
        }
    }


}
