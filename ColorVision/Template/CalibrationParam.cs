using ColorVision.MySql.DAO;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ColorVision.Template
{
    public class CalibrationParam: ParamBase
    {
        [JsonProperty("FileName_DarkNoise")]
        public string FileNameDarkNoise { get => _FileNameDarkNoise; set { _FileNameDarkNoise = value; NotifyPropertyChanged(); } }
        private string _FileNameDarkNoise;

        [JsonProperty("Selected_DarkNoise")]
        public bool SelectedDarkNoise { get => _SelectedDarkNoise; set { _SelectedDarkNoise = value; NotifyPropertyChanged(); } }
        private bool _SelectedDarkNoise;

        [JsonProperty("FileName_DSNU")]
        public string FileNameDSNU { get => _FileNameDSNU; set { _FileNameDSNU = value; NotifyPropertyChanged(); } }
        private string _FileNameDSNU;

        [JsonProperty("Selected_DSNU")]
        public bool SelectedDSNU { get => _SelectedDSNU; set { _SelectedDSNU = value; NotifyPropertyChanged(); } }
        private bool _SelectedDSNU;

        [JsonProperty("FileName_Distortion")]
        public string FileNameDistortion { get => _FileNameDistortion; set { _FileNameDistortion = value; NotifyPropertyChanged(); } }
        private string _FileNameDistortion;

        [JsonProperty("Selected_Distortion")]
        public bool SelectedDistortion { get => _SelectedDistortion; set { _SelectedDistortion = value; NotifyPropertyChanged(); } }
        private bool _SelectedDistortion;

        [JsonProperty("FileName_DefectPoint")]
        public string FileNameDefectPoint { get => _FileNameDefectPoint; set { _FileNameDefectPoint = value; NotifyPropertyChanged(); } }
        private string _FileNameDefectPoint;

        [JsonProperty("Selected_DefectPoint")]
        public bool SelectedDefectPoint { get => _SelectedDefectPoint; set { _SelectedDefectPoint = value; NotifyPropertyChanged(); } }
        private bool _SelectedDefectPoint;


        [JsonProperty("FileName_Luminance")]
        public string FileNameLuminance { get => _FileNameLuminance; set { _FileNameLuminance = value; NotifyPropertyChanged(); } }
        private string _FileNameLuminance;


        public bool SelectedColorShift { get => _SelectedColorShift; set { _SelectedColorShift = value; NotifyPropertyChanged(); } }
        private bool _SelectedColorShift;

        [JsonProperty("FileName_ColorShift")]
        public string FileNameColorShift { get => _FileNameColorShift; set { _FileNameColorShift = value; NotifyPropertyChanged(); } }
        private string _FileNameColorShift;





        private bool _SelectedLuminance;
        [JsonProperty("Selected_Luminance")]
        public bool SelectedLuminance { get => _SelectedLuminance; set {
                _SelectedLuminance = value; NotifyPropertyChanged();
                if (value)
                {
                    SelectedColorOne = false;
                    SelectedColorFour = false;
                    SelectedColorMulti = false;
                }
            } }

        [JsonProperty("FileName_ColorOne")]
        public string FileNameColorOne { get => _FileNameColorOne; set { _FileNameColorOne = value; NotifyPropertyChanged(); } }
        private string _FileNameColorOne;

        [JsonProperty("Selected_ColorOne")]
        public bool SelectedColorOne { get => _SelectedColorOne;
            set { 
                _SelectedColorOne = value; 
                NotifyPropertyChanged();
                if (value)
                {
                    SelectedColorFour = false;
                    SelectedColorMulti = false;
                    SelectedLuminance = false;
                }
            } }
        private bool _SelectedColorOne;

        [JsonProperty("FileName_ColorFour")]
        public string FileNameColorFour { get => _FileNameColorFour;  set { _FileNameColorFour = value; NotifyPropertyChanged();}  }
        private string _FileNameColorFour;

        private bool _SelectedColorFour;
        [JsonProperty("Selected_ColorFour")]
        public bool SelectedColorFour { get => _SelectedColorFour; 
            set {
                _SelectedColorFour = value; 
                NotifyPropertyChanged();
                if (value)
                {
                    SelectedColorOne = false;
                    SelectedColorMulti = false;
                    SelectedLuminance = false;
                }


            } }

        [JsonProperty("FileName_ColorMulti")]
        public string FileNameColorMulti { get => _FileNameColorMulti; set { _FileNameColorMulti = value; NotifyPropertyChanged(); } }
        private string _FileNameColorMulti;


        private bool _SelectedColorMulti;
        [JsonProperty("Selected_ColorMulti")]
        public bool SelectedColorMulti { get => _SelectedColorMulti; 
            set {
                _SelectedColorMulti = value; 
                NotifyPropertyChanged();
                if (value)
                {
                    SelectedColorOne = false;
                    SelectedColorFour = false;
                    SelectedLuminance = false;
                }
            }
        }

        [JsonProperty("FileName_Uniformity_Y")]
        public string FileNameUniformityY { get => _FileNameUniformityY; set { _FileNameUniformityY = value; NotifyPropertyChanged(); } }
        private string _FileNameUniformityY;

        [JsonProperty("Selected_Uniformity_Y")]
        public bool SelectedUniformityY { get => _SelectedUniformityY; set { _SelectedUniformityY = value; NotifyPropertyChanged(); } }
        private bool _SelectedUniformityY;

        [JsonProperty("FileName_Uniformity_Z")]
        public string FileNameUniformityZ { get => _FileNameUniformityZ; set { _FileNameUniformityZ = value; NotifyPropertyChanged(); } }
        private string _FileNameUniformityZ;

        [JsonProperty("Selected_Uniformity_Z")]
        public bool SelectedUniformityZ { get => _SelectedUniformityZ; set { _SelectedUniformityZ = value; NotifyPropertyChanged(); } }
        private bool _SelectedUniformityZ;

        [JsonProperty("FileName_Uniformity_X")]
        public string FileNameUniformityX { get => _FileNameUniformityX; set { _FileNameUniformityX = value; NotifyPropertyChanged(); } }
        private string _FileNameUniformityX;

        [JsonProperty("Selected_Uniformity_X")]
        public bool SelectedUniformityX { get => _SelectedUniformityX; set { _SelectedUniformityX = value; NotifyPropertyChanged(); } }
        private bool _SelectedUniformityX;

        public CalibrationParam() 
        {
        }
        public CalibrationParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modDetails)
        {
        }
    }


}
