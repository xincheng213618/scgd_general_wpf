using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Services.Templates.POI;
using ColorVision.Engine.Templates.POI.Comply;
using ColorVision.Engine.Templates.POI.Dao;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates.POI
{
    public class ExportPOI : ExportTemplateBase
    {
        public override string GuidId => "PoiParam";
        public override string Header => Properties.Resources.MenuPoi;
        public override int Order => 1;
        public override ITemplate Template { get; } = new TemplatePOI();
    }

    public class TemplatePOI : ITemplate<PoiParam>, IITemplateLoad
    {
        public TemplatePOI()
        {
            IsSideHide = true;
            Title = "关注点设置";
            Code = ModMasterType.POI;
            TemplateParams = PoiParam.Params;
        }
        public override void PreviewMouseDoubleClick(int index)
        {
            var WindowFocusPoint = new WindowFocusPoint(PoiParam.Params[index].Value) { Owner = Application.Current.GetActiveWindow() };
            WindowFocusPoint.ShowDialog();
        }

        public override void Load()
        {
            var backup = PoiParam.Params.ToDictionary(tp => tp.Id, tp => tp);
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                List<PoiMasterModel> poiMasters = PoiMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "tenant_id", UserConfig.Instance.TenantId }, { "is_delete", 0 } });
                foreach (var dbModel in poiMasters)
                {
                    var poiparam = new PoiParam(dbModel);
                    if (backup.TryGetValue(poiparam.Id, out var model))
                    {
                        model.Value = poiparam;
                        model.Key = poiparam.Name;
                    }
                    else
                    {
                        PoiParam.Params.Add(new TemplateModel<PoiParam>(dbModel.Name ?? "default", poiparam));
                    }
                }
            }
            SaveIndex.Clear();
        }

        public override void Save()
        {
            if (SaveIndex.Count == 0) return;
            foreach (var index in SaveIndex)
            {
                var item = TemplateParams[index];
                PoiMasterDao.Instance.Save(new PoiMasterModel(item.Value));
            }
            SaveIndex.Clear();
        }
        public override void Delete(int index)
        {
            PoiMasterDao poiMasterDao = new();
            poiMasterDao.DeleteById(TemplateParams[index].Value.Id);
            TemplateParams.RemoveAt(index);
        }



        public override void Create(string templateName)
        {
            PoiParam? AddPoiParam(string templateName)
            {
                if(ExportTemp != null)
                {
                    ExportTemp.Name = templateName;
                    PoiMasterModel poiMasterModel = new(ExportTemp);
                    PoiMasterDao.Instance.Save(poiMasterModel);
                    ExportTemp.Id = poiMasterModel.Id;
                    List<PoiDetailModel> poiDetails = new();
                    foreach (PoiPoint pt in ExportTemp.PoiPoints)
                    {
                        PoiDetailModel poiDetail = new PoiDetailModel(ExportTemp.Id, pt);
                        poiDetails.Add(poiDetail);
                    }
                    PoiDetailDao.Instance.SaveByPid(ExportTemp.Id, poiDetails);

                    return ExportTemp;
                }
                else
                {
                    PoiMasterModel poiMasterModel = new PoiMasterModel(templateName, UserConfig.Instance.TenantId);
                    PoiMasterDao.Instance.Save(poiMasterModel);

                    int pkId = poiMasterModel.Id;
                    if (pkId > 0)
                    {
                        PoiMasterModel model = PoiMasterDao.Instance.GetById(pkId);
                        if (model != null) return new PoiParam(model);
                        else return null;
                    }
                    return null;
                }


            }


            PoiParam? param = AddPoiParam(templateName);
            if (param != null)
            {
                var a = new TemplateModel<PoiParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(PoiParam)}模板失败", "ColorVision");
            }
        }


        public override void Export(int index)
        {
            PoiParam.LoadPoiDetailFromDB(TemplateParams[index].Value);
            base.Export(index);
        }

        public override bool Import()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.cfg|*.cfg";
            ofd.Title = "导入模板";
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;
            //if (TemplateParams.Any(a => a.Key.Equals(System.IO.Path.GetFileNameWithoutExtension(ofd.FileName), StringComparison.OrdinalIgnoreCase)))
            //{
            //    MessageBox.Show(Application.Current.GetActiveWindow(), "模板名称已存在", "ColorVision");
            //    return false;
            //}
            byte[] fileBytes = File.ReadAllBytes(ofd.FileName);
            string fileContent = System.Text.Encoding.UTF8.GetString(fileBytes);
            try
            {
                ExportTemp = JsonConvert.DeserializeObject<PoiParam>(fileContent);
                if (ExportTemp !=null)
                {
                    ExportTemp.Id = -1;
                    foreach (var item in ExportTemp.PoiPoints)
                    {
                        item.Id = -1;
                    }
                }
                return true;
            }
            catch (JsonException ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"解析模板文件时出错: {ex.Message}", "ColorVision");
                return false;
            }
        }

    }

    /// <summary>
    /// 关注点模板
    /// </summary>
    public class PoiParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<PoiParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiParam>>();

        public static void Save2DB(PoiParam poiParam)
        {
            PoiMasterModel poiMasterModel = new(poiParam);
            PoiMasterDao.Instance.Save(poiMasterModel);

            List<PoiDetailModel> poiDetails = new();
            foreach (PoiPoint pt in poiParam.PoiPoints)
            {
                PoiDetailModel poiDetail = new PoiDetailModel(poiParam.Id, pt);
                poiDetails.Add(poiDetail);
            }
            PoiDetailDao.Instance.SaveByPid(poiParam.Id, poiDetails);
        }


        public static void LoadPoiDetailFromDB(PoiParam poiParam)
        {
            poiParam.PoiPoints.Clear();
            List<PoiDetailModel> poiDetails = PoiDetailDao.Instance.GetAllByPid(poiParam.Id);
            foreach (var dbModel in poiDetails)
            {
                poiParam.PoiPoints.AddUnique(new PoiPoint(dbModel));
            }
        }


        public PoiParam()
        {

        }

        public PoiParam(PoiMasterModel dbModel)
        {
            Id = dbModel.Id;

            Name = dbModel.Name ?? string.Empty;
            Width = dbModel.Width ?? 0;
            Height = dbModel.Height ?? 0;
            Type = dbModel.Type ?? 0;
            DatumArea.X1X = dbModel.LeftTopX ?? 0;
            DatumArea.X1Y = dbModel.LeftTopY ?? 0;
            DatumArea.X2X = dbModel.RightTopX ?? 0;
            DatumArea.X2Y = dbModel.RightTopY ?? 0;
            DatumArea.X3X = dbModel.RightBottomX ?? 0;
            DatumArea.X3Y = dbModel.RightBottomY ?? 0;
            DatumArea.X4X = dbModel.LeftBottomX ?? 0;
            DatumArea.X4Y = dbModel.LeftBottomY ?? 0;
            DatumArea.CenterX = (DatumArea.X2X - DatumArea.X1X) / 2;
            DatumArea.CenterY = (DatumArea.X4Y - DatumArea.X1Y) / 2;
            CfgJson = dbModel.CfgJson ?? string.Empty;
            ValidateId = dbModel.ValidateId ?? -1;
        }

        public string CfgJson
        {
            get => JsonConvert.SerializeObject(DatumArea);
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    DatumArea ??= new DatumArea();
                }
                else
                {
                    try
                    {
                        DatumArea = JsonConvert.DeserializeObject<DatumArea>(value) ?? new DatumArea();
                    }
                    catch
                    {
                        DatumArea = new DatumArea();
                    }
                }
            }
        }

        public int Type { get => _Type; set { _Type = value; NotifyPropertyChanged(); } }
        private int _Type;


        public int Width { get => _Width; set { _Width = value; NotifyPropertyChanged(); } }
        private int _Width;

        public int Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); } }
        private int _Height;
        public int ValidateId { get => _ValidateId; set { _ValidateId = value; NotifyPropertyChanged(); } }
        private int _ValidateId;

        public RelayCommand ValidateCIEAVGCommand => new RelayCommand(a =>
        {
            var Template = new TemplateComplyParam("Comply.CIE.AVG");
            new WindowTemplate(Template, Template.FindIndex(ValidateId)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });

        /// <summary>
        /// 关注点列表
        /// </summary>
        public ObservableCollection<PoiPoint> PoiPoints { get; set; } = new ObservableCollection<PoiPoint>();

        public DatumArea DatumArea { get; set; } = new DatumArea();

        [JsonIgnore]
        public bool IsPointCircle { get => DefaultPointType == RiPointTypes.Circle; set { if (value) DefaultPointType = RiPointTypes.Circle; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointRect { get => DefaultPointType == RiPointTypes.Rect; set { if (value) DefaultPointType = RiPointTypes.Rect; NotifyPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointMask { get => DefaultPointType == RiPointTypes.Mask; set { if (value) DefaultPointType = RiPointTypes.Rect; NotifyPropertyChanged(); } }
        public RiPointTypes DefaultPointType { set; get; }

    }

}
