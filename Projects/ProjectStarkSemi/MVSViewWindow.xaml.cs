using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using MvCamCtrl.NET;
using ProjectStarkSemi.Conoscope;
using ScottPlot.AxisLimitManagers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static MvCamCtrl.NET.MyCamera;

namespace ProjectStarkSemi
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


    public enum PixelType
    {
        PixelType_Gvsp_BayerGB8 = 0x0108000a,
        PixelType_Gvsp_RGB8_Packed = 0x02180014
    }

    public class MVSViewWindowConfig : ViewModelBase,IConfig
    {
        public static MVSViewWindowConfig Instance => ConfigService.Instance.GetRequiredService<MVSViewWindowConfig>();

        public PixelType PixelType { get => _PixelType; set { _PixelType = value; OnPropertyChanged(); } }
        private PixelType _PixelType = PixelType.PixelType_Gvsp_BayerGB8;

        public bool IsCoverBayer { get => _IsCoverBayer; set { _IsCoverBayer = value; OnPropertyChanged(); } }
        private bool _IsCoverBayer = true;
    }

    public class MVSViewManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MVSViewManager));
        private static MVSViewManager _instance;
        private static readonly object _locker = new();
        public static MVSViewManager GetInstance() { lock (_locker) { return _instance ??= new MVSViewManager(); } }

        public MVSViewWindowConfig Config { get; set; }
        public RelayCommand EditMVSViewConfigCommand { get; set; }

        public MVSViewManager() 
        {
            Config = MVSViewWindowConfig.Instance;
            EditMVSViewConfigCommand = new RelayCommand(a => EditMVSViewConfig());
        }
        public void EditMVSViewConfig()
        {
            new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

    }


    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MVSViewWindow : Window
    {
        public MVSViewManager MVSViewManager { get; set; }
        MyCamera.MV_CC_DEVICE_INFO_LIST m_stDeviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
        private MyCamera m_MyCamera = new MyCamera();
        bool m_bGrabbing = false;
        Thread m_hReceiveThread = null;
        IntPtr displayHandle = IntPtr.Zero;

        public MVSViewWindow()
        {
            InitializeComponent();
            this.Closing += Window_Closing;
            this.Loaded += new RoutedEventHandler(BasicDemoWindow_Load);
        }
        ImageView imgDisplay { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MVSViewManager = MVSViewManager.GetInstance();
            this.DataContext = MVSViewManager;
            imgDisplay = new ImageView();
            DisplayGrid.Child = imgDisplay;
            cbPixelType.ItemsSource = Enum.GetValues(typeof(PixelType));

        }

        private void BasicDemoWindow_Load(object sender, RoutedEventArgs e)
        {
            // ch: 初始化 SDK | en: Initialize SDK
            MyCamera.MV_CC_Initialize_NET();

            // ch: 枚举设备 | en: Enum Device List
            DeviceListAcq();
        }

        // ch:显示错误信息 | en:Show error message
        private void ShowErrorMsg(string csMessage, int nErrorNum)
        {
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

            MessageBox.Show(errorMsg, "PROMPT");
        }

        private void DeviceListAcq()
        {
            // ch:创建设备列表 | en:Create Device List
            System.GC.Collect();
            cbDeviceList.Items.Clear();
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
                MyCamera.MV_CC_DEVICE_INFO device = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(m_stDeviceList.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO));
                string strUserDefinedName = "";

                if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                {
                    MyCamera.MV_GIGE_DEVICE_INFO_EX gigeInfo = (MyCamera.MV_GIGE_DEVICE_INFO_EX)MyCamera.ByteToStruct(device.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO_EX));

                    if ((gigeInfo.chUserDefinedName.Length > 0) && (gigeInfo.chUserDefinedName[0] != '\0'))
                    {
                        if (MyCamera.IsTextUTF8(gigeInfo.chUserDefinedName))
                        {
                            strUserDefinedName = Encoding.UTF8.GetString(gigeInfo.chUserDefinedName).TrimEnd('\0');
                        }
                        else
                        {
                            strUserDefinedName = Encoding.Default.GetString(gigeInfo.chUserDefinedName).TrimEnd('\0');
                        }

                        cbDeviceList.Items.Add("GEV: " + strUserDefinedName + " (" + gigeInfo.chSerialNumber + ")");
                    }
                    else
                    {
                        cbDeviceList.Items.Add("GEV: " + gigeInfo.chManufacturerName + " " + gigeInfo.chModelName + " (" + gigeInfo.chSerialNumber + ")");
                    }
                }
                else if (device.nTLayerType == MyCamera.MV_USB_DEVICE)
                {
                    MyCamera.MV_USB3_DEVICE_INFO_EX usbInfo = (MyCamera.MV_USB3_DEVICE_INFO_EX)MyCamera.ByteToStruct(device.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO_EX));
                    
                    if ((usbInfo.chUserDefinedName.Length > 0) && (usbInfo.chUserDefinedName[0] != '\0'))
                    {
                        if (MyCamera.IsTextUTF8(usbInfo.chUserDefinedName))
                        {
                            strUserDefinedName = Encoding.UTF8.GetString(usbInfo.chUserDefinedName).TrimEnd('\0');
                        }
                        else
                        {
                            strUserDefinedName = Encoding.Default.GetString(usbInfo.chUserDefinedName).TrimEnd('\0');
                        }
                        cbDeviceList.Items.Add("U3V: " + strUserDefinedName + " (" + usbInfo.chSerialNumber + ")");
                    }
                    else
                    {
                        cbDeviceList.Items.Add("U3V: " + usbInfo.chManufacturerName + " " + usbInfo.chModelName + " (" + usbInfo.chSerialNumber + ")");
                    }
                }
            }

            // ch:选择第一项 | en:Select the first item
            if (m_stDeviceList.nDeviceNum != 0)
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
            writeableBitmap = null;
            if (m_stDeviceList.nDeviceNum == 0 || cbDeviceList.SelectedIndex == -1)
            {
                ShowErrorMsg("No device, please select", 0);
                return;
            }

            // ch:获取选择的设备信息 | en:Get selected device information
            MyCamera.MV_CC_DEVICE_INFO device =
                (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(m_stDeviceList.pDeviceInfo[cbDeviceList.SelectedIndex],
                                                              typeof(MyCamera.MV_CC_DEVICE_INFO));

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

            nRet = m_MyCamera.MV_CC_OpenDevice_NET();
            if (MyCamera.MV_OK != nRet)
            {
                m_MyCamera.MV_CC_DestroyDevice_NET();
                ShowErrorMsg("Device open fail!", nRet);
                return;
            }

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

            // ch:控件操作 | en:Control operation
            bnOpen.IsEnabled = false;

            bnClose.IsEnabled = true;
            bnStartGrab.IsEnabled = true;
            bnStopGrab.IsEnabled = false;
            bnContinuesMode.IsEnabled = true;
            bnContinuesMode.IsChecked = true;
            bnTriggerMode.IsEnabled = true;
            cbSoftTrigger.IsEnabled = false;
            bnTriggerExec.IsEnabled = false;

            tbExposure.IsEnabled = true;
            tbGain.IsEnabled = true;
            tbFrameRate.IsEnabled = true;
            bnGetParam.IsEnabled = true;
            bnSetParam.IsEnabled = true;
        }

        private void bnClose_Click(object sender, RoutedEventArgs e)
        {
            // ch:取流标志位清零 | en:Reset flow flag bit
            if (m_bGrabbing == true)
            {
                m_bGrabbing = false;
            }

            // ch:关闭设备 | en:Close Device
            m_MyCamera.MV_CC_CloseDevice_NET();
            m_MyCamera.MV_CC_DestroyDevice_NET();

            // ch:控件操作 | en:Control Operation
            bnOpen.IsEnabled = true;

            bnClose.IsEnabled = false;
            bnStartGrab.IsEnabled = false;
            bnStopGrab.IsEnabled = false;
            bnContinuesMode.IsEnabled = false;
            bnTriggerMode.IsEnabled = false;
            cbSoftTrigger.IsEnabled = false;
            bnTriggerExec.IsEnabled = false;

            tbExposure.IsEnabled = false;
            tbGain.IsEnabled = false;
            tbFrameRate.IsEnabled = false;
            bnGetParam.IsEnabled = false;
            bnSetParam.IsEnabled = false;
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
                    if (m_bGrabbing)
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
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void RtlMoveMemory(IntPtr Destination, IntPtr Source, uint Length);

        WriteableBitmap writeableBitmap { get; set; }
        public void ReceiveThreadProcess()
        {
            MyCamera.MV_FRAME_OUT stFrameInfo = new MyCamera.MV_FRAME_OUT();
            MyCamera.MV_DISPLAY_FRAME_INFO stDisplayInfo = new MyCamera.MV_DISPLAY_FRAME_INFO();
            MV_PIXEL_CONVERT_PARAM stConvertParam = new MV_PIXEL_CONVERT_PARAM();
            IntPtr pImageBuffer = IntPtr.Zero;
            int nRet = MyCamera.MV_OK;

            while (m_bGrabbing)
            {
                nRet = m_MyCamera.MV_CC_GetImageBuffer_NET(ref stFrameInfo, 1000);
                if (nRet == MyCamera.MV_OK)
                {
                    if (stFrameInfo.stFrameInfo.enPixelType == MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB8)
                    {
                        if (MVSViewManager.Config.IsCoverBayer)
                        {
                            MvGvspPixelType enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed;
                            uint nConvertDataSize = (uint)stFrameInfo.stFrameInfo.nWidth * stFrameInfo.stFrameInfo.nHeight * 3;
                            if (pImageBuffer == IntPtr.Zero)
                            {
                                pImageBuffer = Marshal.AllocHGlobal((int)nConvertDataSize);
                            }

                            // ch:像素格式转换 | en:Convert pixel format 

                            stConvertParam.nWidth = stFrameInfo.stFrameInfo.nWidth;                 //ch:图像宽 | en:image width
                            stConvertParam.nHeight = stFrameInfo.stFrameInfo.nHeight;               //ch:图像高 | en:image height
                            stConvertParam.pSrcData = stFrameInfo.pBufAddr;                         //ch:输入数据缓存 | en:input data buffer
                            stConvertParam.nSrcDataLen = stFrameInfo.stFrameInfo.nFrameLen;         //ch:输入数据大小 | en:input data size
                            stConvertParam.enSrcPixelType = stFrameInfo.stFrameInfo.enPixelType;    //ch:输入像素格式 | en:input pixel format
                            stConvertParam.enDstPixelType = enDstPixelType;                         //ch:输出像素格式 | en:output pixel format
                            stConvertParam.nDstBufferSize = nConvertDataSize;                       //ch:输出缓存大小 | en:output buffer size'
                            stConvertParam.pDstBuffer = pImageBuffer; //ch:输出数据缓存 | en:output data buffer

                            nRet = m_MyCamera.MV_CC_ConvertPixelType_NET(ref stConvertParam);//图像格式转化

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // Bail out if grabbing has been stopped while waiting for the dispatcher
                                if (!m_bGrabbing) return;

                                // --- KEY CHANGE: Only create the WriteableBitmap if needed ---
                                // 1. If it hasn't been created yet (is null)
                                // 2. Or if the image resolution has changed
                                if (writeableBitmap == null ||
                                    writeableBitmap.PixelWidth != stFrameInfo.stFrameInfo.nWidth ||
                                    writeableBitmap.PixelHeight != stFrameInfo.stFrameInfo.nHeight)
                                {
                                    // Create the bitmap with the correct dimensions and format
                                    writeableBitmap = new WriteableBitmap(
                                        stFrameInfo.stFrameInfo.nWidth,
                                        stFrameInfo.stFrameInfo.nHeight,
                                        96, 96, // DPI X and Y
                                        PixelFormats.Rgb24, // Displaying Bayer as Grayscale
                                        null);

                                    // Set the Image control's source ONCE, when the bitmap is first created
                                    imgDisplay.ImageShow.Source = writeableBitmap;
                                    imgDisplay.UpdateZoomAndScale();
                                }

                                // --- Update the existing bitmap's buffer on every frame ---
                                try
                                {
                                    // Reserve the back buffer for writing
                                    writeableBitmap.Lock();

                                    // Copy the image data from the camera buffer to the bitmap's back buffer
                                    // Note: Ensure the buffer sizes match to avoid exceptions.
                                    uint bufferSize = (uint)(writeableBitmap.PixelWidth * writeableBitmap.PixelHeight * writeableBitmap.Format.BitsPerPixel / 8);
                                    uint dataSize = stConvertParam.nDstBufferSize;
                                    uint bytesToCopy = Math.Min(bufferSize, dataSize);

                                    RtlMoveMemory(writeableBitmap.BackBuffer, stConvertParam.pDstBuffer, bytesToCopy);

                                    // Specify the area of the bitmap that changed (the whole image)
                                    writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                                }
                                finally
                                {
                                    // Release the back buffer to allow the UI to render the changes
                                    writeableBitmap.Unlock();
                                }
                            });
                        }
                        else
                        {

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // Bail out if grabbing has been stopped while waiting for the dispatcher
                                if (!m_bGrabbing) return;

                                // --- KEY CHANGE: Only create the WriteableBitmap if needed ---
                                // 1. If it hasn't been created yet (is null)
                                // 2. Or if the image resolution has changed
                                if (writeableBitmap == null ||
                                    writeableBitmap.PixelWidth != stFrameInfo.stFrameInfo.nWidth ||
                                    writeableBitmap.PixelHeight != stFrameInfo.stFrameInfo.nHeight)
                                {
                                    // Create the bitmap with the correct dimensions and format
                                    writeableBitmap = new WriteableBitmap(
                                        stFrameInfo.stFrameInfo.nWidth,
                                        stFrameInfo.stFrameInfo.nHeight,
                                        96, 96, // DPI X and Y
                                        PixelFormats.Gray8, // Displaying Bayer as Grayscale
                                        null);

                                    // Set the Image control's source ONCE, when the bitmap is first created
                                    imgDisplay.ImageShow.Source = writeableBitmap;
                                    imgDisplay.UpdateZoomAndScale();
                                }

                                // --- Update the existing bitmap's buffer on every frame ---
                                try
                                {
                                    // Reserve the back buffer for writing
                                    writeableBitmap.Lock();

                                    // Copy the image data from the camera buffer to the bitmap's back buffer
                                    // Note: Ensure the buffer sizes match to avoid exceptions.
                                    uint bufferSize = (uint)(writeableBitmap.PixelWidth * writeableBitmap.PixelHeight * writeableBitmap.Format.BitsPerPixel / 8);
                                    uint dataSize = stFrameInfo.stFrameInfo.nFrameLen;
                                    uint bytesToCopy = Math.Min(bufferSize, dataSize);

                                    RtlMoveMemory(writeableBitmap.BackBuffer, stFrameInfo.pBufAddr, bytesToCopy);

                                    // Specify the area of the bitmap that changed (the whole image)
                                    writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                                }
                                finally
                                {
                                    // Release the back buffer to allow the UI to render the changes
                                    writeableBitmap.Unlock();
                                }
                            });
                        }



                    };


                    if (stFrameInfo.stFrameInfo.enPixelType == MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Bail out if grabbing has been stopped while waiting for the dispatcher
                            if (!m_bGrabbing) return;

                            // --- KEY CHANGE: Only create the WriteableBitmap if needed ---
                            // 1. If it hasn't been created yet (is null)
                            // 2. Or if the image resolution has changed
                            if (writeableBitmap == null ||
                                writeableBitmap.PixelWidth != stFrameInfo.stFrameInfo.nWidth ||
                                writeableBitmap.PixelHeight != stFrameInfo.stFrameInfo.nHeight)
                            {
                                // Create the bitmap with the correct dimensions and format
                                writeableBitmap = new WriteableBitmap(
                                    stFrameInfo.stFrameInfo.nWidth,
                                    stFrameInfo.stFrameInfo.nHeight,
                                    96, 96, // DPI X and Y
                                    PixelFormats.Rgb24, // Displaying Bayer as Grayscale
                                    null);

                                // Set the Image control's source ONCE, when the bitmap is first created
                                imgDisplay.ImageShow.Source = writeableBitmap;
                                imgDisplay.UpdateZoomAndScale();
                            }

                            // --- Update the existing bitmap's buffer on every frame ---
                            try
                            {
                                // Reserve the back buffer for writing
                                writeableBitmap.Lock();

                                // Copy the image data from the camera buffer to the bitmap's back buffer
                                // Note: Ensure the buffer sizes match to avoid exceptions.
                                uint bufferSize = (uint)(writeableBitmap.PixelWidth * writeableBitmap.PixelHeight * writeableBitmap.Format.BitsPerPixel / 8);
                                uint dataSize = stFrameInfo.stFrameInfo.nFrameLen;
                                uint bytesToCopy = Math.Min(bufferSize, dataSize);

                                RtlMoveMemory(writeableBitmap.BackBuffer, stFrameInfo.pBufAddr, bytesToCopy);

                                // Specify the area of the bitmap that changed (the whole image)
                                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                            }
                            finally
                            {
                                // Release the back buffer to allow the UI to render the changes
                                writeableBitmap.Unlock();
                            }
                        });
                    }
                    ;


                    //stDisplayInfo.hWnd = displayHandle;
                    //stDisplayInfo.pData = stFrameInfo.pBufAddr;
                    //stDisplayInfo.nDataLen = stFrameInfo.stFrameInfo.nFrameLen;
                    //stDisplayInfo.nWidth = stFrameInfo.stFrameInfo.nWidth;
                    //stDisplayInfo.nHeight = stFrameInfo.stFrameInfo.nHeight;
                    //stDisplayInfo.enPixelType = stFrameInfo.stFrameInfo.enPixelType;
                    //m_MyCamera.MV_CC_DisplayOneFrame_NET(ref stDisplayInfo);

                    m_MyCamera.MV_CC_FreeImageBuffer_NET(ref stFrameInfo);
                }
            }
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
            //displayHandle = displayArea.Handle;

            // ch:标志位置位true | en:Set position bit true
            m_bGrabbing = true;

            m_hReceiveThread = new Thread(ReceiveThreadProcess);
            m_hReceiveThread.Start();

            // ch:开始采集 | en:Start Grabbing
            int nRet = m_MyCamera.MV_CC_StartGrabbing_NET();
            if (MyCamera.MV_OK != nRet)
            {
                m_bGrabbing = false;
                ShowErrorMsg("Start Grabbing Fail!", nRet);
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
            // ch:标志位设为false | en:Set flag bit false
            m_bGrabbing = false;

            // ch:停止采集 | en:Stop Grabbing
            int nRet = m_MyCamera.MV_CC_StopGrabbing_NET();
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Stop Grabbing Fail!", nRet);
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
                if (m_bGrabbing)
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
                tbExposure.Text = stParam.fCurValue.ToString("F1");
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
                float.Parse(tbExposure.Text);
                float.Parse(tbGain.Text);
                float.Parse(tbFrameRate.Text);
            }
            catch
            {
                ShowErrorMsg("Please enter correct type!", 0);
                return;
            }

            m_MyCamera.MV_CC_SetEnumValue_NET("ExposureAuto", 0);
            int nRet = m_MyCamera.MV_CC_SetFloatValue_NET("ExposureTime", float.Parse(tbExposure.Text));
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
            m_bGrabbing = false;
            writeableBitmap = null;
            imgDisplay.Dispose();
            bnClose_Click(null, null);

            // ch: 反初始化SDK | en: Finalize SDK
            MyCamera.MV_CC_Finalize_NET();
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
            try
            {
                float.Parse(tbExposure.Text);
            }
            catch
            {
                ShowErrorMsg("Please enter correct type!", 0);
                return;
            }

            m_MyCamera.MV_CC_SetEnumValue_NET("ExposureAuto", 0);
            int nRet = m_MyCamera.MV_CC_SetFloatValue_NET("ExposureTime", float.Parse(tbExposure.Text));
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Set Exposure Time Fail!", nRet);
            }

            UpdateStatusBar();
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
            if (StatusExposureText != null && !string.IsNullOrEmpty(tbExposure.Text))
            {
                StatusExposureText.Text = tbExposure.Text;
            }
            if (StatusGainText != null && !string.IsNullOrEmpty(tbGain.Text))
            {
                StatusGainText.Text = tbGain.Text;
            }
            if (StatusFrameRateText != null && !string.IsNullOrEmpty(tbFrameRate.Text))
            {
                StatusFrameRateText.Text = tbFrameRate.Text;
            }
        }
    }
}
