using ColorVision.Config;
using ColorVision.MQTT;
using ColorVision.Util;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Template
{
    public class TemplateControl
    {
        private static TemplateControl _instance;
        private static readonly object _locker = new();
        public static TemplateControl GetInstance() { lock (_locker) { return _instance ??= new TemplateControl(); } }



        private static string FileNameAoiParams = "cfg\\AOIParamSetup.cfg";
        private static string FileNameCalibrationParams = "cfg\\CalibrationSetup.cfg";

        public TemplateControl()
        {
            if (!Directory.Exists("cfg"))
                Directory.CreateDirectory("cfg");

            AoiParams = CfgFile.LoadCfgFile<Dictionary<string, AoiParam>>("cfg\\AOIParamSetup.cfg")?? new Dictionary<string, AoiParam>();
            if (AoiParams.Count == 0)
            {
                AoiParam param = new AoiParam
                {
                    filter_by_area = true,
                    max_area = 6000,
                    min_area = 10,
                    filter_by_contrast = true,
                    max_contrast = 1.7f,
                    min_contrast = 0.3f,
                    contrast_brightness = 1.0f,
                    contrast_darkness = 0.5f,
                    blur_size = 19,
                    min_contour_size = 5,
                    erode_size = 5,
                    dilate_size = 5,
                    left = 5,
                    right = 5,
                    top = 5,
                    bottom = 5
                };
                AoiParams.Add("default", param);
            }

            CalibrationParams = CfgFile.LoadCfgFile<Dictionary<string, CalibrationParam>>("cfg\\CalibrationSetup.cfg")?? new Dictionary<string, CalibrationParam>();
            if (CalibrationParams.Count == 0)
            {
                CalibrationParam param = new CalibrationParam();
                CalibrationParams.Add("default", param);
            }
            Application.Current.MainWindow.Closed += (s, e) =>
            {
                Save();
            };
        }

        public void Save()
        {
            CfgFile.SaveCfgFile(FileNameAoiParams, AoiParams);
            CfgFile.SaveCfgFile(FileNameCalibrationParams, CalibrationParams);
        }
        public void Save(WindowTemplateType windowTemplateType)
        {
            switch (windowTemplateType)
            {
                case WindowTemplateType.AoiParam:
                    CfgFile.SaveCfgFile(FileNameAoiParams, AoiParams);
                    break;
                case WindowTemplateType.Calibration:
                    CfgFile.SaveCfgFile(FileNameCalibrationParams, CalibrationParams);
                    break;
                default:
                    break;
            }
        }




        public Dictionary<string,AoiParam> AoiParams { get; set; }


        public Dictionary<string, CalibrationParam> CalibrationParams { get; set; }




    }
}
