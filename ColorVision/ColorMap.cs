using System;
using OpenCvSharp;
using System.Drawing;
using System.IO;

namespace ColorVision
{
    public class ColorMap
    {

        private int depth = 28;
        public Mat srcColor { get; set; }
        public Color[] colorMap { get; set; }
        public int[] colorMapIdx { get; set; }
        public double minLut { get; set; }

        public double maxLut { get; set; }

        public double[] stepPer { get; set; }//每一个伪彩色等级占用的像素

        public ColorMap(String fileName)
        {
            if (File.Exists(fileName))
            {
                srcColor = Cv2.ImRead(fileName, ImreadModes.AnyColor);
                buildCustomMap(28,0,255);
                minLut = 0;
                maxLut = 255;
            }
            else
            {
                //logger.Error("加载ColorMap失败，请确认当前目录是否存在colormap.png");
            }
        }
        public ColorMap(String fileName,int colorMapNum)
        {
            if (File.Exists(fileName))
            {
                srcColor = Cv2.ImRead(fileName, ImreadModes.AnyColor);
                buildCustomMap(colorMapNum,0,100);
            }
            else
            {
                //logger.Error("加载ColorMap失败，请确认当前目录是否存在colormap.png");
            }
        }
        //private void AddToLog(string txt, bool isDebug)
        //{
        //    if (isDebug)
        //        logger.Debug(txt);
        //    else
        //        logger.Info(txt);
        //}
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

        //private double[] getStepValPer(Mat src)//计算输入图片的伪彩色步长
        //{
        //    double minVal = 0, maxVal = 512;
        //    //获取一下当前图片的最大最小值
        //    Cv2.MinMaxIdx(src, out minVal, out maxVal);
        //    //计算出每个颜色的步长
        //    double[] setp = new double[depth];//一共有depth个步长数据
        //    if (minLut+ (100- maxLut) >=100)//如果上下限的和值超过了100，则认为上下限设置的有问题--minLut:20,maxLut:70,那么实际上要扣除的是20+（100-70）=50/100
        //    {
        //        return setp;
        //    }
        //    for (int i = 0; i < depth; i++)
        //    {
        //        double srcPicVal = maxVal - minVal;
        //        double dstPicVal = srcPicVal * (100 - minLut - (100 - maxLut)) / 100;//扣除了上下限之后剩余需要计算步长的像素数量
        //        setp[i] = dstPicVal * stepPer[i]/100;//计算出每一个伪彩色等级在扣除了上下限后所占的像素数量
        //    }
        //    return setp;
        //}

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
            Mat src8uc3 = new Mat();
            Cv2.CvtColor(src.Clone(), src8uc3, ColorConversionCodes.GRAY2BGR);
            Mat lut = new Mat(1, 256, MatType.CV_8UC3, lutData);
            Cv2.LUT(src8uc3, lut, dst);
            // = null;
            //lut = null;
            //src8uc3 = null;
            GC.Collect();
        }
        public void ApplyLUTColorMapNew(Mat src, Mat dst)
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
            Mat src8uc3 = new Mat();
            Cv2.CvtColor(src.Clone(), src8uc3, ColorConversionCodes.GRAY2BGR);
            Mat lut = new Mat(1, 256, MatType.CV_8UC3, lutData);
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
                String rowData = "";
                for (int col = 0; col < src.Cols; col++)
                {
                    int idx = GetValOfColorMapIdx(rowDataSrc[col], step);
                    rowData += String.Format("{0:D3}[{1}],", rowDataSrc[col], idx);
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
            int colorIdx = (colorMap.Length / depth) * idx;
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
                if (val>= stepData)
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
            Mat cm = new Mat(srcColor.Rows, srcColor.Cols + 150, srcColor.Type(), Scalar.All(255));
            Mat cmRt = cm[new Rect(0, 0, srcColor.Cols, srcColor.Rows)];
            srcColor.Clone().CopyTo(cmRt);
            cmRt = cm[new Rect(srcColor.Cols, 0, 150, srcColor.Rows)];
            for (int i = 0; i < colorMapIdx.Length; i++)
            {
                cmRt.Line(0, colorMapIdx[i], 50, colorMapIdx[i], Scalar.All(0));
                String valText = String.Format("{0}", i + 1);
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
                startX = (float)(startX+ (x1 - x0) * step[i]/100);
            }
            return pts;
        }
        public static Mat linspace(float x0, float x1, int n, double[] step)
        {
            return new Mat(n, 1, MatType.CV_32FC1, linspace0(x0, x1, n, step));
        }

