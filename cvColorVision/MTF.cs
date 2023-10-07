#pragma warning disable  CA2101,CA1707,CA1401,CA1051,CA1838,CA1711,CS0649,CA2211,CA1708,CA1720

using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;
using static cvColorVision.cvCameraCSLib;
using static cvColorVision.SimpleFeatures;

namespace cvColorVision
{
    public partial class cvCameraCSLib
    {
        public class RoiData
        {
            public int Img_x { set; get; }
            public int Img_y { set; get; }
            public int w { set; get; }
            public int h { set; get; }
        }


        public static HImage? ToHImage(string FileName)
        {
            try
            {
                Mat mat = Cv2.ImRead(FileName);
                HImage hImage = new HImage();
                hImage.pData = mat.Data;
                hImage.nHeight = (uint)mat.Width;
                hImage.nHeight = (uint)mat.Height;
                hImage.nChannels = (uint)mat.Channels();
                hImage.nBpp = (uint)mat.ElemSize();
                return hImage;
            }
            catch
            {
                return null;
            }

        }

        public static bool MTF(List<RoiData> roiDatas, HImage tImg)
        {
            string  fovParamCfg = "cfg\\FovParamSetup.cfg";
            FOVParam pm = FOVParam.Load(fovParamCfg);

            double dRatio = pm.MTF_dRatio;

            Dictionary<RoiData, double> MTFResults = new Dictionary<RoiData, double>();
            foreach (RoiData pd in roiDatas)
            {
                var articulation = cvCalArticulation(EvaFunc.CalResol, tImg, 0, 1, 5, dRatio);

                if (articulation < 0)
                {
                    return false;
                }
                else 
                {
                    MTFResults.Add(pd, articulation);
                }
            }
            MTF_saveToCsv("Result", MTFResults);
            return true;
        }


        public static bool CSVinitialized(string fileName,List<string> strings)
        {
            if (File.Exists(fileName))
                return false;

            System.Data.DataTable dt = new System.Data.DataTable();
            foreach (var item in strings)
                dt.Columns.Add(item);

            using FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            using StreamWriter sw2 = new StreamWriter(fs, Encoding.UTF8);
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                if (i != 0) sw2.Write(",");
                sw2.Write(dt.Columns[i].ColumnName);
            }
            sw2.WriteLine("");
            sw2.Flush();

            return true;
        }


        public static void MTF_saveToCsv(string path, Dictionary<RoiData, double> MTFResults)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path += $"{(path.Substring(path.Length - 1, 1) != "/" ? "\\" : "")}MTFResult_{DateTime.Now:yyyyMMddhhmmss}.csv";
            if (!File.Exists(path))
                CSVinitialized(path, new List<string>() { "X", "Y", "Width", "Height", "Value" });
            using FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write);
            using StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
            foreach (var item in MTFResults)
            {
                string str = $"{item.Key.Img_x},{item.Key.Img_y},{item.Key.w},{item.Key.h},{item.Value}";
                sw.Write(str);
                sw.WriteLine();
            }
            sw.Flush();
        }


    }
}
