﻿using OpenCvSharp;
using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Resources;

namespace ColorVision.Engine.Media
{

    public class ColorMap
    {

        public static Mat StreamToMat(Stream stream)
        {
            // 将Stream转换为byte数组
            byte[] bytes = StreamToBytes(stream);

            // 创建Mat对象
            Mat mat = Mat.FromImageData(bytes);

            return mat;
        }

        public static byte[] StreamToBytes(Stream stream)
        {
            // 创建一个临时byte数组存储stream的数据
            byte[] bytes = new byte[stream.Length];
            // 将stream的位置重置到开始
            stream.Position = 0;
            // 将stream的数据读取到byte数组中
            stream.Read(bytes, 0, bytes.Length);

            return bytes;
        }





        private int depth = 28;
        public Mat srcColor { get; set; }
        public Color[] colorMap { get; set; }
        public int[] colorMapIdx { get; set; }
        public double minLut { get; set; }

        public double maxLut { get; set; }

        public double[] stepPer { get; set; }//每一个伪彩色等级占用的像素

        public ColorMap()
        {
            StreamResourceInfo stream = Application.GetResourceStream(new Uri($"/ColorVision.Engine;component/Assets/Image/pictureBox1.Image.png", UriKind.Relative));

            byte[] bytes = StreamToBytes(stream.Stream);

            // 创建Mat对象
            srcColor = Mat.FromImageData(bytes);
            buildCustomMap(28, 0, 255);
            minLut = 0;
            maxLut = 255;
        }

        public ColorMap(string fileName, int colorMapNum)
        {
            if (File.Exists(fileName))
            {
                srcColor = Cv2.ImRead(fileName, ImreadModes.AnyColor);
                buildCustomMap(colorMapNum, 0, 100);
            }
            else
            {
                //log.Error("加载ColorMap失败，请确认当前目录是否存在colormap.png");
            }
        }
        private double getStepVal(Mat src)//计算输入图片的伪彩色步长
        {
            double minVal = 0, maxVal = 512;
            //获取一下当前图片的最大最小值
            Cv2.MinMaxIdx(src, out minVal, out maxVal);
            //平均算出步长
            double setp = (maxVal - minVal) / (depth - 1);
            //AddToLog(string.Format("minVal={0},maxVal={1},setp={2}", minVal, maxVal, setp), false);
            return setp;
        }


        public void ApplyLUTColorMap(Mat src, Mat dst)
        {
            //算图片平均步长
            double step = getStepVal(src);
            byte[] lutData = new byte[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                int idx = GetValOfColorMapIdx((byte)i, step);
                lutData[i * 3 + 0] = colorMap[idx].B;
                lutData[i * 3 + 1] = colorMap[idx].G;
                lutData[i * 3 + 2] = colorMap[idx].R;
            }
            Mat src8uc3 = new();
            Cv2.CvtColor(src.Clone(), src8uc3, ColorConversionCodes.GRAY2BGR);
            Mat lut = Mat.FromPixelData(1, 256, MatType.CV_8UC3, lutData);
            Cv2.LUT(src8uc3, lut, dst);
            // = null;
            //lut = null;
            //src8uc3 = null;
            GC.Collect();
        }
        public void ApplyLUTColorMap2(Mat src, Mat dst)
        {
            //算图片平均步长
            double[] step = stepPer;//这里的步长不用固定值用数组
            byte[] lutData = new byte[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                int idx = GetValOfColorMapIdx((byte)i, step);
                lutData[i * 3 + 0] = colorMap[idx].B;
                lutData[i * 3 + 1] = colorMap[idx].G;
                lutData[i * 3 + 2] = colorMap[idx].R;

            }
            Mat src8uc3 = new();
            Cv2.CvtColor(src.Clone(), src8uc3, ColorConversionCodes.GRAY2BGR);
            Mat lut = Mat.FromPixelData(1, 256, MatType.CV_8UC3, lutData);
            Cv2.LUT(src8uc3, lut, dst);
            // = null;
            //lut = null;
            //src8uc3 = null;
            GC.Collect();
        }

        public unsafe void ApplyColorMap(Mat src, Mat dst)
        {
            double step = getStepVal(src);

            for (int row = 0; row < src.Rows; row++)
            {
                byte* rowDataSrc = (byte*)src.Ptr(row);
                byte* rowDataDst = (byte*)dst.Ptr(row);
                string rowData = "";
                for (int col = 0; col < src.Cols; col++)
                {
                    int idx = GetValOfColorMapIdx(rowDataSrc[col], step);
                    rowData += string.Format("{0:D3}[{1}],", rowDataSrc[col], idx);
                    idx = GetColorMapIdx(idx);
                    rowDataDst[col * 3 + 0] = colorMap[idx].B;
                    rowDataDst[col * 3 + 1] = colorMap[idx].G;
                    rowDataDst[col * 3 + 2] = colorMap[idx].R;
                }
                //Console.WriteLine(rowData);
            }
        }

