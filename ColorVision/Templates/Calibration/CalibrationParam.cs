using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using cvColorVision;
using System;
using System.Collections.Generic;

namespace ColorVision.Templates
{

    public class CalibrationBase : ViewModelBase
    {

        public RelayCommand SelectFileCommand { get; set; }
        public CalibrationBase()
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
        }

        public string FileName { get; set; } 

        public string FilePath { get => _FilePath; set { _FilePath = value; NotifyPropertyChanged(); } }
        private string _FilePath;

        public bool IsSelected { get => _IsSelected; set { if (value == _IsSelected) return; _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;
    }

    public class CalibrationNormal
    {
        public CalibrationNormal()
        {
            DarkNoiseList = new List<string>() { "111", "222" };
            DefectPointList = new List<string>() { "111", "222" };
            DSNUList = new List<string>() { "111", "222" };
            UniformityList = new List<string>() { "111", "222" };
            DistortionList = new List<string>() { "111", "222" };
            ColorShiftList = new List<string>() { "111", "222" };
        }



        public List<string> DarkNoiseList { get; set; }
        public CalibrationBase DarkNoise { get; set; } = new CalibrationBase();
        public List<string> DefectPointList { get; set; }
        public CalibrationBase DefectPoint { get; set; } = new CalibrationBase();
        public List<string> DSNUList { get; set; }
        public CalibrationBase DSNU { get; set; } = new CalibrationBase();
        public List<string> UniformityList { get; set; }
        public CalibrationBase Uniformity { get; set; } = new CalibrationBase();
        public List<string> DistortionList { get; set; }
        public CalibrationBase Distortion { get; set; } = new CalibrationBase();
        public List<string> ColorShiftList { get; set; }
        public CalibrationBase ColorShift { get; set; } = new CalibrationBase();

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

        public CalibrationColor()
        {
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
        public CalibrationBase Luminance { get; set; } = new CalibrationBase();
        public List<string> LumOneColorList { get; set; }
        public CalibrationBase LumOneColor { get; set; } = new CalibrationBase();

        public List<string> LumFourColorList { get; set; }
        public CalibrationBase LumFourColor { get; set; } = new CalibrationBase();

        public List<string> LumMultiColorList { get; set; }
        public CalibrationBase LumMultiColor { get; set; } = new CalibrationBase();
    }

    public class CalibrationParam: ParamBase
    {
        public CalibrationNormal NormalR { get; set; } = new CalibrationNormal();

        public CalibrationNormal NormalG { get; set; } = new CalibrationNormal();

        public CalibrationNormal NormalB { get; set; } = new CalibrationNormal();


        public CalibrationColor Color { get; set; } = new CalibrationColor();

        public CalibrationParam() 
        {

        }
        public CalibrationParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name??string.Empty, modDetails)
        {

        }
    }


}
