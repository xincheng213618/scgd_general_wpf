#pragma warning disable CA1051,CA1805,CA1806,CA1826,CA1859,CS8625
using ColorVision.Database;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Realtime;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public partial class CameraLocalWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CameraLocalWindow));
        private readonly CameraRealtimeFramePipeline _localRealtimePipeline = new();
        private bool _disposed;

        public byte[] rawArray;
        public byte[] srcrawArray;
        public UInt32 ImgWid = 5544, ImgHei = 3684;
        public UInt32 Imgbpp = 8, Imgchannels = 1;
        public IntPtr m_hCamHandle = IntPtr.Zero;

        TakeImageMode m_etakeImageMode = TakeImageMode.Measure_Normal;
        int m_nBppIndex = 1;
        CameraModel m_eCameraMdl = CameraModel.QHY_USB;
        CameraMode m_eCameraMode = CameraMode.CV_MODE;

        public string strPathSysCfg = "cfg\\sys.cfg";

        public DeviceCamera Device { get; set; }

        public CameraLocalWindow(DeviceCamera deviceCamera)
        {
            Device = deviceCamera;
            InitializeComponent();
            DataContext = Device;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
            m_hCamHandle = cvCameraCSLib.CM_CreatCameraManagerV1(m_eCameraMdl, m_eCameraMode, strPathSysCfg);
            cvCameraCSLib.CM_InitXYZ(m_hCamHandle);

            m_eCameraMdl = Device.Config.CameraModel;
            m_eCameraMode = Device.Config.CameraMode;
            m_etakeImageMode = Device.Config.TakeImageMode;
            m_nBppIndex = Device.Config.ImageBpp == ImageBpp.bpp16 ? 1 : 0;

            cb_CM_ID.Items.Clear();
            cb_CM_TYPE.SelectedIndex = (int)m_eCameraMdl;
            cb_CM_MODE.SelectedIndex = (int)m_eCameraMode;
            cb_get_mode.SelectedIndex = (int)m_etakeImageMode;
            cb_bpp.SelectedIndex = m_nBppIndex;
            cb_Channels.SelectedIndex = Device.Config.Channel == ImageChannel.Three ? 1 : 0;

            UpdateConnectionState(false);
            RefreshCameraIdOptions(false);

            UpdateCalibrationTemplateOptions();
        }

        private bool _isInitializingCameraIdSelection;

        private void UpdateConnectionState(bool isConnected)
        {
            btn_Connect.IsEnabled = !isConnected;
            btn_close.IsEnabled = isConnected;
            btn_reset.IsEnabled = !isConnected;
            btn_RefreshCameraId.IsEnabled = !isConnected;

            cb_CM_TYPE.IsEnabled = !isConnected;
            cb_CM_MODE.IsEnabled = !isConnected;
            cb_CM_ID.IsEnabled = !isConnected;
            cb_get_mode.IsEnabled = !isConnected;
            cb_bpp.IsEnabled = !isConnected;

            bool canCapture = isConnected && m_etakeImageMode != TakeImageMode.Live;
            btn_Meas.IsEnabled = canCapture;
            button1.IsEnabled = canCapture;
            btn_CalAutoExp.IsEnabled = isConnected;
        }

        private static void SaveDisplayConfig()
        {
            ConfigHandler.GetInstance().Save<DisplayConfigManager>();
        }

        private static void SaveLocalPreferences()
        {
            ConfigHandler.GetInstance().SaveConfigs();
        }

        private void RefreshChannelsFromCamera()
        {
            if (m_hCamHandle == IntPtr.Zero)
            {
                return;
            }

            UInt32 channelCount = 0;
            if (!cvCameraCSLib.CM_GetChannels(m_hCamHandle, ref channelCount))
            {
                return;
            }

            bool isThreeChannel = channelCount == 3;
            cb_Channels.SelectedIndex = isThreeChannel ? 1 : 0;
            Device.Config.Channel = isThreeChannel ? ImageChannel.Three : ImageChannel.One;
        }

        private string ResolvePreferredCameraId(IReadOnlyList<string> cameraIds)
        {
            if (!string.IsNullOrWhiteSpace(Device.Config.CameraID)
                && cameraIds.Contains(Device.Config.CameraID, StringComparer.OrdinalIgnoreCase))
            {
                return Device.Config.CameraID;
            }

            string cameraCode = Device.Config.CameraCode ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(cameraCode))
            {
                foreach (string cameraId in cameraIds)
                {
                    string md5 = ColorVision.Common.Utilities.Tool.GetMD5(cameraId);
                    if (md5.Contains(cameraCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return cameraId;
                    }
                }
            }

            return cameraIds.FirstOrDefault() ?? string.Empty;
        }

        private void RefreshCameraIdOptions(bool showFailureMessage)
        {
            string szText = string.Empty;
            if (!cvCameraCSLib.GetAllCameraIDV1(m_eCameraMdl, ref szText))
            {
                if (showFailureMessage)
                {
                    MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.GetCameraIdFailed, "ColorVision");
                }
                return;
            }

            JObject jObject = JsonConvert.DeserializeObject<JObject>(szText);
            IReadOnlyList<string> cameraIds = jObject?["ID"]?
                .ToArray()
                .Select(token => token.ToString().Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();

            _isInitializingCameraIdSelection = true;
            cb_CM_ID.Items.Clear();
            foreach (string cameraId in cameraIds)
            {
                cb_CM_ID.Items.Add(cameraId);
            }

            string preferredCameraId = ResolvePreferredCameraId(cameraIds);
            if (!string.IsNullOrWhiteSpace(preferredCameraId))
            {
                cb_CM_ID.SelectedItem = cameraIds.FirstOrDefault(id => id.Equals(preferredCameraId, StringComparison.OrdinalIgnoreCase));
                cb_CM_ID.Text = preferredCameraId;
            }
            else if (cameraIds.Count > 0)
            {
                cb_CM_ID.SelectedIndex = 0;
                cb_CM_ID.Text = cameraIds[0];
            }

            _isInitializingCameraIdSelection = false;
        }

        private void RefreshCameraIds_Click(object sender, RoutedEventArgs e)
        {
            RefreshCameraIdOptions(true);
        }

        private void GetID_Click(object sender, RoutedEventArgs e)
        {
            RefreshCameraIdOptions(true);
        }

        private void GetAllID_Click(object sender, RoutedEventArgs e)
        {
            RefreshCameraIdOptions(true);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _localRealtimePipeline.Stop(resetRealtime: true);
            if (cvCameraCSLib.CM_IsOpen(m_hCamHandle))
            {
                if (m_etakeImageMode == TakeImageMode.Live)
                {
                    cvCameraCSLib.CM_UnregisterCallBack(m_hCamHandle);
                    cvCameraCSLib.CM_Close(m_hCamHandle);
                }
                else
                {
                    cvCameraCSLib.CM_Close(m_hCamHandle);
                }
            }

            cvCameraCSLib.ReleaseCameraManager(m_hCamHandle);
            cvCameraCSLib.ReleaseResource();
            SaveLocalPreferences();
            Dispose();
        }

        cvCameraCSLib.QHYCCDProcCallBack callback;

        private static System.Windows.Media.PixelFormat GetPixelFormat(int channels, int bpp)
        {
            if (channels == 3)
            {
                return bpp == 16
                    ? System.Windows.Media.PixelFormats.Rgb48
                    : System.Windows.Media.PixelFormats.Bgr24;
            }
            else
            {
                return bpp == 16
                    ? System.Windows.Media.PixelFormats.Gray16
                    : System.Windows.Media.PixelFormats.Gray8;
            }
        }

        ulong QHYCCDProcCallBackFunction(int enumImgType, IntPtr pData, int width, int height, int lss, int bpp, int channels, IntPtr buffer)
        {
            var pixelFormat = GetPixelFormat(channels, bpp);
            int stride = RealtimeFramePresenter.GetDefaultStride(width, pixelFormat);
            int frameBytes = stride * height;
            _localRealtimePipeline.SubmitFrame(pData, frameBytes, width, height, channels, bpp, stride);
            return 0;
        }

        private string GetSelectedCameraId()
        {
            if (cb_CM_ID.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString() ?? string.Empty;
            if (cb_CM_ID.SelectedItem is string cameraId)
                return cameraId;
            return cb_CM_ID.Text?.Trim() ?? string.Empty;
        }

        private int GetSelectedChannelCount()
        {
            if (cb_Channels.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Content?.ToString(), out int val))
                    return val;
            }
            if (cb_Channels.SelectedItem is string text && int.TryParse(text, out int parsed))
            {
                return parsed;
            }

            return Device.Config.Channel == ImageChannel.Three ? 3 : 1;
        }

        private void UpdateCalibrationTemplateOptions()
        {
            ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams.CreateEmpty();
            btn_EditCalibrationTemplate.IsEnabled = Device.PhyCamera != null;

            int itemCount = ComboxCalibrationTemplate.Items.Count;
            int selectedIndex = itemCount <= 0
                ? -1
                : Math.Max(0, Math.Min(Device.DisplayConfig.CalibrationTemplateIndex, itemCount - 1));

            ComboxCalibrationTemplate.SelectedIndex = selectedIndex;
        }

        private CalibrationParam? GetSelectedCalibrationTemplate()
        {
            return ComboxCalibrationTemplate.SelectedValue as CalibrationParam;
        }

        private static bool IsColorCalibration(CalibrationType calibrationType)
        {
            return calibrationType == CalibrationType.Luminance
                || calibrationType == CalibrationType.LumOneColor
                || calibrationType == CalibrationType.LumFourColor
                || calibrationType == CalibrationType.LumMultiColor;
        }

        private bool TryGetSelectedCalibrationFiles(bool showErrorMessage, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles)
        {
            calibrationFiles = Array.Empty<DeviceCameraCalibrationFile>();

            CalibrationParam? calibrationParam = GetSelectedCalibrationTemplate();
            if (calibrationParam == null || calibrationParam.Id == -1)
            {
                return true;
            }

            if (Device.TryGetCalibrationTemplateFiles(calibrationParam, out calibrationFiles, out string? errorMessage))
            {
                return true;
            }

            if (showErrorMessage && !string.IsNullOrWhiteSpace(errorMessage))
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), errorMessage, "ColorVision");
            }

            return false;
        }

        private static CalibrationItem CreateCalibrationItem(DeviceCameraCalibrationFile calibrationFile)
        {
            return new CalibrationItem(calibrationFile.CalibrationType, true, calibrationFile.FullPath, calibrationFile.FullPath);
        }

        private static void PopulateLegacyChannelChecks(ChannelCalibration channelCheck, IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles)
        {
            foreach (DeviceCameraCalibrationFile calibrationFile in calibrationFiles)
            {
                CalibrationItem calibrationItem = CreateCalibrationItem(calibrationFile);
                switch (calibrationFile.CalibrationType)
                {
                    case CalibrationType.DarkNoise:
                        channelCheck.DarkNoiseCheck = calibrationItem;
                        break;
                    case CalibrationType.DSNU:
                        channelCheck.dsnuCheck = calibrationItem;
                        break;
                    case CalibrationType.Uniformity:
                        channelCheck.uniformityCheck = calibrationItem;
                        break;
                    case CalibrationType.DefectPoint:
                    case CalibrationType.DefectBPoint:
                    case CalibrationType.DefectWPoint:
                        channelCheck.defectCheck = calibrationItem;
                        break;
                    case CalibrationType.Distortion:
                        channelCheck.distortionCheck = calibrationItem;
                        break;
                }
            }
        }

        private static void ApplyCalibrationTemplate(GetFrameParam param, IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles)
        {
            if (calibrationFiles.Count == 0)
            {
                return;
            }

            List<CalibrationItem> calibrationList = new();
            foreach (DeviceCameraCalibrationFile calibrationFile in calibrationFiles)
            {
                CalibrationItem calibrationItem = CreateCalibrationItem(calibrationFile);
                if (IsColorCalibration(calibrationFile.CalibrationType))
                {
                    param.lumChromaCheck = calibrationItem;
                }
                else
                {
                    calibrationList.Add(calibrationItem);
                }
            }

            if (calibrationList.Count > 0)
            {
                param.calibrationlist = calibrationList;
            }
        }

        private static readonly (ImageChannelType ChannelType, int CfwPort)[] DefaultChannelOrder =
        {
            (ImageChannelType.Gray_Y, 0),
            (ImageChannelType.Gray_X, 1),
            (ImageChannelType.Gray_Z, 2),
        };

        private IReadOnlyList<(ImageChannelType ChannelType, int CfwPort)> GetSelectedChannelConfigs(int channelCount)
        {
            List<(ImageChannelType ChannelType, int CfwPort)> channelConfigs = new(channelCount);

            AppendChannelConfigs(channelConfigs, Device.Config.CFW.ChannelCfgs, channelCount);
            if (channelConfigs.Count < channelCount)
            {
                AppendChannelConfigs(channelConfigs, Device.PhyCamera?.Config.CFW.ChannelCfgs, channelCount);
            }

            for (int i = channelConfigs.Count; i < channelCount; i++)
            {
                channelConfigs.Add(DefaultChannelOrder[Math.Min(i, DefaultChannelOrder.Length - 1)]);
            }

            return channelConfigs;
        }

        private static void AppendChannelConfigs(
            List<(ImageChannelType ChannelType, int CfwPort)> channelConfigs,
            IEnumerable<ColorVision.Engine.Services.PhyCameras.Configs.ChannelCfg>? configuredChannels,
            int maxCount)
        {
            if (configuredChannels == null)
            {
                return;
            }

            foreach (ColorVision.Engine.Services.PhyCameras.Configs.ChannelCfg configuredChannel in configuredChannels)
            {
                if (channelConfigs.Count >= maxCount)
                {
                    break;
                }

                channelConfigs.Add((configuredChannel.Chtype, configuredChannel.Cfwport));
            }
        }

        private float GetExposureForChannel(ImageChannelType channelType, int channelIndex, IReadOnlyList<float> exposureValues)
        {
            if (!Device.Config.IsExpThree)
            {
                return exposureValues[Math.Min(channelIndex, exposureValues.Count - 1)];
            }

            return channelType switch
            {
                ImageChannelType.Gray_X => (float)Device.DisplayConfig.ExpTimeR,
                ImageChannelType.Gray_Y => (float)Device.DisplayConfig.ExpTimeG,
                ImageChannelType.Gray_Z => (float)Device.DisplayConfig.ExpTimeB,
                _ => exposureValues[Math.Min(channelIndex, exposureValues.Count - 1)],
            };
        }

        private bool TryBuildParam(out string json)
        {
            json = string.Empty;
            GetFrameParam param = new GetFrameParam();

            param.channelCount = GetSelectedChannelCount();
            param.measureCount = 1;
            param.title = "";
            param.ob = 4;
            param.obR = 0;
            param.obT = 0;
            param.obB = 0;

            param.startBurst = 1;
            param.endBurst = 3;
            param.posBurst = 0;

            param.autoExpFlag = Device.Config.IsAutoExpose;

            if (!TryGetSelectedCalibrationFiles(true, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles))
            {
                return false;
            }

            ApplyCalibrationTemplate(param, calibrationFiles);

            param.channels = new List<ChannelParam>();

            IReadOnlyList<(ImageChannelType ChannelType, int CfwPort)> channelConfigs = GetSelectedChannelConfigs(param.channelCount);
            float[] exp = GetCurrentExposureValues(param.channelCount);

            for (int i = 0; i < param.channelCount; i++)
            {
                (ImageChannelType channelType, int cfwPort) = channelConfigs[i];
                ChannelParam channel = new ChannelParam();
                channel.exp = GetExposureForChannel(channelType, i, exp);
                channel.channelType = channelType;
                channel.cfwport = cfwPort;

                ChannelCalibration channelCheck = new ChannelCalibration();
                channel.check = channelCheck;

                PopulateLegacyChannelChecks(channelCheck, calibrationFiles);

                param.channels.Add(channel);
            }

            json = JsonConvert.SerializeObject(param);
            return true;
        }

        private void cb_CM_TYPE_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_hCamHandle == IntPtr.Zero) return;
            m_eCameraMdl = (CameraModel)cb_CM_TYPE.SelectedIndex;
            Device.Config.CameraModel = m_eCameraMdl;
            cvCameraCSLib.CM_SetCameraModel(m_hCamHandle, m_eCameraMdl, m_eCameraMode);
            RefreshCameraIdOptions(false);
        }

        private void cb_CM_ID_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_hCamHandle == IntPtr.Zero) return;
            string cameraId = GetSelectedCameraId();
            cvCameraCSLib.CM_SetCameraID(m_hCamHandle, cameraId);

            if (_isInitializingCameraIdSelection)
            {
                return;
            }

            Device.Config.CameraID = cameraId;
        }

        private void cb_get_mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            m_etakeImageMode = (TakeImageMode)cb_get_mode.SelectedIndex;
            Device.Config.TakeImageMode = m_etakeImageMode;
        }

        private void cb_bpp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            m_nBppIndex = cb_bpp.SelectedIndex;
            Device.Config.ImageBpp = m_nBppIndex == 0 ? ImageBpp.bpp8 : ImageBpp.bpp16;
        }

        private void ApplyCurrentCameraSettings()
        {
            if (m_hCamHandle == IntPtr.Zero || !cvCameraCSLib.CM_IsOpen(m_hCamHandle))
            {
                return;
            }

            cvCameraCSLib.CM_SetGain(m_hCamHandle, Device.DisplayConfig.Gain);

            if (!Device.Config.IsExpThree)
            {
                cvCameraCSLib.CM_SetExpTime(m_hCamHandle, (float)Device.DisplayConfig.ExpTime);
            }
        }

        private void UpdateCurrentConfigFromUi(string cameraId)
        {
            Device.Config.CameraID = cameraId;
            Device.Config.CameraModel = m_eCameraMdl;
            Device.Config.CameraMode = m_eCameraMode;
            Device.Config.TakeImageMode = m_etakeImageMode;
            Device.Config.ImageBpp = m_nBppIndex == 0 ? ImageBpp.bpp8 : ImageBpp.bpp16;
            Device.Config.Channel = GetSelectedChannelCount() == 3 ? ImageChannel.Three : ImageChannel.One;
        }

        private void btn_Connect_Click(object sender, RoutedEventArgs e)
        {
            string cameraId = GetSelectedCameraId();
            if (string.IsNullOrEmpty(cameraId))
            {
                MessageBox.Show(Properties.Resources.NoCameraId);
                return;
            }

            if (cvCameraCSLib.CM_IsOpen(m_hCamHandle))
            {
                return;
            }

            int nErr = cvErrorDefine.CV_ERR_UNKNOWN;
            cvCameraCSLib.CM_SetTakeImageMode(m_hCamHandle, m_etakeImageMode);
            cvCameraCSLib.CM_SetImageBpp(m_hCamHandle, m_nBppIndex == 0 ? 8 : 16);

            if (m_etakeImageMode != TakeImageMode.Live)
            {
                if ((nErr = cvCameraCSLib.CM_Open(m_hCamHandle)) != cvErrorDefine.CV_ERR_SUCCESS)
                {
                    string szMsg = "";
                    cvCameraCSLib.CM_GetErrorMessage(nErr, ref szMsg);
                    MessageBox.Show(szMsg);
                    btn_Connect.IsEnabled = true;
                    return;
                }

                string sn = cvCameraCSLib.CM_GetSN(m_hCamHandle);
                string mode = cvCameraCSLib.CM_GetDeviceMode(m_hCamHandle);
                this.Title = "Model: COLOR VISION " + mode;

                ApplyCurrentCameraSettings();
                UpdateCurrentConfigFromUi(cameraId);
                SaveLocalPreferences();
                UpdateConnectionState(true);
            }
            else
            {
                if ((nErr = cvCameraCSLib.CM_Open(m_hCamHandle)) != cvErrorDefine.CV_ERR_SUCCESS)
                {
                    string szMsg = "";
                    cvCameraCSLib.CM_GetErrorMessage(nErr, ref szMsg);
                    MessageBox.Show(szMsg);
                    btn_Connect.IsEnabled = true;
                    return;
                }

                ApplyCurrentCameraSettings();
                UpdateCurrentConfigFromUi(cameraId);
                SaveLocalPreferences();

                if (callback == null)
                {
                    callback = new cvCameraCSLib.QHYCCDProcCallBack(QHYCCDProcCallBackFunction);
                }

                cvCameraCSLib.CM_SetCallBack(m_hCamHandle, callback, IntPtr.Zero);
                _localRealtimePipeline.Start(ImageView);
                UpdateConnectionState(true);
            }
        }

        private void btn_close_Click(object sender, RoutedEventArgs e)
        {
            if (m_etakeImageMode == TakeImageMode.Live)
            {
                _localRealtimePipeline.Stop(resetRealtime: true);
                cvCameraCSLib.CM_UnregisterCallBack(m_hCamHandle);
                cvCameraCSLib.CM_Close(m_hCamHandle);
            }
            else
            {
                cvCameraCSLib.CM_Close(m_hCamHandle);
            }

            UpdateConnectionState(false);
            SaveLocalPreferences();
        }

        UInt32 src_w = 0, src_h = 0;
        UInt32 src_bpp = 0, src_channels = 0;

        private void btn_Meas_Click(object sender, RoutedEventArgs e)
        {
            btn_Meas.IsEnabled = false;
            button1.IsEnabled = false;
            UInt32 w = 0, h = 0;
            UInt32 channels = 0;
            uint bpp = 0;
            UInt32 dstbpp = 32;

            cvCameraCSLib.CM_GetSrcFrameInfo(m_hCamHandle, ref w, ref h, ref bpp, ref channels);
            uint nLen = (bpp / 8) * w * h * channels;

            if (srcrawArray == null || srcrawArray.Length != nLen)
            {
                srcrawArray = new byte[bpp / 8 * w * h * channels];
                rawArray = new byte[dstbpp / 8 * w * h * channels];
            }
            srcrawArray[47] = 90;
            if (!TryBuildParam(out string json1))
            {
                btn_Meas.IsEnabled = true;
                button1.IsEnabled = true;
                return;
            }

            ApplyCurrentCameraSettings();

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(json1);
            string msg = Encoding.UTF8.GetString(utf8Bytes);
            TimeSpan start = new TimeSpan(DateTime.Now.Ticks);
            int nErr = cvCameraCSLib.CM_GetFrame(m_hCamHandle, json1, ref w, ref h, ref bpp, ref dstbpp, ref channels, srcrawArray, rawArray);

            if (nErr != cvErrorDefine.CV_ERR_SUCCESS)
            {
                string szMsg = "";
                cvCameraCSLib.CM_GetErrorMessage(nErr, ref szMsg);
                MessageBox.Show(szMsg);
                btn_Connect.IsEnabled = true;
            }

            TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
            TimeSpan abs = end.Subtract(start).Duration();
            Console.WriteLine(string.Format("程序执行时间：{0}", abs.TotalMilliseconds));

            btn_Meas.IsEnabled = true;
            button1.IsEnabled = true;
            src_w = w;
            src_h = h;
            src_bpp = bpp;
            src_channels = channels;

            bool hasColorCalibration = TryGetSelectedCalibrationFiles(false, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles)
                && calibrationFiles.Any(file => IsColorCalibration(file.CalibrationType));

            if (hasColorCalibration)
            {
                cvCameraCSLib.CM_SetBufferXYZ(m_hCamHandle, w, h, dstbpp, channels, rawArray);
            }

            ShowImageInView(srcrawArray, (int)bpp, (int)channels, (int)w, (int)h);

            if (hasColorCalibration)
            {
                AttachLiveCvcieResult(w, h, dstbpp, channels);
            }

            SaveCaptureFilesIfNeeded(hasColorCalibration, w, h, bpp, dstbpp, channels, srcrawArray);
        }

        private unsafe void ShowImageInView(byte[] data, int bpp, int channels, int width, int height)
        {
            var pixelFormat = GetPixelFormat(channels, bpp);
            int stride = width * channels * (bpp / 8);

            ImageView.EditorContext.IImageOpen = null;
            ImageView.IEditorToolFactory.ApplyImageOpenTools(null);
            ImageView.SetLayerController(null);
            ImageView.Config.ClearProperties();

            WriteableBitmap writeableBitmap = new WriteableBitmap(width, height, 96, 96, pixelFormat, null);
            writeableBitmap.Lock();
            if (bpp == 16 && channels == 3 && pixelFormat == PixelFormats.Rgb48)
            {
                fixed (byte* srcByte = data)
                {
                    byte* dstByte = (byte*)writeableBitmap.BackBuffer;

                    int dstStride = writeableBitmap.BackBufferStride;

                    for (int y = 0; y < height; y++)
                    {
                        ushort* src = (ushort*)(srcByte + y * dstStride);
                        ushort* dst = (ushort*)(dstByte + y * dstStride);

                        for (int x = 0; x < width; x++)
                        {
                            // src: G R B
                            ushort b = src[x * 3 + 0];
                            ushort r = src[x * 3 + 1];
                            ushort g = src[x * 3 + 2];

                            // dst: R G B
                            dst[x * 3 + 0] =b;
                            dst[x * 3 + 1] = g;
                            dst[x * 3 + 2] = r;
                        }
                    }
                }
            }
            else
            {
                Marshal.Copy(
                    data,
                    0,
                    writeableBitmap.BackBuffer,
                    Math.Min(data.Length, writeableBitmap.BackBufferStride * height)
                );
            }

            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            writeableBitmap.Unlock();

            ImageView.OpenImage(writeableBitmap);
        }


        private float[] GetCurrentExposureValues(int channelCount)
        {
            if (!Device.Config.IsExpThree)
            {
                float exposure = (float)Device.DisplayConfig.ExpTime;
                return Enumerable.Repeat(exposure, Math.Max(channelCount, 1)).ToArray();
            }

            float exposureR = (float)Device.DisplayConfig.ExpTimeR;
            float exposureG = (float)Device.DisplayConfig.ExpTimeG;
            float exposureB = (float)Device.DisplayConfig.ExpTimeB;

            return channelCount switch
            {
                <= 1 => new[] { exposureR },
                2 => new[] { exposureR, exposureG },
                _ => new[] { exposureR, exposureG, exposureB },
            };
        }

        private void AttachLiveCvcieResult(uint width, uint height, uint bpp, uint channels)
        {
            if (rawArray == null || rawArray.Length == 0)
            {
                return;
            }

            if (!ImageView.IEditorToolFactory.IImageOpens.TryGetValue(".cvcie", out var imageOpen)
                || imageOpen is not CVRawOpen cvRawOpen)
            {
                return;
            }

            cvRawOpen.AttachLiveCvcie(ImageView, width, height, bpp, channels, rawArray, GetCurrentExposureValues(GetSelectedChannelCount()));
        }

        private string BuildLocalCaptureDirectory()
        {
            string basePath = Device.Config.FileServerCfg.DataBasePath;
            if (string.IsNullOrWhiteSpace(basePath))
            {
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision");
            }

            string deviceCode = string.IsNullOrWhiteSpace(Device.Config.Code) ? "CameraLocal" : Device.Config.Code;
            string captureDirectory = Path.Combine(basePath, deviceCode, "Data", DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(captureDirectory);
            return captureDirectory;
        }

        private static string BuildCaptureStem()
        {
            return $"Local_{DateTime.Now:yyyyMMdd_HHmmss_fff}";
        }

        private void SaveCaptureFilesIfNeeded(bool hasColorCalibration, uint width, uint height, uint srcBpp, uint dstBpp, uint channels, byte[] sourceFrameData)
        {
            if (!Device.Config.UsingFileCaching || sourceFrameData == null || sourceFrameData.Length == 0)
            {
                return;
            }

            string captureDirectory = BuildLocalCaptureDirectory();
            string stem = BuildCaptureStem();
            float[] exposureValues = GetCurrentExposureValues((int)channels);
            float gain = Device.DisplayConfig.Gain;

            string rawFilePath = Path.Combine(captureDirectory, stem + ".cvraw");
            CVCIEFile rawFile = new CVCIEFile
            {
                Version = 1,
                FileExtType = CVType.Raw,
                Rows = (int)height,
                Cols = (int)width,
                Bpp = (int)srcBpp,
                Channels = (int)channels,
                Gain = gain,
                Exp = exposureValues,
                Data = sourceFrameData
            };

            if (!CVFileUtil.WriteCVRaw(rawFilePath, rawFile))
            {
                log.Warn($"Failed to save local raw capture: {rawFilePath}");
                return;
            }

            if (!hasColorCalibration || rawArray == null || rawArray.Length == 0)
            {
                log.Info($"Saved local capture: {rawFilePath}");
                return;
            }

            string cieFilePath = Path.Combine(captureDirectory, stem + ".cvcie");
            CVCIEFile cieFile = new CVCIEFile
            {
                Version = 1,
                FileExtType = CVType.CIE,
                Rows = (int)height,
                Cols = (int)width,
                Bpp = (int)dstBpp,
                Channels = (int)channels,
                Gain = gain,
                Exp = exposureValues,
                SrcFileName = Path.GetFileName(rawFilePath),
                Data = rawArray
            };

            if (CVFileUtil.WriteCVCIE(cieFilePath, cieFile))
            {
                log.Info($"Saved local capture: {cieFilePath}");
            }
            else
            {
                log.Warn($"Failed to save local CVCIE capture: {cieFilePath}");
            }
        }

        private void btn_CalAutoExp_Click(object sender, RoutedEventArgs e)
        {
            btn_CalAutoExp.IsEnabled = false;
            float[] autoExp = new float[3];
            float[] Saturat = new float[3];

            if (btn_Connect.IsEnabled == false)
            {
                if (cvCameraCSLib.CM_GetAutoExpTime(m_hCamHandle, autoExp, Saturat) == cvErrorDefine.CV_ERR_SUCCESS)
                {
                    if (Device.Config.IsExpThree)
                    {
                        Device.DisplayConfig.ExpTimeR = autoExp[0];
                        Device.DisplayConfig.ExpTimeG = autoExp[1];
                        Device.DisplayConfig.ExpTimeB = autoExp[2];
                    }
                    else
                    {
                        Device.DisplayConfig.ExpTime = autoExp[0];
                    }

                    SaveDisplayConfig();
                }
            }

            btn_CalAutoExp.IsEnabled = true;
        }

        private void btn_SetExp_Click(object sender, RoutedEventArgs e)
        {
            if (Device.DisplayConfig.ExpTime > 0)
            {
                cvCameraCSLib.CM_SetExpTime(m_hCamHandle, (float)Device.DisplayConfig.ExpTime);
            }
            else
            {
                MessageBox.Show(Properties.Resources.SetCorrectParameters);
            }
        }

        private void btn_SetGain_Click(object sender, RoutedEventArgs e)
        {
            if (Device.DisplayConfig.Gain >= 0)
            {
                cvCameraCSLib.CM_SetGain(m_hCamHandle, Device.DisplayConfig.Gain);
            }
            else
            {
                MessageBox.Show(Properties.Resources.SetCorrectParameters);
            }
        }

        private void btn_reset_Click(object sender, RoutedEventArgs e)
        {
            int nErr = cvErrorDefine.CV_ERR_UNKNOWN;

            if ((nErr = cvCameraCSLib.CM_Reset(m_hCamHandle)) != cvErrorDefine.CV_ERR_SUCCESS)
            {
                string szMsg = "";
                cvCameraCSLib.CM_GetErrorMessage(nErr, ref szMsg);
                MessageBox.Show(szMsg);
                btn_Connect.IsEnabled = true;
                return;
            }
        }

        private void btn_Test_Click(object sender, RoutedEventArgs e)
        {
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            button1.IsEnabled = false;
            btn_Meas.IsEnabled = false;

            rawArray = null;
            if (rawArray == null)
            {
                UInt64 len = cvCameraCSLib.CM_GetFrameMaxMemLength(m_hCamHandle);
                if (len > 0)
                {
                    rawArray = new byte[len];
                    srcrawArray = new byte[len];
                }
            }

            if (!TryBuildParam(out string json1))
            {
                btn_Meas.IsEnabled = true;
                button1.IsEnabled = true;
                return;
            }

            ApplyCurrentCameraSettings();

            UInt32 w = 0, h = 0;
            UInt32 bpp = 0, channels = 0;
            uint srcbpp = 0;

            cvCameraCSLib.CM_GetFrame(m_hCamHandle, json1, ref w, ref h, ref srcbpp, ref bpp, ref channels, srcrawArray, rawArray);

            bool hasColorCalibration = TryGetSelectedCalibrationFiles(false, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles)
                && calibrationFiles.Any(file => IsColorCalibration(file.CalibrationType));

            ShowImageInView(srcrawArray, (int)srcbpp, (int)channels, (int)w, (int)h);

            if (hasColorCalibration)
            {
                AttachLiveCvcieResult(w, h, bpp, channels);
            }

            btn_Meas.IsEnabled = true;
            button1.IsEnabled = true;
        }

        private void ComboxCalibrationTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Device.DisplayConfig.CalibrationTemplateIndex = ComboxCalibrationTemplate.SelectedIndex;
            SaveDisplayConfig();
        }

        private void EditCalibrationTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (Device.PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.ConfigurePhysicalCameraBeforeCalibration, "ColorVision");
                return;
            }

            var template = new TemplateCalibrationParam(Device.PhyCamera);
            var windowTemplate = new TemplateEditorWindow(template, ComboxCalibrationTemplate.SelectedIndex - 1)
            {
                Owner = Application.Current.GetActiveWindow()
            };
            windowTemplate.ShowDialog();

            UpdateCalibrationTemplateOptions();
        }

        private void cb_CM_MODE_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_hCamHandle == IntPtr.Zero) return;
            m_eCameraMode = (CameraMode)cb_CM_MODE.SelectedIndex;
            Device.Config.CameraMode = m_eCameraMode;
            cvCameraCSLib.CM_SetCameraModel(m_hCamHandle, m_eCameraMdl, m_eCameraMode);
            RefreshChannelsFromCamera();
        }

        private void checkBox1_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (m_hCamHandle == IntPtr.Zero) return;
            cvCameraCSLib.CM_SetCameraROI(m_hCamHandle, 500, 200, 800, 700);
            UInt32 ex = 0; UInt32 ey = 0; UInt32 ew = 0; UInt32 eh = 0;
            cvCameraCSLib.CM_GetCameraROI(m_hCamHandle, ref ex, ref ey, ref ew, ref eh);
        }

        private void btn_FOV_Click(object sender, RoutedEventArgs e)
        {
        }

        private void btn_SFR_Click(object sender, RoutedEventArgs e)
        {
        }

        private void btn_Distortion_Click(object sender, RoutedEventArgs e)
        {
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Camera_MenuItem_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Channels_MenuItem_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Calibration_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            EditCalibrationTemplate_Click(sender, e);
        }

        private void ExpTime_MenuItem_Click(object sender, RoutedEventArgs e)
        {
        }

        private void cb_Channels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Device.Config.Channel = GetSelectedChannelCount() == 3 ? ImageChannel.Three : ImageChannel.One;
        }

        private void PreviewSliderLocalExp_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (m_etakeImageMode != TakeImageMode.Live || m_hCamHandle == IntPtr.Zero || !cvCameraCSLib.CM_IsOpen(m_hCamHandle))
            {
                return;
            }

            cvCameraCSLib.CM_SetExpTime(m_hCamHandle, (float)Device.DisplayConfig.ExpTime);
        }

        private void PreviewSliderLocalGain_ValueChanged1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (m_hCamHandle == IntPtr.Zero || !cvCameraCSLib.CM_IsOpen(m_hCamHandle))
            {
                return;
            }

            cvCameraCSLib.CM_SetGain(m_hCamHandle, Device.DisplayConfig.Gain);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _localRealtimePipeline.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
