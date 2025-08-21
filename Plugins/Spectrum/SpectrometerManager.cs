﻿using ColorVision.Common.MVVM;
using ColorVision.UI;
using Spectrum.Properties;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace Spectrum
{
    [DisplayName("EmissionSP100设置")]
    public class SetEmissionSP100Config : ViewModelBase, IConfig
    {
        public static SetEmissionSP100Config Instance => ConfigService.Instance.GetRequiredService<SetEmissionSP100Config>();

        public event EventHandler EditChanged;

        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }
        public SetEmissionSP100Config()
        {
            EditCommand = new RelayCommand(a =>
            {
                new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                EditChanged?.Invoke(this, new EventArgs());
            } );
        }


        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; NotifyPropertyChanged(); } }
        private bool _IsEnabled = true;

        public int nStartPos { get => _nStartPos; set { _nStartPos = value; NotifyPropertyChanged(); } }
        private int _nStartPos = 1691;

        public int nEndPos { get => _nEndPos; set { _nEndPos = value; NotifyPropertyChanged(); } }
        private int _nEndPos = 2048;

        public double dMeanThreshold { get => _dMeanThreshold; set { _dMeanThreshold = value; NotifyPropertyChanged(); } }
        private double _dMeanThreshold = 80;
    }
    [DisplayName("自动积分时间配置")]
    public class IntTimeConfig : ViewModelBase, IConfig
    {
        [DisplayName("积分时间上限Ms")]
        public int IntLimitTime { get => _IntLimitTime; set { _IntLimitTime = value; NotifyPropertyChanged(); } }
        private int _IntLimitTime = 6000;

        [DisplayName("自动积分时间起始")]
        public float AutoIntTimeB { get => _AutoIntTimeB; set { _AutoIntTimeB = value; NotifyPropertyChanged(); } }
        private float _AutoIntTimeB = 1;
    }

    public class GetDataConfig : ViewModelBase, IConfig
    {
        [DisplayName("是否开启同步频率")]
        public bool IsSyncFrequencyEnabled { get => _IsSyncFrequencyEnabled; set { _IsSyncFrequencyEnabled = value; NotifyPropertyChanged(); } }
        private bool _IsSyncFrequencyEnabled;

        [DisplayName("同步频率")]
        public double Syncfreq { get => _Syncfreq; set { _Syncfreq = value; NotifyPropertyChanged(); } }
        private double _Syncfreq = 1000;

        [DisplayName("同步频率系数")]
        public int SyncfreqFactor { get => _SyncfreqFactor; set { _SyncfreqFactor = value; NotifyPropertyChanged(); } }
        private int _SyncfreqFactor = 10;

        [DisplayName("滤波宽度")]
        public int FilterBW { get => _FilterBW; set { _FilterBW = value; NotifyPropertyChanged(); } }
        private int _FilterBW = 5;

        public float SetWL1 { get => _SetWL1; set { _SetWL1 = value; NotifyPropertyChanged(); } }
        private float _SetWL1 = 380;
        public float SetWL2 { get => _SetWL2; set { _SetWL2 = value; NotifyPropertyChanged(); } }
        private float _SetWL2 = 780;

    }


    public class AutodarkParam : ViewModelBase
    {
        [DisplayName("起始时间(ms)")]
        public float fTimeStart { get => _fTimeStart; set { _fTimeStart = value; NotifyPropertyChanged(); } }
        private float _fTimeStart = 50f;

        [DisplayName("步进(ms)")]
        public int nStepTime { get => _nStepTime; set { _nStepTime = value; NotifyPropertyChanged(); } }
        private int _nStepTime = 100;

        [DisplayName("测量次数")]
        public int nStepCount { get => _nStepCount; set { _nStepCount = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(nEndTime)); } }
        private int _nStepCount = 1;

        [DisplayName("结束时间(ms)")]
        public int nEndTime { get => (int)Math.Round(fTimeStart + _nStepCount * nStepTime); set { RecalculateStepCount(value); ; } }

        private void RecalculateStepCount(int _nEndTime)
        {
            if (nStepTime > 0)
            {
                var count = Math.Round((_nEndTime - fTimeStart) / nStepTime);
                nStepCount = (int)count;
            }
            else
            {
                nStepCount = 0;
            }
        }
    }



    public class SpectrometerManager : ViewModelBase,IConfig
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrometerManager));

        public static SpectrometerManager Instance => ConfigService.Instance.GetRequiredService<SpectrometerManager>();

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();

        public static SetEmissionSP100Config SetEmissionSP100Config => SetEmissionSP100Config.Instance;

        [JsonIgnore]
        public IntPtr Handle { get; set; } = IntPtr.Zero;
        
        [JsonIgnore]
        public bool IsConnected { get => _IsConnected; set { _IsConnected = value; NotifyPropertyChanged(); } }
        private bool _IsConnected = false;

        [JsonIgnore]
        public RelayCommand SetWavelengthFileCommand { get; set; }
        [JsonIgnore]
        public RelayCommand LoadWavelengthFileCommand { get; set; }

        [JsonIgnore]
        public RelayCommand SetCSFileCommand { get; set; }

        [JsonIgnore]
        public RelayCommand SetMaguideFileCommand { get; set; }
        [JsonIgnore]
        public RelayCommand SetMaguideOutputFileCommand { get; set; }

        [JsonIgnore]
        public RelayCommand LoadMaguideFileCommand { get; set; }

        [JsonIgnore]
        public RelayCommand GetDarkDataCommand { get; set; }

        [JsonIgnore]
        public RelayCommand GetLightDataCommand { get; set; }

        [JsonIgnore]
        public RelayCommand GetIntTimeCommand { get; set; }

        [JsonIgnore]
        public RelayCommand GenerateAmplitudeCommand { get; set; }

        [JsonIgnore]
        public RelayCommand ConnectCommand { get; set; }

        [JsonIgnore]
        public RelayCommand DisconnectCommand { get; set; }

        [JsonIgnore]
        public RelayCommand EditIntTimeConfigCommand { get; set; }

        [JsonIgnore]
        public RelayCommand EditGetDataConfigCommand { get; set; }

        [JsonIgnore]
        public RelayCommand EditAutodarkParamCommand { get; set; }


        public SpectrometerManager()
        {
            SetCSFileCommand = new RelayCommand(a => SetFile(path => CSFile = path));
            GetIntTimeCommand = new RelayCommand(a => GetIntTime());
            GetDarkDataCommand = new RelayCommand(a => GetDarkData());
            GetLightDataCommand = new RelayCommand(a => GetLightData());
            GenerateAmplitudeCommand = new RelayCommand(a => GenerateAmplitude());

            ConnectCommand = new RelayCommand(a => Connect());
            DisconnectCommand = new RelayCommand(a => Disconnect());
            MaguideFile = "Magiude.dat";

            LoadWavelengthFileCommand = new RelayCommand(a => LoadWavelengthFile());
            SetWavelengthFileCommand = new RelayCommand(a => SetFile(path => WavelengthFile = path));

            SetMaguideFileCommand = new RelayCommand(a => SetMaguideFile());
            LoadMaguideFileCommand = new RelayCommand(a => LoadMaguideFile());
            SetMaguideOutputFileCommand = new RelayCommand(a => SetMaguideOutputFile());
            EditIntTimeConfigCommand = new RelayCommand(a => EditIntTimeConfig());

            EditGetDataConfigCommand = new RelayCommand(a => EditGetDataConfig());
            EditAutodarkParamCommand = new RelayCommand(a => EditAutodarkParam());
        }

        public void SetMaguideOutputFile()
        {
            using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
            {
                saveFileDialog.FileName = "Magiude.dat"; // 默认文件名
                saveFileDialog.Filter = "DAT files (*.dat)|*.dat|All files (*.*)|*.*";
                saveFileDialog.Title = "选择保存文件路径";

                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    MaguideFileOutput = saveFileDialog.FileName;

                }
            }
        }

        public void EditIntTimeConfig()
        {
            new PropertyEditorWindow(IntTimeConfig) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public IntTimeConfig IntTimeConfig { get => _IntTimeConfig; set { _IntTimeConfig = value; NotifyPropertyChanged(); } }
        private IntTimeConfig _IntTimeConfig = new IntTimeConfig();

        public void EditGetDataConfig()
        {
            new PropertyEditorWindow(GetDataConfig) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public GetDataConfig GetDataConfig { get => _GetDataConfig; set { _GetDataConfig = value; NotifyPropertyChanged(); } }
        private GetDataConfig _GetDataConfig = new GetDataConfig();

        /// <summary>
        /// 连续测试时间
        /// </summary>
        public int MeasurementInterval { get => _MeasurementInterval; set { if (value <= 0) return;  _MeasurementInterval = value;  NotifyPropertyChanged(); } }
        private int _MeasurementInterval = 30;
        /// <summary>
        /// 连续测试次数
        /// </summary>
        public int MeasurementNum { get => _MeasurementNum; set { if (value <= 0) return; _MeasurementNum = value; NotifyPropertyChanged(); } }
        private int _MeasurementNum = 30;
        /// <summary>
        /// 当前测试数
        /// </summary>
        public int LoopMeasureNum { get => _LoopMeasureNum; set { _LoopMeasureNum = value; NotifyPropertyChanged(); } }
        private int _LoopMeasureNum;


        public static int MyCallback(IntPtr strText, int nLen)
        {
            string text = Marshal.PtrToStringAnsi(strText, nLen);
            log.Info("Callback: " + text);
            return 0;
        }

        public void Connect()
        {
            Handle = Spectrometer.CM_CreateEmission((int)SpectrometerType, MyCallback);
            int ncom = int.Parse(SzComName.Replace("COM",""));
            int iR = Spectrometer.CM_Emission_Init(Handle, ncom, BaudRate);
            if (iR == 1)
            {
                MessageBox.Show("连接成功");
            }
            else
            {
                MessageBox.Show("连接失败");
            }
        }
        public int Disconnect()
        {
            if (Handle != IntPtr.Zero)
            {
                int ret = Spectrometer.CM_Emission_Close(Handle);
                ret = Spectrometer.CM_ReleaseEmission(Handle);
                IsConnected = false;
            }
            return -1;
        }

        public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName = "COM1";

        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 115200;

        public SpectrometerType SpectrometerType { get => _SpectrometerType; set { _SpectrometerType = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsCom)); } }
        private SpectrometerType _SpectrometerType = SpectrometerType.CMvSpectra;

        public bool IsCom { get => SpectrometerType == SpectrometerType.LightModule; }

        public void GenerateAmplitude()  
        {
            GetLightData();
            bool ret = Spectrometer.CM_Emission_CreateMagiude(IntTime, fDarkData, fLightData, CSFile, WavelengthFile, MaguideFileOutput);
            if (ret)
            {
                MessageBox.Show("生成成功");
            }
            else
            {
                MessageBox.Show("生成失败");
            }
        }

        public void GetLightData()
        {
            int ret = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fLightData);
            if (ret == 1)
            {
                MessageBox.Show("获取成功");
            }
            else
            {
                MessageBox.Show("获取失败");
            }
        }

        public void GetDarkData()
        {
            int ret = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fDarkData);
            if (ret == 1)
            {
                MessageBox.Show("校零成功");
            }
            else
            {
                MessageBox.Show("校零失败");
            }
        }

        public int MyAutoTimeCallback(int time,double spectum)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                log.Info($"当前自动积分时间: {time},spectum:{spectum}");
            });
            return 0;
        }

        public void GetIntTime()
        {
            float fIntTime = IntTime;
            int ret = Spectrometer.CM_Emission_GetAutoTimeEx(Handle, ref fIntTime, IntLimitTime, AutoIntTimeB, Max,MyAutoTimeCallback);
            if (ret == 1)
            {
                IntTime = fIntTime;
                MessageBox.Show("获取成功");
            }
            else
            {
                MessageBox.Show("自动积分获取失败");
            }
        }

        public float[] fDarkData = new float[2048];

        public float[] fLightData = new float[2048];


        public int IntLimitTime { get => _IntLimitTime; set { _IntLimitTime = value; NotifyPropertyChanged(); } }
        private int _IntLimitTime = 6000;

        public float AutoIntTimeB { get => _AutoIntTimeB; set { _AutoIntTimeB = value; NotifyPropertyChanged(); } }
        private float _AutoIntTimeB = 1;
        public float IntTime { get => _IntTime; set { _IntTime = value; NotifyPropertyChanged(); } }
        private float _IntTime = 100;

        public int Saturation { get => _Saturation; set { _Saturation = value; NotifyPropertyChanged(); } }
        private int _Saturation = 99;
        public int Max { get => _Max; set { _Max = value; NotifyPropertyChanged(); } }
        private int _Max = 50000;

        public double MaxPercent { get => _MaxPercent; set { _MaxPercent = value; NotifyPropertyChanged(); Max = (int)(_MaxPercent * 655.35); } }
        private double _MaxPercent = 76.3;

        public int Average { get => _Average; set { _Average = value; NotifyPropertyChanged(); } }
        private int _Average = 1;

        public string WavelengthFile { get => _WavelengthFile; set { _WavelengthFile = value; NotifyPropertyChanged(); } }
        private string _WavelengthFile = "WavaLength.dat";

        public string CSFile { get => _CSFile; set { _CSFile = value; NotifyPropertyChanged(); } }
        private string _CSFile;

        public string MaguideFile { get => _MaguideFile; set { _MaguideFile = value; NotifyPropertyChanged(); } }
        private string _MaguideFile;

        public string MaguideFileOutput { get => _MaguideFile; set { _MaguideFile = value; NotifyPropertyChanged(); } }
        private string _MaguideFileOutput;

        /// <summary>
        /// 自适应校零
        /// </summary>
        public bool EnableAutodark { get => _EnableAutodark; set { _EnableAutodark = value; NotifyPropertyChanged(); } }
        private bool _EnableAutodark;


        public AutodarkParam AutodarkParam { get => _AutodarkParam; set { _AutodarkParam = value; NotifyPropertyChanged(); } }
        private AutodarkParam _AutodarkParam = new AutodarkParam();
        public void EditAutodarkParam()
        {
            new PropertyEditorWindow(AutodarkParam) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


        /// <summary>
        /// 启动自动积分
        /// </summary>
        public bool EnableAutoIntegration { get => _EnableAutoIntegration; set { _EnableAutoIntegration = value; NotifyPropertyChanged(); } }
        private bool _EnableAutoIntegration;


        private void LoadMaguideFile()
        {
            int ret = Spectrometer.CM_Emission_LoadMagiudeFile(Handle, MaguideFile);
            log.Info($"CM_Emission_LoadMagiudeFile 配置幅值文{MaguideFile}:{ret}");
            if (ret == 1)
            {
                MessageBox.Show("配置幅值文件成功");
            }
            else
            {
                MessageBox.Show("配置幅值文件失败");
            }
        }
        private void LoadWavelengthFile()
        {
            int ret = Spectrometer.CM_Emission_LoadWavaLengthFile(Handle, WavelengthFile);
            log.Info($"CM_Emission_LoadWavaLengthFile 配置波长文{WavelengthFile}:{ret}");
            if (ret == 1)
            {
                MessageBox.Show("配置波长文件成功");
            }
            else
            {
                MessageBox.Show("配置波长文件失败");
            }
        }


        private void SetMaguideFile()
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Filter = "All Files|*.*"; // Optionally set a filter for file types
                dialog.Title = "Save Maguide File";
                dialog.FileName = "Magiude.dat"; // Default file name

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    MaguideFile = dialog.FileName;
                }
            }
        }



        private void SetFile(Action<string> setFilePath)
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Filter = "All Files|*.*"; // Optionally set a filter for file types
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    setFilePath(dialog.FileName);
                }
            }
        }

    }
}
