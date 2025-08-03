using ColorVision.Engine.MySql;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.Ghost2
{

    public class TemplateJsonGhost2 : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TemplateJsonGhost2));

        public GhostDetectionConfig KBJson
        {
            get
            {
                try
                {
                    GhostDetectionConfig kBJson = JsonConvert.DeserializeObject<GhostDetectionConfig>(JsonValue);
                    if (kBJson == null)
                    {
                        kBJson = new GhostDetectionConfig();
                        JsonValue = JsonConvert.SerializeObject(kBJson);
                        return kBJson;
                    }
                    return kBJson;
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    GhostDetectionConfig kBJson = new GhostDetectionConfig();
                    JsonValue = JsonConvert.SerializeObject(kBJson);
                    return kBJson;
                }
            }
            set
            {
                JsonValue = JsonConvert.SerializeObject(value);
                NotifyPropertyChanged();
            }
        }



        public TemplateJsonGhost2() : base()
        {
        }

        public TemplateJsonGhost2(TemplateJsonModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateGhostQK : ITemplateJson<TemplateJsonGhost2>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonGhost2>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonGhost2>>();

        public TemplateGhostQK()
        {
            Title = "Ghost2.0模板管理";
            Code = "ghost";
            Name = "ghost2.0";
            TemplateDicId = 38;
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateJson EditTemplateJson { get; set; }

        public override UserControl GetUserControl()
        {
            EditTemplateJson = new EditTemplateJson(Description);
            return EditTemplateJson;
        }
        public string Description { get; set; } = "{\r\n\"patternType\": 2,\t\t\t\t//图案类型，2代表点阵；4代表圆环\r\n  \"vOther\": {\r\n    \"showMinGain\": 60.0,\t\t//暗区增益，用于显示鬼影\r\n    \"showMaxGain\": 0.8,\t\t//亮区增益\r\n    \"Debug\": false,\t\t\t//是否debug\r\n    \"debugPath\": \"Result\\\\\",\t\t//debug存储路径\r\n    \"debugImgResize\": 2\t\t//debug图片压缩，2、3、4生效，否则不压缩\r\n  },\r\n  \"Bright\": {\r\n    \"thresholdMin\": 30000,\t\t\t//光点二值化 起始阈值\r\n    \"thresholdMax\": 40000,\t\t\t//光点二值化 终止阈值\r\n    \"thresholdStep\": 1000,\t\t\t//光点二值化 步进\r\n    \"brightNumX\": 3,\t\t\t\t//光点横向数量\r\n    \"brightNumY\": 3,\t\t\t\t//光点纵向数量\r\n    \"outRectSizeMin\": 60,\t\t\t//光点最小外接矩形的尺寸，以最短的为准\r\n    \"outRectSizeRate\": 5.0,\t\t\t//光点最小外接矩形的尺寸的浮动系数\t\r\n    \"erodeKernel\": 3\t\t\t\t//腐蚀的核，2-5之间，用于除底噪\r\n  },\r\n  \"Ghost\": {\r\n    \"ingoreCheckMixBright\": [\r\n//如果某个鬼影与光点重合，则用true，对应排序为从左到右，自上而下\r\n      false,\r\n      false,\r\n      false,\r\n      false,\r\n      true,\r\n      false,\r\n      false,\r\n      false,\r\n      false\r\n    ],\r\n    \"thresholdMin\": 120,\t\t//鬼影二值化 起始阈值\r\n    \"thresholdMax\": 500,\t\t//鬼影二值化 终止阈值\r\n    \"thresholdStep\": 10,\t\t//鬼影二值化 步进\r\n    \"outRectSizeMin\": 80,\t\t\t//鬼影最小外接矩形的尺寸，以最短的为准\r\n    \"outRectSizeRate\": 7.3,\t\t\t//鬼影最小外接矩形的尺寸的浮动系数\r\n    \"minGary\": -1,\t\t\t\t\t//暂不生效\r\n    \"garyRate\": 1.0,\t\t\t\t//暂不生效\r\n    \"erodeKernel\": 5,\t\t\t\t//腐蚀的核，2-5之间\r\n    \"erodeTime\": 5,\t\t\t\t//腐蚀的次数\r\n    \"dilateKernel\": 3,\t\t\t\t//膨胀的核，2-5之间\r\n    \"dilateTime\": 0,\t\t\t\t//膨胀的次数\r\n    \"distanceToBright\": 70\t\t\t//鬼影与光点的距离下限，若ingoreCheckMixBright为true，这里则不生效\r\n  },\r\n  \"Ring\": {\r\n//当图案为4（圆环）时的参数\r\n    \"thresholdValue\": 20000,\t\t//亮光圈二值化阈值\r\n    \"outRectSizeMin\": 400,\t\t\t\t//亮光圈最小外接矩尺寸\r\n    \"outRectSizeRate\": 10.0,\t\t\t\t//亮光圈最小外接矩尺寸浮动系数\r\n    \"erodeKernel\": 3,\t\t\t\t\t//底噪腐蚀\r\n    \"erodeTime\": 1,\t\t\t\t\t//底噪腐蚀次数\r\n    \"range\": 5,\t\t\t\t\t\t//取极值的宽度\r\n    \"peakDistance\": [\r\n      60,\t\t\t\t\t\t//一阶鬼影到光圈的距离像素\r\n      40\t\t\t\t\t\t//暂不生效\r\n    ]\r\n  },\r\n  \"MaskRect\": {\r\n    \"enable\": false,\t\t\t//是否取ROI\r\n    \"x\": 0,\t\t\t\t\t//Roi左上角坐标\r\n    \"y\": 0,\t\t\t\r\n    \"w\": 0,\t\t\t\t\t//Roi尺寸\r\n    \"h\": 0\r\n  }\r\n}\r\n";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlGhost2();

    }




}
