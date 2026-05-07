using ColorVision.Database;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WindowsFormsTest;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public struct HImageLocal
    {
        public uint nWidth;
        public uint nHeight;
        public uint nChannels;
        public uint nBpp;
        public IntPtr pData;
    };

    public partial class CameraLocalWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CameraLocalWindow));

        public byte[] rawArray;
        public byte[] srcrawArray;
        public UInt32 ImgWid = 5544, ImgHei = 3684;
        public UInt32 Imgbpp = 8, Imgchannels = 1;
        public IntPtr m_hCamHandle = IntPtr.Zero;
        public FormCfg formcfg = null;

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
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
            m_hCamHandle = cvCameraCSLib.CM_CreatCameraManagerV1(m_eCameraMdl, m_eCameraMode, strPathSysCfg);
            cvCameraCSLib.CM_InitXYZ(m_hCamHandle);

            formcfg = new FormCfg(m_hCamHandle, strPathSysCfg);

            cb_CM_ID.Items.Clear();
            cb_CM_TYPE.SelectedIndex = (int)Device.Config.CameraModel;
            cb_CM_MODE.SelectedIndex = (int)Device.Config.CameraMode;
            cb_get_mode.SelectedIndex = (int)TakeImageMode.Live;
            cb_bpp.SelectedIndex = 0; // "8"

            btn_close.IsEnabled = false;
            btn_Meas.IsEnabled = false;
            btn_MeasTif.IsEnabled = false;
            button1.IsEnabled = false;
            btn_CalAutoExp.IsEnabled = true;

            UpdateCalibrationTemplateOptions();
        }

        private void GetID_Click(object sender, RoutedEventArgs e)
        {
            string szText = "";

            if (cvCameraCSLib.GetAllCameraIDV1(m_eCameraMdl, ref szText))
            {
                log.Info(szText);
                JObject jObject = (JObject)JsonConvert.DeserializeObject(szText);

                if (jObject["ID"] != null)
                {
                    JToken[] data = jObject["ID"].ToArray();

                    for (int i = 0; i < data.Length; i++)
                    {
                        string camerid = data[i].ToString().Trim();
                        string MD5 = ColorVision.Common.Utilities.Tool.GetMD5(camerid);
                        cb_CM_ID.Items.Add(new ComboBoxItem { Content = camerid });

                        if (MD5.ToUpper().Contains(Device.Config.CameraCode))
                        {
                            cb_CM_ID.SelectedIndex = cb_CM_ID.Items.Count - 1;
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Fail");
            }
        }
        private bool _isRefreshing;

        private void GetAllID_Click(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing)
            {
                MessageBox.Show("正在遍历支持的相机模式", "ColorVision");
                return; // 防止重复点击
            }
            _isRefreshing = true;

            // 异步执行，避免阻塞UI线程
            Task.Run((Action)(() =>
            {
                int bufferSize = 102400; // 10KB 缓冲区，视你相机的数量而定
                StringBuilder sbJson = new StringBuilder(bufferSize);

                int ret = cvCameraCSLib.CM_GetAllCameraIDMD5(sbJson, bufferSize);
                _isRefreshing = false;
                // 回到UI线程
                Application.Current.Dispatcher.Invoke(() =>
                {
                    log.Info($"GetAllCameraIDMD5 返回值: {ret}");
                    if (ret == 1)
                    {
                        string cameraIdsMd5 = sbJson.ToString();
                        MessageBox1.Show(cameraIdsMd5, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox1.Show("获取相机ID MD5失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
            }));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (cvCameraCSLib.CM_IsOpen(m_hCamHandle))
            {
                if (m_etakeImageMode == TakeImageMode.Live)
                {
                    cvCameraCSLib.CM_UnregisterCallBack(m_hCamHandle);
                    cvCameraCSLib.CM_Close(m_hCamHandle);
                    m_hStopEvent.Set();
                }
                else
                {
                    cvCameraCSLib.CM_Close(m_hCamHandle);
                }
            }

            cvCameraCSLib.ReleaseCameraManager(m_hCamHandle);
            cvCameraCSLib.ReleaseResource();
        }

        cvCameraCSLib.QHYCCDProcCallBack callback;
        AutoResetEvent m_hShowPictureEvent = new AutoResetEvent(false);
        EventWaitHandle m_hStopEvent = new EventWaitHandle(false, EventResetMode.ManualReset);

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
            Application.Current?.Dispatcher.Invoke(new Action(() =>
            {
                WriteableBitmap writeableBitmap = ImageView.ImageShow.Source as WriteableBitmap;
                bool needNewBitmap = writeableBitmap == null
                    || writeableBitmap.PixelWidth != width
                    || writeableBitmap.PixelHeight != height
                    || GetPixelFormat(channels, bpp) != writeableBitmap.Format;

                if (needNewBitmap)
                {
                    writeableBitmap = new WriteableBitmap(
                        width,
                        height,
                        96, 96,
                        GetPixelFormat(channels, bpp),
                        null);
                    ImageView.ImageShow.Source = writeableBitmap;
                }
                writeableBitmap!.Lock();
                writeableBitmap.WritePixels(
                    new Int32Rect(0, 0, width, height),
                    pData,
                    height * width * channels * (bpp / 8),
                    width * channels * (bpp / 8));
                writeableBitmap.Unlock();
            }));
            return 0;
        }

        private string GetSelectedCameraId()
        {
            if (cb_CM_ID.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString() ?? "";
            return "";
        }

        private int GetSelectedChannelCount()
        {
            if (cb_Channels.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Content?.ToString(), out int val))
                    return val;
            }
            return 1;
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
            UpdateCalibrationFileSummary();
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

        private void ApplyCalibrationTemplate(GetFrameParam param, IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles)
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

        private void UpdateCalibrationFileSummary()
        {
            CalibrationParam? calibrationParam = GetSelectedCalibrationTemplate();
            if (calibrationParam == null || calibrationParam.Id == -1)
            {
                tb_CalibrationFiles.Text = "未选择校正模板";
                return;
            }

            if (Device.TryGetCalibrationTemplateFiles(calibrationParam, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles, out string? errorMessage))
            {
                tb_CalibrationFiles.Text = calibrationFiles.Count == 0
                    ? "模板未启用校正文件"
                    : string.Join(Environment.NewLine, calibrationFiles.Select(file => $"{file.CalibrationType}: {file.FullPath}"));
                return;
            }

            tb_CalibrationFiles.Text = string.IsNullOrWhiteSpace(errorMessage) ? "模板解析失败" : errorMessage;
        }

        private bool TryBuildParam(string? savePath, string? exts, out string json)
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

            param.autoExpFlag = cb_AutoExp.IsChecked == true;

            if (!TryGetSelectedCalibrationFiles(true, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles))
            {
                return false;
            }

            ApplyCalibrationTemplate(param, calibrationFiles);

            param.channels = new List<ChannelParam>();

            ImageChannelType[] types = new ImageChannelType[3];
            int[] cfwport = new int[3];

            for (int i = 0; i < formcfg.projectSysCfg.channelCfg.Count; i++)
            {
                if (i == 3)
                {
                    break;
                }

                types[i] = formcfg.projectSysCfg.channelCfg[i].chtype;
                cfwport[i] = formcfg.projectSysCfg.channelCfg[i].cfwport;
            }

            float[] exp = new float[3];
            exp[0] = 100;
            exp[1] = 100;
            exp[2] = 100;

            for (int i = 0; i < param.channelCount; i++)
            {
                ChannelParam channel = new ChannelParam();
                channel.exp = exp[i];
                channel.channelType = (ImageChannelType)types[i];
                channel.cfwport = cfwport[i];

                ChannelCalibration channelCheck = new ChannelCalibration();
                channel.check = channelCheck;

                PopulateLegacyChannelChecks(channelCheck, calibrationFiles);

                param.channels.Add(channel);
            }

            if (savePath != null && exts != null)
                param.BuildChannelsFileName(savePath, exts);

            json = JsonConvert.SerializeObject(param);
            return true;
        }

        private void cb_CM_TYPE_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_hCamHandle == IntPtr.Zero) return;
            m_eCameraMdl = (CameraModel)cb_CM_TYPE.SelectedIndex;
            cvCameraCSLib.CM_SetCameraModel(m_hCamHandle, m_eCameraMdl, m_eCameraMode);
        }

        private void cb_CM_ID_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_hCamHandle == IntPtr.Zero) return;
            cvCameraCSLib.CM_SetCameraID(m_hCamHandle, GetSelectedCameraId());
        }

        private void cb_get_mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            m_etakeImageMode = (TakeImageMode)cb_get_mode.SelectedIndex;
        }

        private void cb_bpp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            m_nBppIndex = cb_bpp.SelectedIndex;
        }

        private void btn_Connect_Click(object sender, RoutedEventArgs e)
        {
            string cameraId = GetSelectedCameraId();
            if (string.IsNullOrEmpty(cameraId))
            {
                MessageBox.Show("没有相机ID!");
                return;
            }

            if (cvCameraCSLib.CM_IsOpen(m_hCamHandle))
            {
                return;
            }

            int nErr = cvErrorDefine.CV_ERR_UNKNOWN;
            cvCameraCSLib.CM_SetTakeImageMode(m_hCamHandle, m_etakeImageMode);
            if (m_nBppIndex == 0)
            {
                cvCameraCSLib.CM_SetImageBpp(m_hCamHandle, 8);
            }
            else
            {
                cvCameraCSLib.CM_SetImageBpp(m_hCamHandle, 16);
            }

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

                cb_CM_TYPE.IsEnabled = false;
                cb_CM_ID.IsEnabled = false;
                cb_get_mode.IsEnabled = false;
                cb_bpp.IsEnabled = false;
                btn_Connect.IsEnabled = false;
                btn_close.IsEnabled = true;
                btn_Meas.IsEnabled = true;
                button1.IsEnabled = true;
                btn_MeasTif.IsEnabled = true;
                btn_CalAutoExp.IsEnabled = true;
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

                cvCameraCSLib.CM_SetExpTime(m_hCamHandle, float.Parse(tb_Exp.Text));
                cvCameraCSLib.CM_SetGain(m_hCamHandle, float.Parse(tb_Gain.Text));

                if (callback == null)
                {
                    callback = new cvCameraCSLib.QHYCCDProcCallBack(QHYCCDProcCallBackFunction);
                }

                cvCameraCSLib.CM_SetCallBack(m_hCamHandle, callback, IntPtr.Zero);
            }
        }

        private void btn_close_Click(object sender, RoutedEventArgs e)
        {
            btn_close.IsEnabled = false;

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

        UInt32 src_w = 0, src_h = 0;
        UInt32 src_bpp = 0, src_channels = 0;

        private void btn_Meas_Click(object sender, RoutedEventArgs e)
        {
            btn_Meas.IsEnabled = false;
            btn_MeasTif.IsEnabled = false;
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
            if (!TryBuildParam(null, null, out string json1))
            {
                btn_Meas.IsEnabled = true;
                btn_MeasTif.IsEnabled = true;
                return;
            }

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
            btn_MeasTif.IsEnabled = true;
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
        }

        private void ShowImageInView(byte[] data, int bpp, int channels, int width, int height)
        {
            var pixelFormat = GetPixelFormat(channels, bpp);
            int stride = width * channels * (bpp / 8);

            ImageView.EditorContext.IImageOpen = null;
            ImageView.IEditorToolFactory.ApplyImageOpenTools(null);
            ImageView.SetLayerController(null);
            ImageView.Config.ClearProperties();

            WriteableBitmap writeableBitmap = new WriteableBitmap(width, height, 96, 96, pixelFormat, null);
            writeableBitmap.Lock();
            Marshal.Copy(data, 0, writeableBitmap.BackBuffer, Math.Min(data.Length, stride * height));
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            writeableBitmap.Unlock();

            ImageView.OpenImage(writeableBitmap);
        }

        private static float ParseExposureText(string? text, float defaultValue)
        {
            return float.TryParse(text, out float value) ? value : defaultValue;
        }

        private float[] GetCurrentExposureValues(int channelCount)
        {
            float exposure1 = ParseExposureText(tb_Exp.Text, 100f);
            float exposure2 = ParseExposureText(tb_Exp2.Text, exposure1);
            float exposure3 = ParseExposureText(tb_Exp3.Text, exposure2);

            return channelCount switch
            {
                <= 1 => new[] { exposure1 },
                2 => new[] { exposure1, exposure2 },
                _ => new[] { exposure1, exposure2, exposure3 },
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

        private void btn_MeasTif_Click(object sender, RoutedEventArgs e)
        {
            btn_MeasTif.IsEnabled = false;
            btn_Meas.IsEnabled = false;

            if (!TryBuildParam(tb_TiffPath.Text, ".tif", out string json1))
            {
                btn_MeasTif.IsEnabled = true;
                btn_Meas.IsEnabled = true;
                return;
            }
            cvCameraCSLib.CM_GetFrame_TIFF(m_hCamHandle, json1);

            btn_MeasTif.IsEnabled = true;
            btn_Meas.IsEnabled = true;
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
                    if ((CameraType)cb_CM_TYPE.SelectedIndex == CameraType.CV_Q)
                    {
                        tb_Exp.Text = autoExp[0].ToString();
                        tb_Exp2.Text = autoExp[1].ToString();
                        tb_Exp3.Text = autoExp[2].ToString();
                    }
                    else
                    {
                        tb_Exp.Text = autoExp[0].ToString();
                    }
                }
            }

            btn_CalAutoExp.IsEnabled = true;
        }

        private void btn_SetExp_Click(object sender, RoutedEventArgs e)
        {
            if (float.Parse(tb_Exp.Text) > 0)
            {
                cvCameraCSLib.CM_SetExpTime(m_hCamHandle, float.Parse(tb_Exp.Text));
            }
            else
            {
                MessageBox.Show("请设置正确的参数！");
            }
        }

        private void btn_SetGain_Click(object sender, RoutedEventArgs e)
        {
            if (int.Parse(tb_Gain.Text) >= 0)
            {
                cvCameraCSLib.CM_SetGain(m_hCamHandle, float.Parse(tb_Gain.Text));
            }
            else
            {
                MessageBox.Show("请设置正确的参数！");
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

        private void btn_ConfigFile_Click(object sender, RoutedEventArgs e)
        {
            formcfg.m_nChannelCount = GetSelectedChannelCount();
            formcfg.m_hHandle = m_hCamHandle;
            formcfg.ShowDialog();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            button1.IsEnabled = false;
            btn_Meas.IsEnabled = false;
            btn_MeasTif.IsEnabled = false;

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

            if (!TryBuildParam(null, null, out string json1))
            {
                btn_Meas.IsEnabled = true;
                btn_MeasTif.IsEnabled = true;
                button1.IsEnabled = true;
                return;
            }

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
            btn_MeasTif.IsEnabled = true;
            button1.IsEnabled = true;
        }

        private void ComboxCalibrationTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Device.DisplayConfig.CalibrationTemplateIndex = ComboxCalibrationTemplate.SelectedIndex;
            UpdateCalibrationFileSummary();
        }

        private void EditCalibrationTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (Device.PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "在使用校正前，请先配置对映的物理相机", "ColorVision");
                return;
            }

            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.MainWindow, Properties.Resources.DatabaseConnectionFailed, "ColorVision");
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
            cvCameraCSLib.CM_SetCameraModel(m_hCamHandle, m_eCameraMdl, m_eCameraMode);

            UInt32 nChls = 0;
            if (cvCameraCSLib.CM_GetChannels(m_hCamHandle, ref nChls))
            {
                if (nChls == 1)
                    cb_Channels.SelectedIndex = 0;
                else
                    cb_Channels.SelectedIndex = 1;
            }
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
        }
    }
}
