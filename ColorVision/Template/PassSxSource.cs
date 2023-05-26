using ColorVision.MVVM;
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.TextFormatting;

namespace ColorVision.Template
{

    public class SxParm : ParamBase
    {
        public double StartMeasureVal { get; set; } 
        public double StopMeasureVal { get; set; } = 3;
        public int Number { get; set; } = 100;
        public double LmtVal { get; set; }
        [JsonProperty("isSourceV")]
        public bool IsSourceV { get; set; }

    }


    /// <summary>
    /// 源表
    /// </summary>
    public class PassSxSource:ViewModelBase
    {
        public bool IsOpen { get => DevID >= 0; }

        private int _DevID = -1;
        public int DevID { get => _DevID;
            private set 
            {
                _DevID = value;
                NotifyPropertyChanged(nameof(IsOpen));
            } 
        }

        private string _Version;
        public string Version
        {
            get => _Version;
            private set
            {
                _Version = value;
                NotifyPropertyChanged();
            }
        }
        public bool IsSourceV { get => _IsSourceV; set { _IsSourceV = value; SetSource(value); NotifyPropertyChanged(); } }
        private bool _IsSourceV = true;

        public string DevName { get => _DevName; set { _DevName = value; NotifyPropertyChanged(); } }
        private string _DevName;


        public bool  IsNet { get => _IsNet; set { _IsNet = value; NotifyPropertyChanged(); } }
        private bool _IsNet =true;

        public bool Open(bool isNet,string devName)
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

        public double MeasureVal { get => _MeasureVal; set { _MeasureVal = value; NotifyPropertyChanged(); } }
        private double _MeasureVal;

        public double LmtVal { get => _lmtVal; set { _lmtVal = value; NotifyPropertyChanged(); } }
        private double _lmtVal;

        public double V { get => _V;  set { _V = value; NotifyPropertyChanged(); } }
        private double _V;
        public double I { get => _I;  set { _I = value; NotifyPropertyChanged(); } }
        private double _I;
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




        public double[] VList { get => pVList; }
        public double[] IList { get => pIList; }


        private double[] pVList;
        private double[] pIList;
        public void Scan(double startVal, double endVal, double lmtVal, int points )
        {
            pVList = new double[points];
            pIList = new double[points];

            PassSx.cvSweepData(DevID, endVal, lmtVal, lmtVal, startVal, endVal, points, pVList, pIList);
        }






    }
}
