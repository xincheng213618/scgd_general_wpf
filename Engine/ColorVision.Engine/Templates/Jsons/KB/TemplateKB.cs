using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates.POI;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.KB
{

    public class TemplateJsonKBParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TemplateJsonKBParam));
        public RelayCommand InportFormPoiCommand { get; set; }
        public RelayCommand OpenTemplatePoiCommand { get; set; }


        public TemplateJsonKBParam() : base()
        {
            InportFormPoiCommand = new RelayCommand(a => InportFormPoi());
            OpenTemplatePoiCommand = new RelayCommand(a => OpenTemplatePoi());
        }

        public TemplateJsonKBParam(TemplateJsonModel templateJsonModel):base(templateJsonModel) 
        {
            InportFormPoiCommand = new RelayCommand(a => InportFormPoi());
            OpenTemplatePoiCommand = new RelayCommand(a => OpenTemplatePoi());
        }

        public static ObservableCollection<TemplateModel<PoiParam>> PoiParams => TemplatePoi.Params;

        public int TemplatePoiSelectedIndex { get => _TemplatePoiSelectedIndex; set { _TemplatePoiSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplatePoiSelectedIndex;

        public void OpenTemplatePoi()
        {
            new TemplateEditorWindow(new TemplatePoi(), _TemplatePoiSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }

        public void InportFormPoi()
        {
            if (EditPoiParamConfig.Instance.PoiPointParamType != PoiPointParamType.KBParam)
            {
                MessageBox.Show("启用此功能请先设置PoiPointParamType 为KBParam");
                return;
            }

            if (_TemplatePoiSelectedIndex > -1)
            {
                PoiParam poiParam = PoiParams[_TemplatePoiSelectedIndex].Value;
                poiParam.LoadPoiDetailFromDB();

                KBJson kBJson = new KBJson();
                foreach (var item in poiParam.PoiPoints)
                {
                    if (item.PointType == RiPointTypes.Rect)
                    {
                        KBKeyRect kBKeyRect = new KBKeyRect();

                        KBHalo kBHalo = new KBHalo();
                        kBHalo.HaloScale = item.Param.HaloScale;
                        kBHalo.OffsetX = item.Param.HaloOffsetX;
                        kBHalo.OffsetY = item.Param.HaloOffsetY;
                        kBHalo.HaloSize = item.Param.HaloSize;
                        kBHalo.ThresholdV = item.Param.HaloThreadV;
                        kBHalo.Move = item.Param.HaloOutMOVE; ;

                        kBKeyRect.KBHalo = kBHalo;

                        KBKey kBKey = new KBKey();
                        kBKey.KeyScale = item.Param.KeyScale;
                        kBKey.OffsetX = item.Param.KeyOffsetX;
                        kBKey.OffsetY = item.Param.KeyOffsetY;
                        kBKey.ThresholdV = item.Param.KeyThreadV;
                        kBKey.Move = item.Param.KeyOutMOVE;
                        kBKeyRect.KBKey = kBKey;

                        kBKeyRect.Height = (int)item.PixHeight;
                        kBKeyRect.Width = (int)item.PixWidth;
                        kBKeyRect.X = (int)(item.PixX - item.PixWidth / 2);
                        kBKeyRect.Y = (int)(item.PixY - item.PixHeight / 2);
                        kBKeyRect.Name = item.Name;

                        kBJson.KBKeyRects.Add(kBKeyRect);
                    }
                    else
                    {
                        log.Info($"只支持矩形导入{JsonConvert.SerializeObject(item)}");  
                    }

                }


                JsonValue = JsonConvert.SerializeObject(kBJson);
            }


        }


    }



    public class TemplateKB : ITemplateJson<TemplateJsonKBParam>,IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonKBParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonKBParam>>();

        public TemplateKB()
        {
            Title = "KB算法设置";
            Code = "KB";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditKBTemplateJson EditTemplateJson { get; set; } = new EditKBTemplateJson();

        public override UserControl GetUserControl()
        {
            return EditTemplateJson;
        }
        public override UserControl CreateUserControl() => new EditKBTemplateJson();

    }




}
