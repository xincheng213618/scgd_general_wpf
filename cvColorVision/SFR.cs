using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static cvColorVision.SimpleFeatures;

namespace cvColorVision
{
    public partial class cvCameraCSLib
    {
        public class FindRoi
        {   
            public int x { set; get; }
            public int y { set; get; }
            public int width { set; get; }
            public int height { set; get; }
        }


        public static CRECT FindRoi2CRECT(FindRoi fRoi)
        {
            CRECT rtROI = new();
            rtROI.x = fRoi.x;
            rtROI.y = fRoi.y;
            rtROI.cx = fRoi.width;
            rtROI.cy = fRoi.height;
            return rtROI;
        }


        public static bool SFR(HImage tImg, FindRoi fRoi)
        {
            string fovParamCfg = "cfg\\FovParamSetup.cfg";
            FOVParam pm = FOVParam.Load(fovParamCfg);
            double gamma = pm.SFR_gamma;
            if (fRoi == null)
                return false;

            float[] pdfrequency = new float[(int)Math.Max(fRoi.width, fRoi.height)];
            float[] pdomainSamplingData = new float[(int)Math.Max(fRoi.width, fRoi.height)];
            int nLen = (int)Math.Max(tImg.nWidth, tImg.nHeight);

            if (SFRCalculation(tImg, FindRoi2CRECT(fRoi), gamma, pdfrequency, pdomainSamplingData, nLen) < 1)
                return false;

            saveCsv_SFR("Result", pdfrequency, pdomainSamplingData);
            return true;
        }
        private static void saveCsv_SFR(string path, float[] pdfrequency, float[] pdomainSamplingData)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path += $"{(path.Substring(path.Length - 1, 1) != "/" ? "\\" : "")}SFRResult_{DateTime.Now:yyyyMMddhhmmss}.csv";
            if (!File.Exists(path))
                CSVinitialized(path, new List<string>() { "pdfrequency", "pdomainSamplingData" });


            using FileStream fs = new(path, FileMode.Append, FileAccess.Write);
            using StreamWriter sw = new(fs, Encoding.UTF8);
            for (int i = 0; i < pdfrequency.Length; i++)
                sw.WriteLine($"{pdfrequency[i]},{pdomainSamplingData[i]}");
        }


    }
}
