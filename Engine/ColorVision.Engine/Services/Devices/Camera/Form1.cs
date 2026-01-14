#pragma warning disable
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;

using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsTest
{
    public struct HImage
    {
        public uint nWidth;
        public uint nHeight;
        public uint nChannels;
        public uint nBpp;
        public IntPtr pData;
    };

    [JsonObject(MemberSerialization.OptIn)]
    public partial class Form1 : Form
    {
        public delegate void ImageShowEvent(int w, int h, byte[] rawArray, bool isBit8);
        public ImageShowEvent event_ShowImage;
        private Bitmap pictureBoxBitmap = null;

        public byte[] rawArray;
        public byte[] srcrawArray;
        public UInt32 ImgWid = 5544, ImgHei = 3684;
        public UInt32 Imgbpp = 8, Imgchannels = 1;
        public bool m_bImgMoveing = false;
        private System.Drawing.Point m_ImgCurPoint;
        public IntPtr m_hCamHandle = IntPtr.Zero;
        private string filename = null;
        public FormCfg formcfg = null;

        //[JsonProperty]
        //CameraType m_eCameraType = CameraType.BV_Q;
        [JsonProperty]
        TakeImageMode m_etakeImageMode = TakeImageMode.Measure_Normal;
        [JsonProperty]
        int m_nBppIndex = 1;
        [JsonProperty]
        CameraModel m_eCameraMdl = CameraModel.QHY_USB;
        [JsonProperty]
        CameraMode m_eCameraMode = CameraMode.CV_MODE;

        public string strPathSysCfg = "cfg\\sys.cfg";

        public string strLoadname = "cfg//Form1.cfg";

        public Form1()
        {
            if (File.Exists(strLoadname))
            {
                string json = System.IO.File.ReadAllText(strLoadname);
                JsonConvert.PopulateObject(json, this);
            }
            m_cDib = new PhotoShow.CDIb();

            InitializeComponent();

            filename = Application.StartupPath + "\\Form1Config.cfg";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
            m_hCamHandle = cvCameraCSLib.CM_CreatCameraManagerV1(m_eCameraMdl, m_eCameraMode, strPathSysCfg);
            cvCameraCSLib.CM_InitXYZ(m_hCamHandle);
            formcfg = new FormCfg(m_hCamHandle, strPathSysCfg);

            //cvCameraCSLib.CM_SetCV_MIL_CLParam(m_hCamHandle, "COM3", 9600);
            m_cDib.Initial(pictureBox1.Width, pictureBox1.Height);

            tb_TiffPath.Text = System.Windows.Forms.Application.StartupPath + "\\TIFF";
            System.IO.Directory.CreateDirectory(tb_TiffPath.Text);

            cb_CM_TYPE.SelectedIndex = (int)m_eCameraMdl;
            cb_CM_MODE.SelectedIndex = (int)m_eCameraMode;
            cb_get_mode.SelectedIndex = (int)m_etakeImageMode;
            cb_bpp.SelectedIndex = m_nBppIndex;

            btn_close.Enabled = false;
            btn_Meas.Enabled = false;
            btn_MeasTif.Enabled = false;
            button1.Enabled = false;
            btn_CalAutoExp.Enabled = true;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
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

        private void Form1_Resize(object sender, EventArgs e)
        {
        }

        private void OnDraw()
        {
            pictureBox1.Invalidate();
        }

        private void buttonUIEnable(bool enable)
        {
            btn_Connect.Enabled = enable;
            btn_MeasTif.Enabled = enable;
            btn_CalAutoExp.Enabled = enable;
        }

        cvCameraCSLib.QHYCCDProcCallBack callback;

        AutoResetEvent m_hShowPictureEvent = new AutoResetEvent(false);
        EventWaitHandle m_hStopEvent = new EventWaitHandle(false, EventResetMode.ManualReset);

        TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

        static UInt64 QHYCCDProcCallBackFunction(int enumImgType, IntPtr pData, int nW, int nH, int lss, int bpp
            , int channels, IntPtr usrData)
        {
            Form1 form = (Form1)GCHandle.FromIntPtr(usrData).Target;
            int sizeBpp = bpp / 8;
            Marshal.Copy(pData, form.rawArray, 0, (int)(nH * nW * channels * sizeBpp));
            form.ImgWid = (uint)nW;
            form.ImgHei = (uint)nH;
            form.Imgbpp = (uint)bpp;
            form.Imgchannels = (uint)channels;
            form.m_hShowPictureEvent.Set();

            TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
            TimeSpan abs = end.Subtract(form.start).Duration();
            Console.WriteLine("QHYCCDProcCallBackFunction {0}", abs.TotalMilliseconds);
            //form.Text = string.Format("QHYCCDProcCallBackFunction {0}", abs.TotalMilliseconds);
            form.start = end;

            return 0;
        }
        private PhotoShow.CDIb m_cDib;

        static void ShowPictureProc(object obj)
        {
            Form1 form = obj as Form1;

            while (form.m_hStopEvent.WaitOne(0) == false)
            {
                WaitHandle[] waitHandles = { form.m_hStopEvent, form.m_hShowPictureEvent };
                int nRet = WaitHandle.WaitAny(waitHandles);

                switch (nRet)
                {
                    case 0:
                    default:
                        break;
                    case 1:
                        {
                            form.m_cDib.InputImg(form.rawArray, (int)form.Imgbpp, (int)form.Imgchannels, (int)form.ImgWid, (int)form.ImgHei);
                            form.OnDraw();
                            break;
                        }
                }
            }

            Console.WriteLine("ShowPictureProc Thread end");
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBoxBitmap != null)
            {
                pictureBoxBitmap.Dispose();
                pictureBoxBitmap = null;
            }

            Bitmap bmp = new Bitmap(pictureBox1.Width, pictureBox1.Height);

            Graphics g = Graphics.FromImage(bmp);

            if (m_cDib.SrcBitmap != null)
            {
                m_cDib.Draw(g);

                double scale = m_cDib.GetScale();
                int nStartX = m_cDib.GetStartX();
                int nStartY = m_cDib.GetStartY();

                int w = m_cDib.m_iImgWid;
                int h = m_cDib.m_iImgHei;

                if (m_bFiveDot)
                {
                    m_bFiveDot = false;

                    Pen pen = new Pen(Color.Red, 1);
                    SolidBrush grayBrush = new SolidBrush(Color.Red);
                    Font newFont = new Font("宋体", 15);

                    string Text;
                    for (int i = 0; i < 4; i++)
                    {
                        int I2 = i + 1;

                        if (I2 > 3)
                        {
                            I2 = I2 - 4;
                        }

                        int nR = 5;

                        g.DrawEllipse(pen, (int)(scale * arrX[i] + nStartX) - nR, (int)(scale * arrY[i] + nStartY) - nR, nR * 2, nR * 2);

                        int x1 = (int)(scale * arrX[i] + nStartX);
                        int y1 = (int)(scale * arrY[i] + nStartY);

                        int x2 = (int)(scale * arrX[I2] + nStartX);
                        int y2 = (int)(scale * arrY[I2] + nStartY);

                        g.DrawLine(pen, x1, y1, x2, y2);

                        double dValue = Calclength(x1, y1, x2, y2);

                        Text = dValue.ToString("F2");

                        if (i % 2 == 0)
                        {
                            g.DrawString(Text, newFont, grayBrush, new PointF(((x1 + x2) / 2), y1 - 10));
                        }
                        else
                        {
                            g.DrawString(Text, newFont, grayBrush, new PointF(x1, (y1 + y2) / 2 - 10));
                        }
                    }
                }

                if (scr_bDrawStart)
                {
                    System.Drawing.Pen pen = new System.Drawing.Pen(Color.Red);
                    pen.Width = 1.5f;
                    //实时的画矩形
                    if (rect_scrImg != null && rect_scrImg.Width > 0 && rect_scrImg.Height > 0)
                    {
                        g.DrawRectangle(new Pen(Color.Red, 1.0f), rect_scrImg);//重新绘制颜色为红色
                    }
                }
            }

            pictureBoxBitmap = bmp;

            if (pictureBoxBitmap != null)
            {
                e.Graphics.DrawImage(pictureBoxBitmap, new Rectangle(0, 0, pictureBox1.Width, pictureBox1.Height)
                        , (int)0, (int)0, pictureBoxBitmap.Width, pictureBoxBitmap.Height, GraphicsUnit.Pixel);
            }
        }

        double Calclength(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
        }


        private string buildParam(string savePath, string exts)
        {
            GetFrameParam param = new GetFrameParam();

            if (cb_Channels.SelectedIndex == 0)
            {
                param.channelCount = 1;
            }
            else
            {
                param.channelCount = 3;
            }

            param.measureCount = 1;
            param.title = "";
            param.ob = 4;
            param.obR = 0;
            param.obT = 0;
            param.obB = 0;

            param.startBurst =1;
            param.endBurst = 3;
            param.posBurst = 0;

            param.autoExpFlag = cb_AutoExp.Checked;

            if (formcfg.m_bV1)
            {
                foreach (var item in formcfg.calibV.listItem)
                {
                    if (/*cb_FourColorCorrect.Checked && */item.type == CalibrationType.LumFourColor)
                    {
                        param.lumChromaCheck = new CalibrationItem(item.type, item.enable, item.title, "");
                    }

                    if (/*cb_MulColorCorrect.Checked && */item.type == CalibrationType.LumMultiColor)
                    {
                        param.lumChromaCheck = new CalibrationItem(item.type, item.enable, item.title, "");
                    }

                    if (/*cb_MonoCorrect.Checked && */item.type == CalibrationType.LumOneColor)
                    {
                        param.lumChromaCheck = new CalibrationItem(item.type, item.enable, item.title, "");
                    }

                    if (/*cb_LumCorrect.Checked && */item.type == CalibrationType.Luminance)
                    {
                        param.lumChromaCheck = new CalibrationItem(item.type, item.enable, item.title, "");
                    }
                }
            }
            else
            {
                if (formcfg.projectSysCfg != null && formcfg.projectSysCfg.calibrationLibCfg != null)
                    foreach (var item in formcfg.projectSysCfg.calibrationLibCfg)
                    {
                        if (cb_FourColorCorrect.Checked && item.type == CalibrationType.LumFourColor)
                        {
                            param.lumChromaCheck = new CalibrationItem(item.type, cb_FourColorCorrect.Checked, item.title, "");
                        }

                        if (cb_MulColorCorrect.Checked && item.type == CalibrationType.LumMultiColor)
                        {
                            param.lumChromaCheck = new CalibrationItem(item.type, cb_MulColorCorrect.Checked, item.title, "");
                        }

                        if (cb_MonoCorrect.Checked && item.type == CalibrationType.LumOneColor)
                        {
                            param.lumChromaCheck = new CalibrationItem(item.type, cb_MonoCorrect.Checked, item.title, "");
                        }

                        if (cb_LumCorrect.Checked && item.type == CalibrationType.Luminance)
                        {
                            param.lumChromaCheck = new CalibrationItem(item.type, cb_LumCorrect.Checked, item.title, "");
                        }
                    }
            }

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

            if (formcfg.m_bV1)
            {
                param.calibrationlist = new List<CalibrationItem>();

                for (int i = 0; i < formcfg.calibV.listItem.Count; i++)
                {
                    //if (cb_DarkNoiseCorrect.Checked && formcfg.calibV.listItem[i].type == CalibrationType.DarkNoise)
                    //{
                    //    param.calibrationlist.Add(new CalibrationItem(formcfg.calibV.listItem[i].type
                    //    , true, formcfg.calibV.listItem[i].title, ""));
                    //}

                    //if (cb_DSNU.Checked && formcfg.calibV.listItem[i].type == CalibrationType.DSNU)
                    //{
                    //    param.calibrationlist.Add(new CalibrationItem(formcfg.calibV.listItem[i].type
                    //    , true, formcfg.calibV.listItem[i].title, ""));
                    //}

                    //if (cb_UniformFieldCorrect.Checked && formcfg.calibV.listItem[i].type == CalibrationType.Uniformity)
                    //{
                    //    param.calibrationlist.Add(new CalibrationItem(formcfg.calibV.listItem[i].type
                    //    , true, formcfg.calibV.listItem[i].title, ""));
                    //}

                    //if (cb_BadPixelCorrect.Checked && formcfg.calibV.listItem[i].type == CalibrationType.DefectPoint)
                    //{
                    //    param.calibrationlist.Add(new CalibrationItem(formcfg.calibV.listItem[i].type
                    //    , true, formcfg.calibV.listItem[i].title, ""));
                    //}

                    //if (cb_DistortCorrect.Checked && formcfg.calibV.listItem[i].type == CalibrationType.Distortion)
                    //{
                    //    param.calibrationlist.Add(new CalibrationItem(formcfg.calibV.listItem[i].type
                    //    , true, formcfg.calibV.listItem[i].title, ""));

                    //}
                    switch (formcfg.calibV.listItem[i].type)
                    {
                        case CalibrationType.ColorShift:
                        case CalibrationType.Distortion:
                        case CalibrationType.Uniformity:
                        case CalibrationType.DSNU:
                        case CalibrationType.DefectPoint:
                        case CalibrationType.DefectBPoint:
                        case CalibrationType.DefectWPoint:
                        case CalibrationType.DarkNoise:
                            param.calibrationlist.Add(new CalibrationItem(formcfg.calibV.listItem[i].type
                                , formcfg.calibV.listItem[i].enable, formcfg.calibV.listItem[i].title, ""));
                            break;
                        default:
                            break;
                    }
                }
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

                if (cb_DarkNoiseCorrect.Checked && formcfg.calibCfg.strDarkNoiseCali != "")
                {
                    channelCheck.DarkNoiseCheck = new CalibrationItem(CalibrationType.DarkNoise, true, formcfg.calibCfg.strDarkNoiseCali, "");
                }

                if (cb_DSNU.Checked && formcfg.calibCfg.szDSNUCali[i] != "")
                {
                    channelCheck.dsnuCheck = new CalibrationItem(CalibrationType.DSNU, true, formcfg.calibCfg.szDSNUCali[i], "");
                }

                if (cb_UniformFieldCorrect.Checked && formcfg.calibCfg.szUniformCali[i] != "")
                {
                    channelCheck.uniformityCheck = new CalibrationItem(CalibrationType.Uniformity, true, formcfg.calibCfg.szUniformCali[i], "");
                }

                if (cb_BadPixelCorrect.Checked && formcfg.calibCfg.strDefectCali != "")
                {
                    channelCheck.defectCheck = new CalibrationItem(CalibrationType.DefectPoint, true, formcfg.calibCfg.strDefectCali, "");
                }

                if (cb_DistortCorrect.Checked && formcfg.calibCfg.szDistortCali[i] != "")
                {
                    channelCheck.distortionCheck = new CalibrationItem(CalibrationType.Distortion, true, formcfg.calibCfg.szDistortCali[i], "");
                }

                param.channels.Add(channel);
            }

            if (savePath != null && exts != null)
                param.BuildChannelsFileName(savePath, exts);

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(param);

            return json;
        }

        private void cb_CM_TYPE_SelectedIndexChanged(object sender, EventArgs e)
        {
            m_eCameraMdl = (CameraModel)cb_CM_TYPE.SelectedIndex;

            cvCameraCSLib.CM_SetCameraModel(m_hCamHandle, m_eCameraMdl, m_eCameraMode);

            cb_CM_ID.Items.Clear();
            string szText = "";
            if (cvCameraCSLib.GetAllCameraIDV1(m_eCameraMdl, ref szText))
            {
                JObject jObject = (JObject)JsonConvert.DeserializeObject(szText);

                if (jObject["ID"] != null)
                {
                    JToken[] data = jObject["ID"].ToArray();

                    for (int i = 0; i < data.Length; i++)
                    {
                        cb_CM_ID.Items.Add(data[i].ToString());
                    }

                    if (cb_CM_ID.Items.Count > 0)
                        cb_CM_ID.SelectedIndex = 0;
                }
            }

            UInt32 nChls = 0;

            if (cvCameraCSLib.CM_GetChannels(m_hCamHandle, ref nChls))
            {
                if (nChls == 1)
                    cb_Channels.SelectedIndex = 0;
                else
                    cb_Channels.SelectedIndex = 1;
            }
        }

        private void cb_CM_ID_SelectedIndexChanged(object sender, EventArgs e)
        {
            cvCameraCSLib.CM_SetCameraID(m_hCamHandle, cb_CM_ID.Text);
        }

        private void cb_get_mode_SelectedIndexChanged(object sender, EventArgs e)
        {
            m_etakeImageMode = (TakeImageMode)cb_get_mode.SelectedIndex;
        }

        private void cb_bpp_SelectedIndexChanged(object sender, EventArgs e)
        {
            m_nBppIndex = cb_bpp.SelectedIndex;
        }

        private void btn_MeasConnect_Click(object sender, EventArgs e)
        {
            if (cb_CM_ID.Text == "")
            {
                MessageBox.Show("没有相机ID!");
                return;
            }

            if (cvCameraCSLib.CM_IsOpen(m_hCamHandle))
            {
                cb_get_mode.Enabled = false;
                cb_bpp.Enabled = false;
                btn_Connect.Enabled = false;
                cb_CM_ID.Enabled = false;
                return;
            }

            int nErr = cvErrorDefine.CV_ERR_UNKNOWN;

//             if ((nErr = cvCameraCSLib.CM_ResetEx(m_hCamHandle)) != cvErrorDefine.CV_ERR_SUCCESS)
//             {
//                 string szMsg = "";
// 
//                 cvCameraCSLib.CM_GetErrorMessage(nErr, ref szMsg);
// 
//                 MessageBox.Show(szMsg);
// 
//                 btn_Connect.Enabled = true;
// 
//                 return;
//             }
// 
            cvCameraCSLib.CM_SetTakeImageMode(m_hCamHandle, m_etakeImageMode);

            if (m_nBppIndex == 0)
            {
                cvCameraCSLib.CM_SetImageBpp(m_hCamHandle, 8);
            }
            else
            {
                cvCameraCSLib.CM_SetImageBpp(m_hCamHandle, 16);
            }

            btn_Connect.Enabled = false;

            if (m_etakeImageMode != TakeImageMode.Live)
            {

                if ((nErr = cvCameraCSLib.CM_Open(m_hCamHandle)) != cvErrorDefine.CV_ERR_SUCCESS)
                {
                    string szMsg = "";

                    cvCameraCSLib.CM_GetErrorMessage(nErr, ref szMsg);

                    MessageBox.Show(szMsg);

                    btn_Connect.Enabled = true;

                    return;
                }

                string sn = cvCameraCSLib.CM_GetSN(m_hCamHandle);
                string mode = cvCameraCSLib.CM_GetDeviceMode(m_hCamHandle);
                this.Text = "Model: COLOR VISION " + mode;

                cb_CM_TYPE.Enabled = false;
                cb_CM_MODE.Enabled = false;
                cb_CM_ID.Enabled = false;
                cb_get_mode.Enabled = false;
                cb_bpp.Enabled = false;
                btn_Connect.Enabled = false;
                btn_close.Enabled = true;
                btn_Meas.Enabled = true;
                button1.Enabled = true;
                btn_MeasTif.Enabled = true;
                btn_CalAutoExp.Enabled = true;
            }
            else
            {
                if ((nErr = cvCameraCSLib.CM_Open(m_hCamHandle)) != cvErrorDefine.CV_ERR_SUCCESS)
                {
                    string szMsg = "";

                    cvCameraCSLib.CM_GetErrorMessage(nErr, ref szMsg);

                    MessageBox.Show(szMsg);
                    btn_Connect.Enabled = true;
                    return;
                }

                cvCameraCSLib.CM_SetExpTime(m_hCamHandle, float.Parse(tb_Exp.Text));
                cvCameraCSLib.CM_SetGain(m_hCamHandle, float.Parse(tb_Gain.Text));

                rawArray = null;

                UInt32 w = 0, h = 0;
                UInt32 channels = 0;
                uint bpp = 0;

                cvCameraCSLib.CM_GetSrcFrameInfo(m_hCamHandle, ref w, ref h, ref bpp, ref channels);
                UInt64 nLen = (bpp / 8) * w * h * channels;
                if (nLen > 0)
                {
                    rawArray = new byte[nLen];
                }

                GCHandle hander = GCHandle.Alloc(this);
                IntPtr intPtrHandle = GCHandle.ToIntPtr(hander);

                if (callback == null)
                {
                    callback = new cvCameraCSLib.QHYCCDProcCallBack(QHYCCDProcCallBackFunction);
                }

                cvCameraCSLib.CM_SetCallBack(m_hCamHandle, callback, intPtrHandle);

                Thread showpictureThread = new Thread(() => ShowPictureProc(this));
                m_hStopEvent.Reset();
                showpictureThread.Start();

                cb_CM_TYPE.Enabled = false;
                cb_CM_MODE.Enabled = false;
                cb_CM_ID.Enabled = false;
                cb_get_mode.Enabled = false;
                cb_bpp.Enabled = false;
                btn_Connect.Enabled = false;
                btn_close.Enabled = true;

                cvCameraCSLib.CM_GetSrcFrameInfo(m_hCamHandle, ref w, ref h, ref bpp, ref channels);
                //H264Encoder.H264_Encoder_Setup((int)w,(int)h);
            }
        }

        private void btn_StopLive_Click(object sender, EventArgs e)
        {
            btn_close.Enabled = false;

            if (m_etakeImageMode == TakeImageMode.Live)
            {
                cvCameraCSLib.CM_UnregisterCallBack(m_hCamHandle);

                cvCameraCSLib.CM_Close(m_hCamHandle);
                m_hStopEvent.Set();

                cb_CM_TYPE.Enabled = true;
                cb_CM_MODE.Enabled = true;
                cb_CM_ID.Enabled = true;
                cb_get_mode.Enabled = true;
                cb_bpp.Enabled = true;
                btn_Connect.Enabled = true;
                btn_close.Enabled = false;
            }
            else
            {
                cvCameraCSLib.CM_Close(m_hCamHandle);

                cb_CM_TYPE.Enabled = true;
                cb_CM_MODE.Enabled = true;
                cb_CM_ID.Enabled = true;
                cb_get_mode.Enabled = true;
                cb_bpp.Enabled = true;
                btn_Connect.Enabled = true;
                btn_close.Enabled = false;
                btn_Meas.Enabled = false;
                btn_MeasTif.Enabled = false;
                button1.Enabled = false;
                btn_CalAutoExp.Enabled = false;
            }
        }

        UInt32 src_w = 0, src_h = 0;
        UInt32 src_bpp = 0, src_channels = 0;

        private void btn_Meas_Click(object sender, EventArgs e)
        {
            btn_Meas.Enabled = false;
            btn_MeasTif.Enabled = false;
            UInt32 w = 0, h = 0;
            UInt32 channels = 0;
            uint bpp = 0;
            UInt32 dstbpp = 32;
            //cvCameraCSLib.CM_SetCameraROI(m_hCamHandle, 2233, 1316, 600, 600);
            //cvCameraCSLib.fndllTest();
            cvCameraCSLib.CM_GetSrcFrameInfo(m_hCamHandle, ref w, ref h, ref bpp, ref channels);
            uint nLen = (bpp / 8) * w * h * channels;

            if (srcrawArray == null || srcrawArray.Length != nLen)
            {
                srcrawArray = new byte[bpp / 8 * w * h * channels];
                rawArray = new byte[dstbpp / 8 * w * h * channels];
            }
            srcrawArray[47] = 90;
            string json1 = buildParam(null, null);

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(json1);
            string msg = Encoding.UTF8.GetString(utf8Bytes);
            TimeSpan start = new TimeSpan(DateTime.Now.Ticks);
            int nErr = cvCameraCSLib.CM_GetFrame(m_hCamHandle, json1, ref w, ref h, ref bpp, ref dstbpp, ref channels, srcrawArray, rawArray);

            if (nErr != cvErrorDefine.CV_ERR_SUCCESS)
            {
                string szMsg = "";

                cvCameraCSLib.CM_GetErrorMessage(nErr, ref szMsg);

                MessageBox.Show(szMsg);
                btn_Connect.Enabled = true;
            }

            TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
            TimeSpan abs = end.Subtract(start).Duration();
            Console.WriteLine(string.Format("程序执行时间：{0}", abs.TotalMilliseconds));

            btn_Meas.Enabled = true;
            btn_MeasTif.Enabled = true;
            src_w = w;
            src_h = h;
            src_bpp = bpp;
            src_channels = channels;

            if (cb_FourColorCorrect.Checked ||
                cb_MulColorCorrect.Checked ||
                cb_MonoCorrect.Checked ||
                cb_LumCorrect.Checked)
            {
                cvCameraCSLib.CM_SetBufferXYZ(m_hCamHandle, w, h, dstbpp, channels, rawArray);
            }

            m_cDib.InputImg(srcrawArray, (int)bpp, (int)channels, (int)w, (int)h);

            OnDraw();
        }

        double[] arrX = new double[5] { 0, 0, 0, 0, 0 };
        double[] arrY = new double[5] { 0, 0, 0, 0, 0 };

        bool m_bFiveDot = false;

        private void btn_MeasTif_Click(object sender, EventArgs e)
        {
            btn_MeasTif.Enabled = false;
            btn_Meas.Enabled = false;

            string json1 = buildParam(tb_TiffPath.Text, ".tif");

            cvCameraCSLib.CM_GetFrame_TIFF(m_hCamHandle, json1);

            //IntPtr hHandle;
            //hHandle = cvCameraCSLib.CreatCameraManager(m_eCameraType, "00F95384296", "cfg\\sys.cfg");
            //cvCameraCSLib.CM_Open(hHandle);

            //btn_Meas.Enabled = false;
            //btn_MeasTif.Enabled = false;
            //rawArray = null;
            //if (rawArray == null)
            //{
            //    UInt64 len = cvCameraCSLib.CM_GetFrameMaxMemLength(hHandle);
            //    if (len > 0)
            //    {
            //        rawArray = new byte[len];
            //        srcrawArray = new byte[len];
            //    }
            //}

            //string json1 = buildParam(null, null);

            //UInt32 w = 0, h = 0;
            //UInt32 bpp = 0, channels = 0;

            //uint srcbpp = 0;
            //cvCameraCSLib.CM_GetFrame(hHandle, json1, ref w, ref h, ref srcbpp, ref bpp, ref channels, srcrawArray, rawArray);
            //btn_Meas.Enabled = true;
            //btn_MeasTif.Enabled = true;

            //if (cb_FourColorCorrect.Checked ||
            //    cb_MulColorCorrect.Checked ||
            //    cb_MonoCorrect.Checked ||
            //    cb_LumCorrect.Checked)
            //{
            //    cvCameraCSLib.CM_SetBufferXYZ(w, h, bpp, channels, rawArray);
            //}

            //m_cDib.InputImg(srcrawArray, (int)srcbpp, (int)channels, (int)w, (int)h);

            //OnDraw();
            //cvCameraCSLib.CM_Close(hHandle);

            //return;
        }

        private void btn_CalAutoExp_Click(object sender, EventArgs e)
        {
            //             timer2.Interval = 10;
            //             timer2.Enabled = true;
            // 
            //             return;
            btn_CalAutoExp.Enabled = false;
            float[] autoExp = new float[3];
            float[] Saturat = new float[3];

            if (btn_Connect.Enabled == false)
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

            btn_CalAutoExp.Enabled = true;
        }

        private void btn_StartLive_Click(object sender, EventArgs e)
        {
            cvCameraCSLib.CM_SetTakeImageMode(m_hCamHandle, TakeImageMode.Live);

            if (cvCameraCSLib.CM_Open(m_hCamHandle) == cvErrorDefine.CV_ERR_SUCCESS)
            {
                cvCameraCSLib.CM_SetExpTime(m_hCamHandle, float.Parse(tb_Exp.Text));
                cvCameraCSLib.CM_SetGain(m_hCamHandle, float.Parse(tb_Gain.Text));
                btn_close.Enabled = true;

                rawArray = null;

                UInt32 len = cvCameraCSLib.CM_GetFrameMemLength(m_hCamHandle);
                if (len > 0)
                {
                    rawArray = new byte[len];
                }

                GCHandle hander = GCHandle.Alloc(this);
                IntPtr intPtrHandle = GCHandle.ToIntPtr(hander);

                if (callback == null)
                {
                    callback = new cvCameraCSLib.QHYCCDProcCallBack(QHYCCDProcCallBackFunction);
                }

                cvCameraCSLib.CM_SetCallBack(m_hCamHandle, callback, intPtrHandle);

                Thread showpictureThread = new Thread(() => ShowPictureProc(this));
                m_hStopEvent.Reset();
                showpictureThread.Start();
            }
        }

        private void btn_SetExp_Click(object sender, EventArgs e)
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

        private void btn_Test_Click(object sender, EventArgs e)
        {

        }

        private void btn_ConfigFile_Click(object sender, EventArgs e)
        {
            formcfg.m_nChannelCount = int.Parse(cb_Channels.Text);
            formcfg.m_hHandle = m_hCamHandle;
            formcfg.ShowDialog();
        }

        private void pictureBox1_MouseEnter(object sender, EventArgs e)
        {
            pictureBox1.Focus();
        }

        private void pictureBox1_MouseLeave(object sender, EventArgs e)
        {
        }

        private void pictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {

        }

        bool scr_bDrawStart = false;//判断是否绘制
        System.Drawing.Point pointStart = System.Drawing.Point.Empty;//画框的起始点
        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (!checkBox1.Checked)
            {
                m_bImgMoveing = true;

                m_ImgCurPoint.X = e.X;
                m_ImgCurPoint.Y = e.Y;
            }
            else
            {
                scr_bDrawStart = true;
                pointStart = e.Location;

                //if (scr_bDrawStart)
                //{
                //    scr_bDrawStart = false;
                //}
                //else
                //{
                //    scr_bDrawStart = true;
                //    pointStart = e.Location;
                //}
            }


        }

        class FindRoi
        {
            public int x { set; get; }
            public int y { set; get; }
            public int width { set; get; }

            public int height { set; get; }
            public override string ToString()
            {
                return string.Format("{0},{1},{2},{3}", x, y, width, height);
            }
        }

        Rectangle rect_img;
        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {

        }

        System.Drawing.Point pointContinue = System.Drawing.Point.Empty;//画框的结束点
        Rectangle rect_scrImg;
        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {

        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Bounds.Contains(e.X, e.Y))
            {
                pictureBox1.Focus();
            }
        }

        private void cb_MonoCorrect_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_MonoCorrect.Checked)
            {
                cb_FourColorCorrect.Checked = false;
                cb_MulColorCorrect.Checked = false;
            }
        }

        private void cb_FourColorCorrect_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_FourColorCorrect.Checked)
            {
                cb_MonoCorrect.Checked = false;
                cb_MulColorCorrect.Checked = false;
            }
        }

        private void MenuItem_ShowCenterLine_Click(object sender, EventArgs e)
        {
            MenuItem_ShowCenterLine.Checked = !MenuItem_ShowCenterLine.Checked;
            pictureBox1.Invalidate();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            btn_Meas.Enabled = false;
            btn_MeasTif.Enabled = false;

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

            string json1 = buildParam(null, null);

            UInt32 w = 0, h = 0;
            UInt32 bpp = 0, channels = 0;

            uint srcbpp = 0;

            //cvCameraCSLib.ReadImage("ceshi001.tif", ref w, ref h, ref srcbpp, ref channels, ref srcrawArray);

            cvCameraCSLib.CM_GetFrame(m_hCamHandle, json1, ref w, ref h, ref srcbpp, ref bpp, ref channels, srcrawArray, rawArray);

            HImage tImg;

            tImg.nWidth = w;
            tImg.nHeight = h;
            tImg.nBpp = srcbpp;
            tImg.nChannels = channels;

            unsafe
            {
                fixed (byte* pAdr = srcrawArray)
                {
                    tImg.pData = (IntPtr)pAdr;
                }
            }

            m_cDib.InputImg(srcrawArray, (int)srcbpp, (int)channels, (int)w, (int)h);

            OnDraw();

            btn_Meas.Enabled = true;
            btn_MeasTif.Enabled = true;
            button1.Enabled = true;
        }

        private void btn_FOV_Click(object sender, EventArgs e)
        {

        }
        private void saveCsv_fovDegrees_ref(string path, string data)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            if (path.Substring(path.Length - 1, 1) != "/")
            {
                path = path + "\\FOVResult.csv";
            }
            else
            {
                path = path + "FOVResult.csv";
            }
            if (!File.Exists(path))
            {
                //首先模拟建立将要导出的数据，这些数据都存于DataTable中  
                System.Data.DataTable dt = new System.Data.DataTable();
                dt.Columns.Add("FovDegrees_ref", typeof(string));
                dt.Columns.Add("Time", typeof(string));
                System.IO.FileStream fs2 = new FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                StreamWriter sw2 = new StreamWriter(fs2, UnicodeEncoding.UTF8);
                //string path = saveFileDialog.FileName.ToString();//保存路径
                //Tabel header
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    if (i != 0)
                    {
                        sw2.Write(",");
                    }
                    sw2.Write(dt.Columns[i].ColumnName);
                }
                sw2.WriteLine("");
                sw2.Flush();
                sw2.Close();
                fs2.Close();
            }
            System.IO.FileStream fs = new FileStream(path, System.IO.FileMode.Append, System.IO.FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, UnicodeEncoding.UTF8);
            sw.Write(data);
            sw.Write(",");
            sw.Write(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
            sw.Write(",");
            sw.WriteLine("");
            sw.Flush();
            sw.Close();
            fs.Close();
        }

        FindRoi fRoi;
        private void btn_SFR_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
  
        }

        private void Camera_MenuItem_Click(object sender, EventArgs e)
        {

        }

        private void Channels_MenuItem_Click(object sender, EventArgs e)
        {

        }

        private void Calibration_MenuItem_Click(object sender, EventArgs e)
        {


        }

        private void ExpTime_MenuItem_Click(object sender, EventArgs e)
        {

        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            timer2.Enabled = false;

            float[] autoExp = new float[3];
            float[] Saturat = new float[3];

            if (btn_Connect.Enabled == false)
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

                    btn_Meas.Enabled = false;
                    btn_MeasTif.Enabled = false;
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

                    string json1 = buildParam(null, null);

                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                    cvCameraCSLib.CM_GetFrame(m_hCamHandle, json1, ref w, ref h, ref bpp, ref dstbpp, ref channels, srcrawArray, rawArray);

                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                    TimeSpan abs = end.Subtract(start).Duration();
                    Console.WriteLine(string.Format("程序执行时间：{0}", abs.TotalMilliseconds));

                    btn_Meas.Enabled = true;
                    btn_MeasTif.Enabled = true;
                    src_w = w;
                    src_h = h;
                    src_bpp = bpp;
                    src_channels = channels;

                    if (cb_FourColorCorrect.Checked ||
                        cb_MulColorCorrect.Checked ||
                        cb_MonoCorrect.Checked ||
                        cb_LumCorrect.Checked)
                    {
                        cvCameraCSLib.CM_SetBufferXYZ(m_hCamHandle, w, h, dstbpp, channels, rawArray);
                    }

                    m_cDib.InputImg(srcrawArray, (int)bpp, (int)channels, (int)w, (int)h);

                    OnDraw();
                }
                else
                {
                    MessageBox.Show("CM_GetAutoExpTime自动曝光失败！");
                }
            }

            timer2.Enabled = true;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            cvCameraCSLib.CM_SetCameraROI(m_hCamHandle, 500, 200, 800, 700);
            UInt32 ex = 0; UInt32 ey = 0; UInt32 ew = 0; UInt32 eh = 0;
            cvCameraCSLib.CM_GetCameraROI(m_hCamHandle, ref ex, ref ey, ref ew, ref eh);
        }

        private void btn_reset_Click(object sender, EventArgs e)
        {
            int nErr = cvErrorDefine.CV_ERR_UNKNOWN;

            if ((nErr = cvCameraCSLib.CM_Reset(m_hCamHandle)) != cvErrorDefine.CV_ERR_SUCCESS)
            {
                string szMsg = "";

                cvCameraCSLib.CM_GetErrorMessage(nErr, ref szMsg);

                MessageBox.Show(szMsg);

                btn_Connect.Enabled = true;

                return;
            }
        }

        private void cb_CM_MODE_SelectedIndexChanged(object sender, EventArgs e)
        {
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

        private void btn_Distortion_Click(object sender, EventArgs e)
        {
        }

        private void saveCsv_Distortion(string path, double pointx, double pointy, double maxErrorRatio, double t)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            if (path.Substring(path.Length - 1, 1) != "/")
            {
                path = path + "\\DistortionResult.csv";
            }
            else
            {
                path = path + "DistortionResult.csv";
            }
            if (!File.Exists(path))
            {
                //首先模拟建立将要导出的数据，这些数据都存于DataTable中  
                System.Data.DataTable dt = new System.Data.DataTable();
                dt.Columns.Add("Time", typeof(string));
                dt.Columns.Add("pointx", typeof(string));
                dt.Columns.Add("pointy", typeof(string));
                dt.Columns.Add("maxErrorRatio", typeof(string));
                dt.Columns.Add("t", typeof(string));
                System.IO.FileStream fs2 = new FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                StreamWriter sw2 = new StreamWriter(fs2, UnicodeEncoding.UTF8);
                //string path = saveFileDialog.FileName.ToString();//保存路径
                //Tabel header
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    if (i != 0)
                    {
                        sw2.Write(",");
                    }
                    sw2.Write(dt.Columns[i].ColumnName);
                }
                sw2.WriteLine("");
                sw2.Flush();
                sw2.Close();
                fs2.Close();
            }
            System.IO.FileStream fs = new FileStream(path, System.IO.FileMode.Append, System.IO.FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, UnicodeEncoding.UTF8);
            sw.Write(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
            sw.Write(",");
            sw.Write(pointx);
            sw.Write(",");
            sw.Write(pointy);
            sw.Write(",");
            sw.Write(maxErrorRatio);
            sw.Write(",");
            sw.Write(t);
            sw.Write(",");
            sw.WriteLine("");
            sw.Flush();
            sw.Close();
            fs.Close();
        }

        private void cb_MulColorCorrect_CheckedChanged(object sender, EventArgs e)
        {
            if (cb_MulColorCorrect.Checked)
            {
                cb_FourColorCorrect.Checked = false;
                cb_MonoCorrect.Checked = false;
            }
        }

        private void btn_SetGain_Click(object sender, EventArgs e)
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
    }
}
