#pragma warning disable CA1805,CA1822,CS8602,CS8622,CS8625
using ColorVision.ImageEditor;
using ColorVision.UI.Menus;
using log4net;
using MvCamCtrl.NET;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static MvCamCtrl.NET.MyCamera;

namespace Conoscope.MVS
{
    public class MenuMVSVideo : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override int Order => 1000;

        public override string Header => "MVSVideo";

        public override void Execute()
        {
            MVSViewWindow mVSViewWindow = new MVSViewWindow();
            mVSViewWindow.Show();
        }

    }



    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MVSViewWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MVSViewWindow));

        public MVSViewManager MVSViewManager { get; set; }
        MyCamera.MV_CC_DEVICE_INFO_LIST m_stDeviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
        private MyCamera m_MyCamera = new MyCamera();
        private readonly List<int> displayedDeviceIndices = new();
        private static readonly TimeSpan CaptureStopTimeout = TimeSpan.FromSeconds(2);
        private readonly MvsCaptureSession captureSession;
        private readonly MvsDeferredCleanup deferredCleanup;
        private readonly object nativeCleanupGate = new();
        private MvsSdkLease? sdkLease;
        private bool deviceCreated;
        private bool deviceOpen;
        private bool nativeCleanupCompleted;
        private readonly MvsFrameUiUpdateGate frameUiUpdateGate = new();
        private volatile bool windowClosing;
        IntPtr displayHandle = IntPtr.Zero;
        private readonly MVSGratingOverlayVisual gratingOverlay = new MVSGratingOverlayVisual();
        private Core.ConoscopeModelProfile? hookedObservationCameraProfile;

        public MVSViewWindow()
        {
            captureSession = new MvsCaptureSession(
                () => m_MyCamera.MV_CC_StartGrabbing_NET(),
                () => m_MyCamera.MV_CC_StopGrabbing_NET(),
                ReceiveThreadProcess,
                ex => log.Error("Observation camera capture thread failed", ex),
                CaptureStopTimeout,
                action => Dispatcher.BeginInvoke(DispatcherPriority.Send, action),
                HandleCaptureFault);
            deferredCleanup = new MvsDeferredCleanup(
                captureSession.WaitForExit,
                CloseDeviceAndFinalizeSdk,
                ex => log.Error("Observation camera deferred cleanup failed", ex));
            InitializeComponent();
            this.Closing += Window_Closing;
            this.Loaded += new RoutedEventHandler(BasicDemoWindow_Load);
        }
        ImageView imgDisplay { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MVSViewManager = MVSViewManager.GetInstance();
            this.DataContext = MVSViewManager;
            MVSViewManager.Config.EnsureSelectedGratingDiameter();
            MVSViewManager.Config.PropertyChanged += MVSConfig_PropertyChanged;
            MVSViewManager.PropertyChanged += MVSViewManager_PropertyChanged;
            HookObservationCameraProfile(MVSViewManager.CurrentModelProfile);
            imgDisplay = new ImageView();
            ImageDisplayHost.Children.Add(imgDisplay);
            imgDisplay.ImageShow.AddOverlayVisual(gratingOverlay);
            imgDisplay.Zoombox1.ContentMatrixChanged += ImageDisplay_ContentMatrixChanged;
            cbPixelType.ItemsSource = Enum.GetValues<PixelType>();
            SelectGratingDiameter(MVSViewManager.Config.SelectedGratingDiameterMillimeters);
            UpdateGratingOverlay();

        }

        private void SelectGratingDiameter(double diameterMillimeters)
        {
            foreach (object item in cbGratingDiameter.Items)
            {
                if (TryGetGratingDiameter(item, out double tagValue) && AreClose(tagValue, diameterMillimeters))
                {
                    cbGratingDiameter.SelectedItem = item;
                    return;
                }
            }

            cbGratingDiameter.SelectedIndex = cbGratingDiameter.Items.Count > 0 ? 0 : -1;
        }

        private static bool TryGetGratingDiameter(object? item, out double value)
        {
            switch (item)
            {
                case double doubleValue:
                    value = doubleValue;
                    return true;
                case float floatValue:
                    value = floatValue;
                    return true;
                case ComboBoxItem comboBoxItem:
                    return double.TryParse(comboBoxItem.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                default:
                    return double.TryParse(item?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }
        }

        private static bool AreClose(double left, double right)
        {
            return Math.Abs(left - right) < 0.0001;
        }

        private void ImageDisplay_ContentMatrixChanged(object? sender, EventArgs e)
        {
            UpdateGratingOverlay();
        }

        private void MVSViewManager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MVSViewManager.CurrentModelProfile))
            {
                HookObservationCameraProfile(MVSViewManager.CurrentModelProfile);
                UpdateGratingOverlay();
            }
        }

        private void MVSConfig_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MVSViewWindowConfig.SelectedGratingDiameterMillimeters))
            {
                UpdateGratingOverlay();
            }
            else if (e.PropertyName == nameof(MVSViewWindowConfig.OnlyShowCs200Devices))
            {
                DeviceListAcq();
            }
        }

        private void ObservationCameraProfile_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Core.ConoscopeModelProfile.ObservationCameraCenterX) ||
                e.PropertyName == nameof(Core.ConoscopeModelProfile.ObservationCameraCenterY) ||
                e.PropertyName == nameof(Core.ConoscopeModelProfile.ObservationCameraScaleCoefficient))
            {
                UpdateGratingOverlay();
            }
        }

        private void HookObservationCameraProfile(Core.ConoscopeModelProfile profile)
        {
            if (ReferenceEquals(hookedObservationCameraProfile, profile))
            {
                return;
            }

            if (hookedObservationCameraProfile != null)
            {
                hookedObservationCameraProfile.PropertyChanged -= ObservationCameraProfile_PropertyChanged;
            }

            hookedObservationCameraProfile = profile;
            hookedObservationCameraProfile.PropertyChanged += ObservationCameraProfile_PropertyChanged;
        }

        private void cbGratingDiameter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MVSViewManager?.Config == null)
            {
                return;
            }

            if (TryGetGratingDiameter(cbGratingDiameter.SelectedItem, out double diameterMillimeters))
            {
                MVSViewManager.Config.SelectedGratingDiameterMillimeters = diameterMillimeters;
                UpdateGratingOverlay();
                return;
            }

            if (cbGratingDiameter.Items.Count == 0)
            {
                MVSViewManager.Config.SelectedGratingDiameterMillimeters = 0;
                UpdateGratingOverlay();
            }
        }

        private void UpdateGratingOverlay()
        {
            if (MVSViewManager?.Config == null || imgDisplay == null)
            {
                return;
            }

            double diameterMillimeters = MVSViewManager.Config.SelectedGratingDiameterMillimeters;
            if (diameterMillimeters <= 0)
            {
                gratingOverlay.Clear();
                tbGratingOverlayStatus.Text = Properties.Resources.Conoscope_TestAreaNotDisplayed;
                return;
            }

            Core.ConoscopeModelProfile currentModelProfile = MVSViewManager.CurrentModelProfile;
            double scaleCoefficient = currentModelProfile.ObservationCameraScaleCoefficient;
            if (scaleCoefficient <= double.Epsilon)
            {
                gratingOverlay.Clear();
                tbGratingOverlayStatus.Text = Properties.Resources.Conoscope_ScaleCoefficientNotConfigured;
                return;
            }

            double imagePixelDiameter = diameterMillimeters / scaleCoefficient;
            if (imgDisplay.ImageShow.Source == null)
            {
                gratingOverlay.Clear();
                tbGratingOverlayStatus.Text = Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.TestAreaWaitingForImage, diameterMillimeters);
                return;
            }

            if (imagePixelDiameter <= 0 || double.IsNaN(imagePixelDiameter) || double.IsInfinity(imagePixelDiameter) ||
                double.IsNaN(currentModelProfile.ObservationCameraCenterX) || double.IsNaN(currentModelProfile.ObservationCameraCenterY) ||
                double.IsInfinity(currentModelProfile.ObservationCameraCenterX) || double.IsInfinity(currentModelProfile.ObservationCameraCenterY))
            {
                gratingOverlay.Clear();
                tbGratingOverlayStatus.Text = Properties.Resources.Conoscope_TestAreaSizeInvalid;
                return;
            }

            gratingOverlay.Update(
                new Point(currentModelProfile.ObservationCameraCenterX, currentModelProfile.ObservationCameraCenterY),
                imagePixelDiameter,
                imgDisplay.Zoombox1.ContentMatrix.M11);
            imgDisplay.ImageShow.TopVisual(gratingOverlay);
            tbGratingOverlayStatus.Text = Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.TestAreaWithCenter, diameterMillimeters, currentModelProfile.ObservationCameraCenterX, currentModelProfile.ObservationCameraCenterY);
        }

        private void BasicDemoWindow_Load(object sender, RoutedEventArgs e)
        {
            if (sdkLease == null)
            {
                // The SDK is process-global. Keep this lease until this window's
                // last native camera worker and device have both been released.
                MvsSdkAcquireResult acquireResult = MvsSdkLifetime.Shared.Acquire();
                if (!acquireResult.Acquired)
                {
                    ShowErrorMsg("Initialize SDK failed", acquireResult.NativeResult);
                    return;
                }

                sdkLease = acquireResult.Lease;
            }

            // ch: 枚举设备 | en: Enum Device List
            DeviceListAcq();
        }

        // ch:显示错误信息 | en:Show error message
        private void ShowErrorMsg(string csMessage, int nErrorNum)
        {
            log.Info($"ShowErrorMsg {csMessage}{nErrorNum}");

            string errorMsg;
            if (nErrorNum == 0)
            {
                errorMsg = csMessage;
            }
            else
            {
                errorMsg = csMessage + ": Error =" + String.Format("{0:X}", nErrorNum);
            }

            switch (nErrorNum)
            {
                case MyCamera.MV_E_HANDLE: errorMsg += " Error or invalid handle "; break;
                case MyCamera.MV_E_SUPPORT: errorMsg += " Not supported function "; break;
                case MyCamera.MV_E_BUFOVER: errorMsg += " Cache is full "; break;
                case MyCamera.MV_E_CALLORDER: errorMsg += " Function calling order error "; break;
                case MyCamera.MV_E_PARAMETER: errorMsg += " Incorrect parameter "; break;
                case MyCamera.MV_E_RESOURCE: errorMsg += " Applying resource failed "; break;
                case MyCamera.MV_E_NODATA: errorMsg += " No data "; break;
                case MyCamera.MV_E_PRECONDITION: errorMsg += " Precondition error, or running environment changed "; break;
                case MyCamera.MV_E_VERSION: errorMsg += " Version mismatches "; break;
                case MyCamera.MV_E_NOENOUGH_BUF: errorMsg += " Insufficient memory "; break;
                case MyCamera.MV_E_UNKNOW: errorMsg += " Unknown error "; break;
                case MyCamera.MV_E_GC_GENERIC: errorMsg += " General error "; break;
                case MyCamera.MV_E_GC_ACCESS: errorMsg += " Node accessing condition error "; break;
                case MyCamera.MV_E_ACCESS_DENIED: errorMsg += " No permission "; break;
                case MyCamera.MV_E_BUSY: errorMsg += " Device is busy, or network disconnected "; break;
                case MyCamera.MV_E_NETER: errorMsg += " Network error "; break;
            }

            MessageBox.Show(errorMsg, Properties.Resources.Prompt);
        }

        private int ResolveSelectedDeviceIndex()
        {
            int selectedIndex = cbDeviceList.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < displayedDeviceIndices.Count)
            {
                return displayedDeviceIndices[selectedIndex];
            }

            return selectedIndex;
        }

        private static bool TryBuildDeviceListItem(MyCamera.MV_CC_DEVICE_INFO device, out string displayName, out string modelName)
        {
            displayName = string.Empty;
            modelName = string.Empty;

            if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
            {
                MyCamera.MV_GIGE_DEVICE_INFO_EX gigeInfo = (MyCamera.MV_GIGE_DEVICE_INFO_EX)MyCamera.ByteToStruct(device.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO_EX));
                modelName = DecodeDeviceText(gigeInfo.chModelName);
                displayName = BuildDeviceDisplayName("GEV", DecodeDeviceText(gigeInfo.chUserDefinedName), DecodeDeviceText(gigeInfo.chManufacturerName), modelName, DecodeDeviceText(gigeInfo.chSerialNumber));
                return true;
            }

            if (device.nTLayerType == MyCamera.MV_USB_DEVICE)
            {
                MyCamera.MV_USB3_DEVICE_INFO_EX usbInfo = (MyCamera.MV_USB3_DEVICE_INFO_EX)MyCamera.ByteToStruct(device.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO_EX));
                modelName = DecodeDeviceText(usbInfo.chModelName);
                displayName = BuildDeviceDisplayName("U3V", DecodeDeviceText(usbInfo.chUserDefinedName), DecodeDeviceText(usbInfo.chManufacturerName), modelName, DecodeDeviceText(usbInfo.chSerialNumber));
                return true;
            }

            return false;
        }

        private static string BuildDeviceDisplayName(string prefix, string userDefinedName, string manufacturerName, string modelName, string serialNumber)
        {
            string modelDisplayName = FormatObservationCameraModelName(modelName);
            string name = !string.IsNullOrWhiteSpace(modelDisplayName)
                ? modelDisplayName
                : (!string.IsNullOrWhiteSpace(userDefinedName) ? userDefinedName : manufacturerName);

            return string.IsNullOrWhiteSpace(serialNumber)
                ? $"{prefix}: {name}"
                : $"{prefix}: {name} ({serialNumber})";
        }

        private static string FormatObservationCameraModelName(string modelName)
        {
            return modelName.Contains("CS200", StringComparison.OrdinalIgnoreCase) ? "MV-CS200" : modelName.Trim();
        }

        private static string DecodeDeviceText(byte[] value)
        {
            if (value.Length == 0 || value[0] == '\0')
            {
                return string.Empty;
            }

            Encoding encoding = MyCamera.IsTextUTF8(value) ? Encoding.UTF8 : Encoding.Default;
            return encoding.GetString(value).TrimEnd('\0').Trim();
        }

        private static string DecodeDeviceText(string value)
        {
            return value?.TrimEnd('\0').Trim() ?? string.Empty;
        }

        private void DeviceListAcq()
        {
            // ch:创建设备列表 | en:Create Device List
            System.GC.Collect();
            cbDeviceList.Items.Clear();
            displayedDeviceIndices.Clear();
            m_stDeviceList.nDeviceNum = 0;
            int nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref m_stDeviceList);
            if (0 != nRet)
            {
                ShowErrorMsg("Enumerate devices fail!", 0);
                return;
            }

            // ch:在窗体列表中显示设备名 | en:Display device name in the form list
            for (int i = 0; i < m_stDeviceList.nDeviceNum; i++)
            {
                MyCamera.MV_CC_DEVICE_INFO device = Marshal.PtrToStructure<MV_CC_DEVICE_INFO>(m_stDeviceList.pDeviceInfo[i]);

                if (!TryBuildDeviceListItem(device, out string displayName, out string modelName))
                {
                    continue;
                }

                if (MVSViewManager?.Config.OnlyShowCs200Devices == true
                    && !modelName.Contains("CS200", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                displayedDeviceIndices.Add(i);
                cbDeviceList.Items.Add(displayName);
            }

            // ch:选择第一项 | en:Select the first item
            if (cbDeviceList.Items.Count != 0)
            {
                cbDeviceList.SelectedIndex = 0;
            }
        }

        private void bnEnum_Click(object sender, RoutedEventArgs e)
        {
            DeviceListAcq();
        }
        private void bnOpen_Click(object sender, RoutedEventArgs e)
        {
            imgDisplay.Realtime.Reset();
            if (m_stDeviceList.nDeviceNum == 0 || cbDeviceList.SelectedIndex == -1)
            {
                ShowErrorMsg("No device, please select", 0);
                return;
            }

            // ch:获取选择的设备信息 | en:Get selected device information
            int deviceIndex = ResolveSelectedDeviceIndex();
            MyCamera.MV_CC_DEVICE_INFO device =
                Marshal.PtrToStructure<MV_CC_DEVICE_INFO>(m_stDeviceList.pDeviceInfo[deviceIndex]);

            // ch:打开设备 | en:Open device
            if (null == m_MyCamera)
            {
                m_MyCamera = new MyCamera();
                if (null == m_MyCamera)
                {
                    return;
                }
            }

            int nRet = m_MyCamera.MV_CC_CreateDevice_NET(ref device);
            if (MyCamera.MV_OK != nRet)
            {
                return;
            }
            deviceCreated = true;

            nRet = m_MyCamera.MV_CC_OpenDevice_NET();
            if (MyCamera.MV_OK != nRet)
            {
                m_MyCamera.MV_CC_DestroyDevice_NET();
                deviceCreated = false;
                ShowErrorMsg("Device open fail!", nRet);
                return;
            }
            deviceOpen = true;

            // ch:探测网络最佳包大小(只对GigE相机有效) | en:Detection network optimal package size(It only works for the GigE camera)
            if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
            {
                int nPacketSize = m_MyCamera.MV_CC_GetOptimalPacketSize_NET();
                if (nPacketSize > 0)
                {
                    nRet = m_MyCamera.MV_CC_SetIntValueEx_NET("GevSCPSPacketSize", nPacketSize);
                    if (nRet != MyCamera.MV_OK)
                    {
                        ShowErrorMsg("Set Packet Size failed!", nRet);
                    }
                }
                else
                {
                    ShowErrorMsg("Get Packet Size failed!", nPacketSize);
                }
            }
            int i = m_MyCamera.MV_CC_SetEnumValue_NET("PixelFormat", (uint)MVSViewWindowConfig.Instance.PixelType);

            // ch:设置采集连续模式 | en:Set Continues Aquisition Mode
            m_MyCamera.MV_CC_SetEnumValue_NET("AcquisitionMode", (uint)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
            m_MyCamera.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);

            bnGetParam_Click(null, null);

            bnOpen.IsEnabled = false;
            bnClose.IsEnabled = true;
            bnStartGrab.IsEnabled = true;
            bnStopGrab.IsEnabled = false;
            bnContinuesMode.IsEnabled = true;
            bnContinuesMode.IsChecked = true;
            bnTriggerMode.IsEnabled = true;
            cbSoftTrigger.IsEnabled = false;
            bnTriggerExec.IsEnabled = false;
            tbGain.IsEnabled = true;
            tbFrameRate.IsEnabled = true;
            bnGetParam.IsEnabled = true;
            bnSetParam.IsEnabled = true;

            MVSViewManager.IsOpen = true;
        }

        private void bnClose_Click(object sender, RoutedEventArgs e)
        {
            MvsCaptureStopResult stopResult = StopCapture(showNativeError: true);
            if (!stopResult.WorkerExited)
            {
                ShowErrorMsg("Capture thread did not stop; device remains open", 0);
                return;
            }

            // ch:关闭设备 | en:Close Device
            CloseDevice();

            // ch:控件操作 | en:Control Operation
            bnOpen.IsEnabled = true;

            bnClose.IsEnabled = false;
            bnStartGrab.IsEnabled = false;
            bnStopGrab.IsEnabled = false;
            bnContinuesMode.IsEnabled = false;
            bnTriggerMode.IsEnabled = false;
            cbSoftTrigger.IsEnabled = false;
            bnTriggerExec.IsEnabled = false;
            tbGain.IsEnabled = false;
            tbFrameRate.IsEnabled = false;
            bnGetParam.IsEnabled = false;
            bnSetParam.IsEnabled = false;

            MVSViewManager.IsOpen = false;
        }

        private void bnContinuesMode_Checked(object sender, RoutedEventArgs e)
        {
            if (true == bnContinuesMode.IsChecked)
            {
                m_MyCamera.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
                cbSoftTrigger.IsEnabled = false;
                bnTriggerExec.IsEnabled = false;
            }
        }

        private void bnTriggerMode_Checked(object sender, RoutedEventArgs e)
        {
            // ch:打开触发模式 | en:Open Trigger Mode
            if (true == bnTriggerMode.IsChecked)
            {
                m_MyCamera.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);

                // ch:触发源选择 | en:Trigger source select;
                if (true == cbSoftTrigger.IsChecked)
                {
                    m_MyCamera.MV_CC_SetEnumValue_NET("TriggerSource", (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);
                    if (captureSession.IsWorkerAlive)
                    {
                        bnTriggerExec.IsEnabled = true;
                    }
                }
                else
                {
                    m_MyCamera.MV_CC_SetEnumValue_NET("TriggerSource", (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE0);
                }
                cbSoftTrigger.IsEnabled = true;
            }
        }
        private void ReceiveThreadProcess(CancellationToken cancellationToken)
        {
            long generation = frameUiUpdateGate.BeginGeneration();
            MyCamera.MV_FRAME_OUT stFrameInfo = new MyCamera.MV_FRAME_OUT();
            MV_PIXEL_CONVERT_PARAM stConvertParam = new MV_PIXEL_CONVERT_PARAM();
            IntPtr pImageBuffer = IntPtr.Zero;
            uint currentImageBufferSize = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    int nRet = m_MyCamera.MV_CC_GetImageBuffer_NET(ref stFrameInfo, 1000);
                    if (nRet == MyCamera.MV_OK)
                    {
                        try
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                continue;
                            }

                            IntPtr pRenderData = IntPtr.Zero;
                            uint renderDataSize = 0;
                            PixelFormat pixelFormat = PixelFormats.Gray8;
                            bool shouldRender = false;

                            // 1. 根据不同像素格式准备数据指针和参数
                            if (stFrameInfo.stFrameInfo.enPixelType == MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB8)
                            {
                                if (MVSViewManager.Config.IsCoverBayer)
                                {
                                    uint nConvertDataSize = (uint)(stFrameInfo.stFrameInfo.nWidth * stFrameInfo.stFrameInfo.nHeight * 3);

                                    // 动态分配或调整非托管内存大小
                                    if (pImageBuffer == IntPtr.Zero || currentImageBufferSize != nConvertDataSize)
                                    {
                                        if (pImageBuffer != IntPtr.Zero) Marshal.FreeHGlobal(pImageBuffer);
                                        pImageBuffer = Marshal.AllocHGlobal((int)nConvertDataSize);
                                        currentImageBufferSize = nConvertDataSize;
                                    }

                                    stConvertParam.nWidth = stFrameInfo.stFrameInfo.nWidth;
                                    stConvertParam.nHeight = stFrameInfo.stFrameInfo.nHeight;
                                    stConvertParam.pSrcData = stFrameInfo.pBufAddr;
                                    stConvertParam.nSrcDataLen = stFrameInfo.stFrameInfo.nFrameLen;
                                    stConvertParam.enSrcPixelType = stFrameInfo.stFrameInfo.enPixelType;
                                    stConvertParam.enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed;
                                    stConvertParam.nDstBufferSize = nConvertDataSize;
                                    stConvertParam.pDstBuffer = pImageBuffer;

                                    if (m_MyCamera.MV_CC_ConvertPixelType_NET(ref stConvertParam) == MyCamera.MV_OK)
                                    {
                                        pRenderData = pImageBuffer;
                                        renderDataSize = nConvertDataSize;
                                        pixelFormat = PixelFormats.Rgb24;
                                        shouldRender = true;
                                    }
                                }
                                else
                                {
                                    pRenderData = stFrameInfo.pBufAddr;
                                    renderDataSize = stFrameInfo.stFrameInfo.nFrameLen;
                                    pixelFormat = PixelFormats.Gray8;
                                    shouldRender = true;
                                }
                            }
                            else if (stFrameInfo.stFrameInfo.enPixelType == MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed)
                            {
                                pRenderData = stFrameInfo.pBufAddr;
                                renderDataSize = stFrameInfo.stFrameInfo.nFrameLen;
                                pixelFormat = PixelFormats.Rgb24;
                                shouldRender = true;
                            }

                            // Realtime copies the native buffer before returning, then posts a
                            // coalesced render without making this worker wait for the UI thread.
                            if (shouldRender && pRenderData != IntPtr.Zero && !cancellationToken.IsCancellationRequested)
                            {
                                int width = stFrameInfo.stFrameInfo.nWidth;
                                int height = stFrameInfo.stFrameInfo.nHeight;
                                int stride = (width * pixelFormat.BitsPerPixel + 7) / 8;
                                if (imgDisplay.Realtime.SubmitFrame(pRenderData, width, height, pixelFormat, stride, checked((int)renderDataSize)))
                                {
                                    QueueFrameUiUpdate(generation, cancellationToken);
                                }
                            }
                        }
                        finally
                        {
                            // ch:释放相机内部缓存 | en:Release camera-owned frame buffer
                            m_MyCamera.MV_CC_FreeImageBuffer_NET(ref stFrameInfo);
                        }
                    }
                }
            }
            finally
            {
                // 确保线程退出时释放非托管内存，防止内存泄漏
                if (pImageBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pImageBuffer);
                    pImageBuffer = IntPtr.Zero;
                }
            }
        }

        private void QueueFrameUiUpdate(long generation, CancellationToken cancellationToken)
        {
            if (!frameUiUpdateGate.TryQueue(generation))
            {
                return;
            }

            imgDisplay.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!frameUiUpdateGate.TryComplete(generation))
                {
                    return;
                }

                if (windowClosing || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                MVSViewManager.Count++;
                UpdateGratingOverlay();
            }));
        }

        public Int32 ConvertToRGB(object obj, IntPtr pSrc, ushort nHeight, ushort nWidth, MyCamera.MvGvspPixelType nPixelType, IntPtr pDst)
        {
            if (IntPtr.Zero == pSrc || IntPtr.Zero == pDst)
            {
                return MyCamera.MV_E_PARAMETER;
            }

            int nRet = MyCamera.MV_OK;
            MyCamera device = obj as MyCamera;
            MyCamera.MV_PIXEL_CONVERT_PARAM stPixelConvertParam = new MyCamera.MV_PIXEL_CONVERT_PARAM();

            stPixelConvertParam.pSrcData = pSrc;//源数据
            if (IntPtr.Zero == stPixelConvertParam.pSrcData)
            {
                return -1;
            }

            stPixelConvertParam.nWidth = nWidth;//图像宽度
            stPixelConvertParam.nHeight = nHeight;//图像高度
            stPixelConvertParam.enSrcPixelType = nPixelType;//源数据的格式
            stPixelConvertParam.nSrcDataLen = (uint)(nWidth * nHeight * ((((uint)nPixelType) >> 16) & 0x00ff) >> 3);

            stPixelConvertParam.nDstBufferSize = (uint)(nWidth * nHeight * ((((uint)MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed) >> 16) & 0x00ff) >> 3);
            stPixelConvertParam.pDstBuffer = pDst;//转换后的数据
            stPixelConvertParam.enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed;
            stPixelConvertParam.nDstBufferSize = (uint)nWidth * nHeight * 3;

            nRet = device.MV_CC_ConvertPixelType_NET(ref stPixelConvertParam);//格式转换
            if (MyCamera.MV_OK != nRet)
            {
                return -1;
            }

            return MyCamera.MV_OK;
        }

        private void bnStartGrab_Click(object sender, RoutedEventArgs e)
        {
            MvsCaptureStartResult startResult = captureSession.Start();
            if (!startResult.Started)
            {
                if (startResult.AlreadyRunning)
                {
                    log.Warn("Observation camera capture is already running or still stopping");
                }
                else
                {
                    ShowErrorMsg("Start Grabbing Fail!", startResult.NativeResult);
                }
                return;
            }

            // ch:控件操作 | en:Control Operation
            bnStartGrab.IsEnabled = false;
            bnStopGrab.IsEnabled = true;

            if (true == bnTriggerMode.IsChecked && true == cbSoftTrigger.IsChecked)
            {
                bnTriggerExec.IsEnabled = true;
            }
        }

        private void bnStopGrab_Click(object sender, RoutedEventArgs e)
        {
            MvsCaptureStopResult stopResult = StopCapture(showNativeError: true);
            if (!stopResult.WorkerExited)
            {
                ShowErrorMsg("Capture thread did not stop within the shutdown timeout", 0);
                bnStartGrab.IsEnabled = false;
                bnStopGrab.IsEnabled = false;
                bnTriggerExec.IsEnabled = false;
                return;
            }

            // ch:控件操作 | en:Control Operation
            bnStartGrab.IsEnabled = true;
            bnStopGrab.IsEnabled = false;

            bnTriggerExec.IsEnabled = false;
        }

        private void bnTriggerExec_Click(object sender, RoutedEventArgs e)
        {
            // ch:触发命令 | en:Trigger command
            int nRet = m_MyCamera.MV_CC_SetCommandValue_NET("TriggerSoftware");
            if (MyCamera.MV_OK != nRet)
            {
                ShowErrorMsg("Trigger Software Fail!", nRet);
            }
        }

        private void cbSoftTrigger_Checked(object sender, RoutedEventArgs e)
        {
            if (true == cbSoftTrigger.IsChecked)
            {
                // ch:触发源设为软触发 | en:Set trigger source as Software
                m_MyCamera.MV_CC_SetEnumValue_NET("TriggerSource", (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);
                if (captureSession.IsWorkerAlive)
                {
                    bnTriggerExec.IsEnabled = true;
                }
            }
            else
            {
                m_MyCamera.MV_CC_SetEnumValue_NET("TriggerSource", (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE0);
                bnTriggerExec.IsEnabled = false;
            }
        }

        private void bnGetParam_Click(object sender, RoutedEventArgs e)
        {
            MyCamera.MVCC_FLOATVALUE stParam = new MyCamera.MVCC_FLOATVALUE();
            int nRet = m_MyCamera.MV_CC_GetFloatValue_NET("ExposureTime", ref stParam);
            if (MyCamera.MV_OK == nRet)
            {
                MVSViewManager.Config.Exposure = stParam.fCurValue / 1000;
            }

            nRet = m_MyCamera.MV_CC_GetFloatValue_NET("Gain", ref stParam);
            if (MyCamera.MV_OK == nRet)
            {
                tbGain.Text = stParam.fCurValue.ToString("F1");
            }

            nRet = m_MyCamera.MV_CC_GetFloatValue_NET("ResultingFrameRate", ref stParam);
            if (MyCamera.MV_OK == nRet)
            {
                tbFrameRate.Text = stParam.fCurValue.ToString("F1");
            }

            UpdateStatusBar();
        }

        private void bnSetParam_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                float.Parse(tbGain.Text);
                float.Parse(tbFrameRate.Text);
            }
            catch
            {
                ShowErrorMsg("Please enter correct type!", 0);
                return;
            }

            m_MyCamera.MV_CC_SetEnumValue_NET("ExposureAuto", 0);
            int nRet = m_MyCamera.MV_CC_SetFloatValue_NET("ExposureTime", (float)MVSViewManager.Config.Exposure*1000);
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Set Exposure Time Fail!", nRet);
            }

            m_MyCamera.MV_CC_SetEnumValue_NET("GainAuto", 0);
            nRet = m_MyCamera.MV_CC_SetFloatValue_NET("Gain", float.Parse(tbGain.Text));
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Set Gain Fail!", nRet);
            }

            nRet = m_MyCamera.MV_CC_SetFloatValue_NET("AcquisitionFrameRate", float.Parse(tbFrameRate.Text));
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Set Frame Rate Fail!", nRet);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            windowClosing = true;
            MvsCaptureStopResult stopResult = StopCapture(showNativeError: false);
            MVSViewManager.Config.PropertyChanged -= MVSConfig_PropertyChanged;
            MVSViewManager.PropertyChanged -= MVSViewManager_PropertyChanged;
            if (hookedObservationCameraProfile != null)
            {
                hookedObservationCameraProfile.PropertyChanged -= ObservationCameraProfile_PropertyChanged;
                hookedObservationCameraProfile = null;
            }
            imgDisplay.Zoombox1.ContentMatrixChanged -= ImageDisplay_ContentMatrixChanged;
            imgDisplay.ImageShow.RemoveOverlayVisual(gratingOverlay);
            imgDisplay.Dispose();
            MVSViewManager.IsOpen = false;

            if (stopResult.WorkerExited)
            {
                CloseDeviceAndFinalizeSdk();
            }
            else
            {
                log.Warn("Observation camera worker did not stop in time; native cleanup is deferred until it exits");
                deferredCleanup.Schedule();
            }
        }

        private MvsCaptureStopResult StopCapture(bool showNativeError)
        {
            MvsCaptureStopResult result = captureSession.Stop();
            if (showNativeError && result.StopRequested && result.NativeResult != MyCamera.MV_OK)
            {
                ShowErrorMsg("Stop Grabbing Fail!", result.NativeResult);
            }

            if (result.WorkerExited)
            {
                imgDisplay.Realtime.Reset();
            }

            return result;
        }

        private void HandleCaptureFault(Exception exception, MvsCaptureStopResult stopResult)
        {
            if (windowClosing)
            {
                return;
            }

            imgDisplay.Realtime.Reset();
            bnStartGrab.IsEnabled = stopResult.WorkerExited && deviceOpen;
            bnStopGrab.IsEnabled = false;
            bnTriggerExec.IsEnabled = false;

            if (stopResult.NativeResult != MyCamera.MV_OK)
            {
                ShowErrorMsg("Stop Grabbing Fail!", stopResult.NativeResult);
            }

            ShowErrorMsg($"Capture stopped because the receive thread failed: {exception.Message}", 0);
        }

        private void CloseDevice()
        {
            lock (nativeCleanupGate)
            {
                if (deviceOpen)
                {
                    m_MyCamera.MV_CC_CloseDevice_NET();
                    deviceOpen = false;
                }

                if (deviceCreated)
                {
                    m_MyCamera.MV_CC_DestroyDevice_NET();
                    deviceCreated = false;
                }
            }
        }

        private void CloseDeviceAndFinalizeSdk()
        {
            lock (nativeCleanupGate)
            {
                if (nativeCleanupCompleted)
                {
                    return;
                }

                if (deviceOpen)
                {
                    m_MyCamera.MV_CC_CloseDevice_NET();
                    deviceOpen = false;
                }

                if (deviceCreated)
                {
                    m_MyCamera.MV_CC_DestroyDevice_NET();
                    deviceCreated = false;
                }

                if (sdkLease != null)
                {
                    int releaseResult = sdkLease.Release();
                    sdkLease = null;
                    if (releaseResult != MyCamera.MV_OK)
                    {
                        log.Error($"Finalize MVS SDK failed: 0x{releaseResult:X8}");
                    }
                }

                nativeCleanupCompleted = true;
            }
        }

        private void tbGain_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                float.Parse(tbGain.Text);
            }
            catch
            {
                ShowErrorMsg("Please enter correct type!", 0);
                return;
            }

            m_MyCamera.MV_CC_SetEnumValue_NET("GainAuto", 0);
            int nRet = m_MyCamera.MV_CC_SetFloatValue_NET("Gain", float.Parse(tbGain.Text));
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Set Gain Fail!", nRet);
            }

            UpdateStatusBar();
        }

        private void tbExposure_TextChanged(object sender, TextChangedEventArgs e)
        {


        }

        private void tbFrameRate_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                float.Parse(tbFrameRate.Text);
            }
            catch
            {
                ShowErrorMsg("Please enter correct type!", 0);
                return;
            }

            int nRet = m_MyCamera.MV_CC_SetFloatValue_NET("AcquisitionFrameRate", float.Parse(tbFrameRate.Text));
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Set Frame Rate Fail!", nRet);
            }

            UpdateStatusBar();
        }

        private void ParameterTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            if (sender == tbExposure)
            {
                CommitExposure();
            }
            else if (sender == tbGain)
            {
                CommitGain();
            }

            e.Handled = true;
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        private void cbModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //int nRet = m_MyCamera.MV_CC_SetEnumValue_NET("PixelFormat", (uint)MVSViewWindowConfig.Instance.PixelType);
            //if (nRet != MyCamera.MV_OK)
            //{
            //    ShowErrorMsg("Set Frame Rate Fail!", nRet);
            //}
        }

        // Menu event handlers
        private void MenuShowSidePanel_Checked(object sender, RoutedEventArgs e)
        {
            if (SidePanelColumn != null)
            {
                SidePanelColumn.Width = new GridLength(200);
            }
        }

        private void MenuShowSidePanel_Unchecked(object sender, RoutedEventArgs e)
        {
            if (SidePanelColumn != null)
            {
                SidePanelColumn.Width = new GridLength(0);
            }
        }

        private void MenuShowStatusBar_Checked(object sender, RoutedEventArgs e)
        {
            if (StatusBarBottom != null)
            {
                StatusBarBottom.Visibility = Visibility.Visible;
            }
        }

        private void MenuShowStatusBar_Unchecked(object sender, RoutedEventArgs e)
        {
            if (StatusBarBottom != null)
            {
                StatusBarBottom.Visibility = Visibility.Collapsed;
            }
        }

        // Update status bar with current camera parameters
        private void UpdateStatusBar()
        {
            if (StatusGainText != null && !string.IsNullOrEmpty(tbGain.Text))
            {
                StatusGainText.Text = tbGain.Text;
            }
            if (StatusFrameRateText != null && !string.IsNullOrEmpty(tbFrameRate.Text))
            {
                StatusFrameRateText.Text = tbFrameRate.Text;
            }
        }

        private void ExposureTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitExposure();
        }

        private void GainTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitGain();
        }

        private void CommitExposure()
        {
            BindingOperations.GetBindingExpression(tbExposure, TextBox.TextProperty)?.UpdateSource();
            if (!MVSViewManager.IsOpen) return;
            if (MVSViewManager.Config.Exposure <= 0)
            {
                log.Info($"ExposureTime {MVSViewManager.Config.Exposure}");
                return;
            }

            if (MVSViewManager.Config.Exposure > MVSViewManager.Config.MaxExposure)
            {
                log.Info($"Exposure time cannot be greater than {MVSViewManager.Config.MaxExposure} ms!");
                return;
            }
            m_MyCamera.MV_CC_SetEnumValue_NET("ExposureAuto", 0);
            int nRet = m_MyCamera.MV_CC_SetFloatValue_NET("ExposureTime", (float)MVSViewManager.Config.Exposure * 1000);
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Set Exposure Time Fail!", nRet);
            }
            log.Info($"ExposureTime ret{nRet}");
            UpdateStatusBar();
        }

        private void CommitGain()
        {
            if (!MVSViewManager.IsOpen || string.IsNullOrWhiteSpace(tbGain.Text))
            {
                return;
            }

            if (!float.TryParse(tbGain.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out float gain) &&
                !float.TryParse(tbGain.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out gain))
            {
                ShowErrorMsg("Please enter correct type!", 0);
                return;
            }

            m_MyCamera.MV_CC_SetEnumValue_NET("GainAuto", 0);
            int nRet = m_MyCamera.MV_CC_SetFloatValue_NET("Gain", gain);
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Set Gain Fail!", nRet);
            }

            UpdateStatusBar();
        }
    }
}
