using CsharpDEMO;
using cvColorVision;
using StructTestN;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVisionTool
{
    public interface IMove
    {
        public void Move(int step);
        public double GetPosition();
        public double GetCalArtculation();
    }

    public class AutoFocusConfig
    {
        public int Step { get; set; } = 1000;
        public int StepOver { get; set; } = 10;

    }


    public class HardwareControl: IMove
    {

        public nint motorHandle;
        public nint camHandle;

        public void Init()
        {
            IntPtr pp = new IntPtr(11);

            //初始化dll，这个函数在最先必须调且只能调一次！！
            //cvCameraCSLib.InitResource(deviceMonitor, pp);

            //建立相机句柄
            //IntPtr camHandle = cvCameraCSLib.CM_CreatCameraManagerSimple(atuoCfg.dcf);

            if (camHandle == IntPtr.Zero)
            {
                Console.WriteLine("创建句柄失败！请查看log");
                //return;
            }

            //设置镜头COM口,查看设备管理器来设置
            //int success = cvCameraCSLib.CM_SetComSimple(camHandle, 1, atuoCfg.focusCOM);
            //if (success != 1)
            //{

            //    Console.WriteLine("CM_SetComSimple失败！请查看log");
            //    return;
            //}

            //IntPtr motorHandle = IntPtr.Zero;
            //success = cvCameraCSLib.CM_CreatChildHandle(camHandle, ref motorHandle, (byte)atuoCfg.motorNum);

            ////首先回到初始位置
            ////success = cvCameraCSLib.GoHome(motorHandle);


            //success = cvCameraCSLib.MoveDiaphragm(motorHandle, 5.6f);

            //if (success != 1)
            //{
            //    Console.WriteLine("设置对焦环失败！");
            //}

        }


        public void Move(int step)
        {
            int res = cvCameraCSLib.MoveAbsPostion(motorHandle, (int)Position + step);
        }

        private double Position = 0;

        public double GetPosition()
        {
            int pos = 0;
            int res = cvCameraCSLib.GetPosition(motorHandle, ref pos, 5000);
            Position = pos;
            return pos;
        }

        public double GetCalArtculation()
        {
            uint w = 0, h = 0, srcbpp = 0, bpp = 0, channels = 0;

            cvCameraCSLib.CM_SetExpTimeSimple(camHandle, 20);  //设置曝光
            cvCameraCSLib.CM_GetSrcFrameInfo(camHandle, ref w, ref h, ref srcbpp, ref channels);

            byte[] src = new byte[srcbpp / 8 * w * h * channels];           //灰度数据每个像素点占两字节
            byte[] imgdata = new byte[w * h * channels * 4];       //通道值数

            int res = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
            if (res != 1)
            {
                Console.WriteLine("fail to take img");
                return 0;
            }

            HImage tHimage = new HImage();
            tHimage.nHeight = h;
            tHimage.nWidth = w;
            tHimage.nChannels = channels;
            tHimage.nBpp = srcbpp;
            unsafe
            {
                fixed (byte* tp = src)
                {
                    tHimage.pData = new IntPtr(tp);
                }
            }
            autoFocusCfg cfgObj = new autoFocusCfg();


            double averageLevel = cvCameraCSLib.cvCalArticulation(EvaFunc.fun5, tHimage, cfgObj.EdgeFocus.offy, cfgObj.EdgeFocus.d, cfgObj.EdgeFocus.w, 0.01, cfgObj.EdgeFocus.h, cfgObj.EdgeFocus.nStep, cfgObj.EdgeFocus.nMaxCount);
            return averageLevel;
        }
    }

    public class AutoFocus
    {
        public void MountainClimbing(IMove move, AutoFocusConfig autoFocusConfig)
        {
            Dictionary<double, double> records = new Dictionary<double, double>();
            double preValue = move.GetCalArtculation();
            double prePosition = move.GetPosition();
            records.Add(prePosition, preValue);

            int step = autoFocusConfig.Step;

            int Minindex = 0;
            bool first = true;
            while (Math.Abs(step) > autoFocusConfig.StepOver)
            {
                move.Move((int)step);
                double Position = move.GetPosition();
                double Artculation = move.GetCalArtculation();
                if (records.TryAdd(Position, move.GetCalArtculation()))
                    records[Position] = move.GetCalArtculation();

                if (Artculation < records.Aggregate((l, r) => l.Value > r.Value ? l : r).Value)
                    Minindex++;

                if (Artculation > preValue)
                {
                    Minindex = 0;
                }
                else
                {
                    if (first)
                    {
                        first = false;
                        step = -step;
                        continue;
                    }
                    step = -step / 2;
                }
                preValue = Artculation;
            }

        }

    }



    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DemoType demoType = new DemoType();
            demoType.testMotor();
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {

        }
    }
}