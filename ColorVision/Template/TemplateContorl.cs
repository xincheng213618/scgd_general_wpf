using ColorVision.Config;
using ColorVision.MQTT;
using ColorVision.Util;
using cvColorVision;
using ScottPlot.Styles;
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
        private static string FileNamePGParams = "cfg\\PGParamSetup.cfg";

        private bool IsOldAoiParams;
        private bool IsOldCalibrationParams;
        private bool IsOldPGParams;


        public TemplateControl()
        {
            if (!Directory.Exists("cfg"))
                Directory.CreateDirectory("cfg");

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
            AoiParams = IDefault(FileNameAoiParams, param, ref IsOldAoiParams);
            CalibrationParams = IDefault(FileNameCalibrationParams, new CalibrationParam(),ref IsOldCalibrationParams);
            PGParams = IDefault(FileNamePGParams, new PGParam(), ref IsOldPGParams);

            Application.Current.MainWindow.Closed += (s, e) =>
            {
                Save();
            };
        }
        /// 这里是初始化模板的封装，因为模板的代码高度统一，所以使用泛型T来设置具体的模板参数。
        /// 又因为需要兼容之前的代码写法，所以在中间层做了一个转换逻辑，让代码可以读之前的，也可以读现在的，读之前的也保存之前的 <summary>
        /// 这里是初始化模板的封装，因为模板的代码高度统一，所以使用泛型T来设置具体的模板参数。
        /// 最后在给模板的每一个元素加上一个切换的效果，即当某一个模板启用时，关闭其他已经启用的模板；
        /// 同一类型，只能存在一个启用的模板
        private static ObservableCollection<KeyValuePair<string, T>> IDefault<T>(string FileName ,T Default , ref bool IsOldParams) where T : ParamBase
        {
            ObservableCollection<KeyValuePair<string, T>> Params = new ObservableCollection<KeyValuePair<string, T>>();

            Dictionary<string, T> ParamsOld = CfgFile.LoadCfgFile<Dictionary<string, T>>(FileName) ?? new Dictionary<string, T>();
            if (ParamsOld.Count != 0)
            {
                IsOldParams = true;
                Params = new ObservableCollection<KeyValuePair<string, T>>();
                foreach (var item in ParamsOld)
                {
                    Params.Add(item);
                }
            }
            else
            {
                Params = CfgFile.LoadCfgFile<ObservableCollection<KeyValuePair<string, T>>>(FileName) ?? new ObservableCollection<KeyValuePair<string, T>>();
                if (Params.Count == 0)
                {
                    Params.Add(new KeyValuePair<string, T>("default", Default));
                }
            }
            foreach (var item in Params)
            {
                item.Value.IsEnabledChanged += (s, e) =>
                {
                    foreach (var item2 in Params)
                    {
                        if (item2.Key != item.Key)
                            item2.Value.IsEnable = false;
                    }
                };
            }
            Params.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    Params[e.NewStartingIndex].Value.IsEnabledChanged += (s, e1) =>
                    {
                        foreach (var item2 in Params)
                        {
                            if (item2.Key != Params[e.NewStartingIndex].Key)
                                item2.Value.IsEnable = false;
                        }
                    };
                    
                }
            };
            return Params;
        }



        public void Save()
        {
            SaveDefault(FileNameAoiParams, AoiParams,IsOldAoiParams);
            SaveDefault(FileNameCalibrationParams, CalibrationParams, IsOldCalibrationParams);
            SaveDefault(FileNamePGParams, PGParams, IsOldPGParams);
        }


        public void Save(WindowTemplateType windowTemplateType)
        {
            switch (windowTemplateType)
            {
                case WindowTemplateType.AoiParam:
                    SaveDefault(FileNameAoiParams, AoiParams, IsOldAoiParams);
                    break;
                case WindowTemplateType.Calibration:
                    SaveDefault(FileNameCalibrationParams, CalibrationParams, IsOldCalibrationParams);
                    break;
                case WindowTemplateType.PGParam:
                    SaveDefault(FileNamePGParams, PGParams, IsOldPGParams);
                    break;
                default:
                    break;
            }
        }


        private static void SaveDefault<T>(string FileNameParams, ObservableCollection<KeyValuePair<string, T>> t, bool IsOldParams)
        {
            if (IsOldParams)
                CfgFile.SaveCfgFile(FileNameParams, ObservableCollectionToDictionary(t));
            else
                CfgFile.SaveCfgFile(FileNameParams, t);

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

        public ObservableCollection<KeyValuePair<string, AoiParam>> AoiParams { get; set; }

        public ObservableCollection<KeyValuePair<string, CalibrationParam>> CalibrationParams { get; set; } 
        public ObservableCollection<KeyValuePair<string, PGParam>> PGParams { get; set; }




    }
}
