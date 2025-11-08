using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using cvColorVision;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;

namespace ColorVision.Engine.Services.Devices.SMU
{

    public class TemplateSMUParam : ITemplate<SMUParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<SMUParam>> Params { get; set; } = new ObservableCollection<TemplateModel<SMUParam>>();

        public TemplateSMUParam()
        {
            Title = "SMUParamConfig";
            Code = "SMU";
            TemplateDicId = 13;
            TemplateParams = Params;
        }
    }


    public class SMUParam : ParamModBase
    {

        public SMUParam() { }

        public SMUParam(ModMasterModel modMaster, List<ModDetailModel> sxDetail) : base(modMaster, sxDetail) { }

        public double StartMeasureVal
        {
            set { SetProperty(ref _StartMeasureVal, value, "BeginValue"); }
            get => GetValue(_StartMeasureVal, "BeginValue");
        }
        public double StopMeasureVal
        {
            set { SetProperty(ref _StopMeasureVal, value, "EndValue"); }
            get => GetValue(_StopMeasureVal, "EndValue");
        }
        public int Number
        {
            set { SetProperty(ref _Number, value, "Points"); }
            get => GetValue(_Number, "Points");
        }
        public double LmtVal
        {
            set { SetProperty(ref _LmtVal, value, "LimitValue"); }
            get => GetValue(_LmtVal, "LimitValue");
        }
        public bool IsSourceV
        {
            set { SetProperty(ref _IsSourceV, value); }
            get => GetValue(_IsSourceV);
        }

        private double _StartMeasureVal;
        private double _StopMeasureVal;
        private double _LmtVal;
        private int _Number;
        private bool _IsSourceV;
    }


    /// <summary>
    /// 源表
    /// </summary>
    public class PassSxSource : ViewModelBase
    {
        public bool IsOpen { get => DevID >= 0; }

        private int _DevID = -1;
        public int DevID
        {
            get => _DevID;
            private set
            {
                _DevID = value;
                OnPropertyChanged(nameof(IsOpen));
            }
        }

        private string _Version;
        public string Version
        {
            get => _Version;
            private set
            {
                _Version = value;
                OnPropertyChanged();
            }
        }
        public bool IsSourceV { get => _IsSourceV; set { _IsSourceV = value; SetSource(value); OnPropertyChanged(); } }
        private bool _IsSourceV = true;

        public string DevName { get => _DevName; set { _DevName = value; OnPropertyChanged(); } }
        private string _DevName;

        public bool IsNet { get => _IsNet; set { _IsNet = value; OnPropertyChanged(); } }
        private bool _IsNet = true;

        public double StartMeasureVal { get => _startMeasureVal; set { _startMeasureVal = value; OnPropertyChanged(); } }
        private double _startMeasureVal;
        public double StopMeasureVal { get => _stopMeasureVal; set { _stopMeasureVal = value; OnPropertyChanged(); } }
        private double _stopMeasureVal;
        public int Number { get => _number; set { _number = value; OnPropertyChanged(); } }
        private int _number;

        public double LimitVal { get => _limitVal; set { _limitVal = value; OnPropertyChanged(); } }
        private double _limitVal;

        public double MeasureVal { get => _MeasureVal; set { _MeasureVal = value; OnPropertyChanged(); } }
        private double _MeasureVal;

        public double LmtVal { get => _lmtVal; set { _lmtVal = value; OnPropertyChanged(); } }
        private double _lmtVal;

        public double? V { get => _V; set { _V = value; OnPropertyChanged(); } }
        private double? _V;
        public double? I { get => _I; set { _I = value; OnPropertyChanged(); } }
        private double? _I;


        public bool Open(bool isNet, string devName)
        {
            if (IsOpen) return true;
            if (string.IsNullOrWhiteSpace(devName)) return false;

            DevID = PassSx.OpenNetDevice(isNet, devName);
            if (DevID >= 0)
            {
                int strLen = 1024;
                byte[] pszIdn = new byte[strLen];
                PassSx.cvPssSxGetIDN(DevID, pszIdn, ref strLen);
                Version = Encoding.Default.GetString(pszIdn, 0, strLen);
                SetSource(IsSourceV);
                PassSx.CvPssSxSetOutput(DevID);
                return true;
            }
            return false;
        }

        public void Close()
        {
            if (IsOpen)
            {
                PassSx.CvPssSxSetOutput(DevID);
                PassSx.CloseDevice(DevID);
                DevID = -1;
            }
        }

        public bool SetSource(bool isSourceV)
        {
            if (!IsOpen) return false;
            return PassSx.cvPssSxSetSourceV(DevID, isSourceV);
        }


        public void MeasureData(double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            PassSx.cvMeasureData(DevID, measureVal, lmtVal, ref rstV, ref rstI);

            rstI *= 1000;
            I = rstI;
            V = rstV;
        }
        public void StepMeasureData(double measureVal, double lmtVal, ref double rstV, ref double rstI)
        {
            PassSx.cvStepMeasureData(DevID, measureVal, lmtVal, ref rstV, ref rstI);

            rstI *= 1000;
            I = rstI;
            V = rstV;
        }

        public void CloseOutput() => PassSx.CvPssSxSetOutput(DevID);




        public double[] VList { get => pVList; set { pVList = value; } }
        public double[] IList { get => pIList; set { pIList = value; } }


        private double[] pVList;
        private double[] pIList;
        public void Scan(double startVal, double endVal, double lmtVal, int points)
        {
            pVList = new double[points];
            pIList = new double[points];

            PassSx.cvSweepData(DevID, endVal, lmtVal, lmtVal, startVal, endVal, points, pVList, pIList);
        }






    }
}
