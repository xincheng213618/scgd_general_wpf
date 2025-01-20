using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI.Menus;
using cvColorVision;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public class ExportSMUParam : MenuItemBase
    {
        public override string OwnerGuid => "Template";

        public override string GuidId => "SMUParam";
        public override int Order => 12;
        public override string Header => Properties.Resources.MenuSUM;

        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateSMUParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }

    }


    public class TemplateSMUParam : ITemplate<SMUParam>, IITemplateLoad
    {
        public TemplateSMUParam()
        {
            Title = "SMUParam设置";
            Code = "SMU";
            TemplateParams = SMUParam.Params;
        }
    }

    public class SMUParam : ParamModBase
    {
        public static ObservableCollection<TemplateModel<SMUParam>> Params { get; set; } = new ObservableCollection<TemplateModel<SMUParam>>();

        public SMUParam() { }

        public SMUParam(ModMasterModel modMaster, List<ModDetailModel> sxDetail) : base(modMaster.Id, modMaster.Name ?? string.Empty, sxDetail) { }

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

        public bool IsNet { get => _IsNet; set { _IsNet = value; NotifyPropertyChanged(); } }
        private bool _IsNet = true;

        public double StartMeasureVal { get => _startMeasureVal; set { _startMeasureVal = value; NotifyPropertyChanged(); } }
        private double _startMeasureVal;
        public double StopMeasureVal { get => _stopMeasureVal; set { _stopMeasureVal = value; NotifyPropertyChanged(); } }
        private double _stopMeasureVal;
        public int Number { get => _number; set { _number = value; NotifyPropertyChanged(); } }
        private int _number;

        public double LimitVal { get => _limitVal; set { _limitVal = value; NotifyPropertyChanged(); } }
        private double _limitVal;

        public double MeasureVal { get => _MeasureVal; set { _MeasureVal = value; NotifyPropertyChanged(); } }
        private double _MeasureVal;

        public double LmtVal { get => _lmtVal; set { _lmtVal = value; NotifyPropertyChanged(); } }
        private double _lmtVal;

        public double? V { get => _V; set { _V = value; NotifyPropertyChanged(); } }
        private double? _V;
        public double? I { get => _I; set { _I = value; NotifyPropertyChanged(); } }
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
