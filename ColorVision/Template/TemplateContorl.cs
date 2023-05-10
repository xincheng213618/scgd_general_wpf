using ColorVision.Config;
using ColorVision.MQTT;
using ColorVision.Util;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Template
{
    /// <summary>
    /// 模板管理
    /// </summary>

    public class TemplateControl
    {
        private static TemplateControl _instance;
        private static readonly object _locker = new();
        public static TemplateControl GetInstance() { lock (_locker) { return _instance ??= new TemplateControl(); } }

        private static string FileNameAoiParams = "cfg\\AOIParamSetup.cfg";
        private static string FileNameCalibrationParams = "cfg\\CalibrationSetup.cfg";

        private bool IsOldAoiParams;
        private bool IsOldCalibrationParams;

        /// <summary>
        /// 这里做个注释：需要兼容之前的代码写法，所以在中间层做了一个转换逻辑，让代码可以读之前的，也可以读现在的，读之前的也保存之前的
        /// </summary>
        public TemplateControl()
        {
            if (!Directory.Exists("cfg"))
                Directory.CreateDirectory("cfg");

            Dictionary<string, AoiParam> AoiParamsOld = CfgFile.LoadCfgFile<Dictionary<string, AoiParam>>(FileNameAoiParams) ?? new Dictionary<string, AoiParam>();

            if (AoiParamsOld.Count != 0)
            {
                IsOldAoiParams = true;
                AoiParams = new ObservableCollection<KeyValuePair<string, AoiParam>>();
                foreach (var item in AoiParamsOld)
                {
                    AoiParams.Add(item);
                }
            }
            else
            {
                AoiParams = CfgFile.LoadCfgFile<ObservableCollection<KeyValuePair<string, AoiParam>>>(FileNameAoiParams) ?? new ObservableCollection<KeyValuePair<string, AoiParam>>();
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
                    AoiParams.Add(new KeyValuePair<string, AoiParam>("default", param));
                }
            }

            Dictionary<string, CalibrationParam> CalibrationParamsOld = CfgFile.LoadCfgFile<Dictionary<string, CalibrationParam>>(FileNameCalibrationParams) ?? new Dictionary<string, CalibrationParam>();
            if (CalibrationParamsOld.Count != 0)
            {
                IsOldCalibrationParams = true;
                CalibrationParams = new ObservableCollection<KeyValuePair<string, CalibrationParam>>();
                foreach (var item in CalibrationParamsOld)
                {
                    CalibrationParams.Add(item);
                }
            }
            else
            {
                CalibrationParams = CfgFile.LoadCfgFile<ObservableCollection<KeyValuePair<string, CalibrationParam>>>(FileNameCalibrationParams) ?? new ObservableCollection<KeyValuePair<string, CalibrationParam>>();
                if (CalibrationParams.Count == 0)
                {
                    CalibrationParam param = new CalibrationParam();
                    CalibrationParams.Add(new KeyValuePair<string, CalibrationParam>("default", param));
                }
            }


            Application.Current.MainWindow.Closed += (s, e) =>
            {
                Save();
            };
        }

        public void Save()
        {
            if (!IsOldAoiParams)
                CfgFile.SaveCfgFile(FileNameAoiParams, AoiParams);
            else
                CfgFile.SaveCfgFile(FileNameAoiParams, ObservableCollectionToDictionary(AoiParams));

            if (!IsOldCalibrationParams)
                CfgFile.SaveCfgFile(FileNameCalibrationParams, CalibrationParams);
            else
                CfgFile.SaveCfgFile(FileNameCalibrationParams, ObservableCollectionToDictionary(CalibrationParams));
        }
        public void Save(WindowTemplateType windowTemplateType)
        {
            switch (windowTemplateType)
            {
                case WindowTemplateType.AoiParam:
                    if (!IsOldAoiParams)
                        CfgFile.SaveCfgFile(FileNameAoiParams, AoiParams);
                    else
                        CfgFile.SaveCfgFile(FileNameAoiParams, ObservableCollectionToDictionary(AoiParams));
                    break;
                case WindowTemplateType.Calibration:
                    if (!IsOldCalibrationParams)
                        CfgFile.SaveCfgFile(FileNameCalibrationParams, CalibrationParams);
                    else
                        CfgFile.SaveCfgFile(FileNameCalibrationParams, ObservableCollectionToDictionary(CalibrationParams));
                    break;
                default:
                    break;
            }
        }


        private static Dictionary<string,T> ObservableCollectionToDictionary<T>(ObservableCollection<KeyValuePair<string, T>> keyValues)
        {
            var keys = new Dictionary<string, T>() { };
            foreach (var key in keyValues)
            {
                keys.Add(key.Key, key.Value);
            }
            return keys;
        }

        public ObservableCollection<KeyValuePair<string,AoiParam>> AoiParams { get; set; }


        public ObservableCollection<KeyValuePair<string, CalibrationParam>> CalibrationParams { get; set; }




    }
}