        //private int _dr(Mat dst, int n)
        //{
        //    int cols = 100;
        //    //int height = 0;
        //    int i = 1;
        //    for (; i < colorMap.Length; i++)
        //    {
        //        Color clr0 = colorMap[i - 1];
        //        Color clr = colorMap[i];
        //        Mat r = linspace(clr0.R, clr.R, n);
        //        Mat g = linspace(clr0.G, clr.G, n);
        //        Mat b = linspace(clr0.B, clr.B, n);
        //        byte[,] rd = ConvertToBytes(r, cols);
        //        byte[,] gd = ConvertToBytes(g, cols);
        //        byte[,] bd = ConvertToBytes(b, cols);

        //        Mat m_r = new Mat(n, cols, MatType.CV_8UC1, rd);
        //        Mat m_g = new Mat(n, cols, MatType.CV_8UC1, gd);
        //        Mat m_b = new Mat(n, cols, MatType.CV_8UC1, bd);

        //        Mat m = new Mat();
        //        Cv2.Merge(new Mat[] { m_b, m_g, m_r }, m);
        //        Mat poi = dst[new Rect(0, (i - 1) * n, cols, n)];
        //        m.CopyTo(poi);
        //    }

        //    return i * n;
        //}

        public static Mat ReMapInversion(Mat src)
        {
            Mat map_x = new Mat(src.Size(), MatType.CV_32FC1);
            Mat map_y = new Mat(src.Size(), MatType.CV_32FC1);
            for (int i = 0; i < src.Rows; i++)
            {
                for (int j = 0; j < src.Cols; j++)
                {
                    map_x.Set<float>(i, j, (float)j);
                    map_y.Set<float>(i, j, (float)(src.Rows - 1 - i));
                }
            }

            Mat dst = new Mat();
            Cv2.Remap(src, dst, map_x, map_y, InterpolationFlags.Linear);

            return dst;
        }
        //public Mat DrawMap()
        //{
        //    int rows = 1024;
        //    int cols = srcColor.Cols;
        //    int n = colorMapIdx.Length;
        //    Mat cm = new Mat(rows, cols + 150, srcColor.Type(), Scalar.All(255));
        //    Mat cmRt = cm[new Rect(0, 0, cols, rows)];
        //    int height = _dr(cmRt, 1024 / n);
        //    //MatHelp.format(cmRt);
        //    ReMapInversion(cmRt).CopyTo(cmRt);
        //    Mat m = linspace(rows - height, height - 1, n);
        //    Mat mClr = new Mat();
        //    m.ConvertTo(mClr, MatType.CV_16UC1);
        //    cmRt = cm[new Rect(cols, rows - height, 150, height)];
        //    for (int i = 0; i < n; i++)
        //    {
        //        int y = mClr.Get<ushort>(i, 0);
        //        cmRt.Line(0, y, 50, y, Scalar.All(0));
        //        String valText = String.Format("{0}", n - i);
        //        //if (i == 0)
        //        // cmRt.PutText(valText, new OpenCvSharp.Point(60, y + 20), HersheyFonts.HersheyDuplex, 1, Scalar.All(0));
        //        //else
        //        cmRt.PutText(valText, new OpenCvSharp.Point(60, y), HersheyFonts.HersheyDuplex, 1, Scalar.All(0));

        //    }
        //    return cm;
        //}

        public void buildCustomMap(int n, double min, double max)//初始化colormap
        {
            this.minLut = min;
            this.maxLut = max;
            double[] stepColorPer = new double[n];
            for (int i = 0; i < n; i++)
            {
                stepColorPer[i] = (double)(100) / (n-1);
            }
            Mat m = linspace(0, 1535, n, stepColorPer);//这里是算每一种等级实际显示的颜色
            Mat mClr = new Mat();
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
                    stepPer[i] = stepPer[i]+startPiex;
                }
            }

        }
    }
}