        private int GetColorMapIdx(int idx)
        {
            int colorIdx = colorMap.Length / depth * idx;
            return colorIdx;
        }

        private int GetValOfColorMapIdx(byte val, double step)
        {
            if (step < 0.000000001f) return 0;
            int idx = (int)(val / step);
            if (idx >= depth) idx = depth - 1;
            return idx;
        }

        private int GetValOfColorMapIdx(byte val, double[] step)//计算从0-255每一个像素应该显示的伪彩色颜色
        {
            double stepData = 0;//伪彩色的起始值即是0
            int idx = 0;//初始化伪彩色颜色编号
            for (int i = 0; i < step.Length; i++)
            {
                //if (step[i] < 0.000000001f) return 0;
                stepData = stepData + step[i];//计算出第i个等级的时候的结束像素
                if (val >= stepData)
                {
                    idx = idx + 1;
                }
                else
                {
                    break;
                }

            }
            if (idx >= depth) idx = depth - 1;
            return idx;
        }

        public void reMap()
        {
            depth = colorMapIdx.Length;
        }

        public Mat DrawSrcMap()
        {
            Mat cm = new(srcColor.Rows, srcColor.Cols + 150, srcColor.Type(), Scalar.All(255));
            Mat cmRt = cm[new OpenCvSharp.Rect(0, 0, srcColor.Cols, srcColor.Rows)];
            srcColor.Clone().CopyTo(cmRt);
            cmRt = cm[new OpenCvSharp.Rect(srcColor.Cols, 0, 150, srcColor.Rows)];
            for (int i = 0; i < colorMapIdx.Length; i++)
            {
                cmRt.Line(0, colorMapIdx[i], 50, colorMapIdx[i], Scalar.All(0));
                string valText = string.Format("{0}", i + 1);
                if (i == 0)
                    cmRt.PutText(valText, new OpenCvSharp.Point(60, colorMapIdx[i] + 20), HersheyFonts.HersheyDuplex, 1, Scalar.All(0));
                else
                    cmRt.PutText(valText, new OpenCvSharp.Point(60, colorMapIdx[i]), HersheyFonts.HersheyDuplex, 1, Scalar.All(0));

            }
            return cm;
        }

        private static byte[,] ConvertToBytes(Mat src, int cols)
        {
            src.ConvertTo(src, MatType.CV_8UC1);
            bool isRow = src.Rows > src.Cols;
            int n = isRow ? src.Rows : src.Cols;
            byte[,] data = new byte[n, cols];
            for (int i = 0; i < n; i++)
            {
                if (isRow)
                {
                    byte d = src.At<byte>(i, 0);
                    for (int j = 0; j < cols; j++)
                    {
                        data[i, j] = d;
                    }
                }
                else
                {
                    byte d = src.At<byte>(0, i);
                    for (int j = 0; j < cols; j++)
                    {
                        data[i, j] = d;
                    }
                }
            }
            return data;
        }
        public static float[] linspace0(float x0, float x1, int n, double[] step)
        {
            float[] pts = new float[n];
            float startX = x0;
            for (int i = 0; i < n; i++)
            {
                pts[i] = startX;
                startX = (float)(startX + (x1 - x0) * step[i] / 100);
            }
            return pts;
        }
        public static Mat linspace(float x0, float x1, int n, double[] step)
        {
            return Mat.FromPixelData(n, 1, MatType.CV_32FC1, linspace0(x0, x1, n, step));
        }



        public void buildCustomMap(int n, double min, double max)//初始化colormap
        {
            minLut = min;
            maxLut = max;
            double[] stepColorPer = new double[n];
            for (int i = 0; i < n; i++)
            {
                stepColorPer[i] = (double)100 / (n - 1);
            }
            Mat m = linspace(0, 1535, n, stepColorPer);//这里是算每一种等级实际显示的颜色
            Mat mClr = new();
            m.ConvertTo(mClr, MatType.CV_16UC1);
            colorMapIdx = new int[n];
            colorMap = new Color[n];
            stepPer = new double[n];
            double perStepPiex = 0;
            double calPiex = max - min;
            perStepPiex = calPiex / n;
            double startPiex = min;
            for (int i = 0; i < mClr.Rows; i++)
            {
                colorMapIdx[i] = mClr.Get<ushort>(i, 0);
                Vec3b cv = srcColor.Get<Vec3b>(colorMapIdx[i], 0);
                colorMap[i] = Color.FromArgb(cv.Item2, cv.Item1, cv.Item0);
                stepPer[i] = perStepPiex;//算出每个伪彩色等级占用的像素数量，现在是用均分的，后面的再说
                if (i == 0)
                {
                    stepPer[i] = stepPer[i] + startPiex;
                }
            }

        }
    }
}
