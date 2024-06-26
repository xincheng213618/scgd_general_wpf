﻿using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates.POI.Dao;
using ColorVision.Engine.Templates.POI.Validate;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Templates.POI;
using ColorVision.UI.Sorts;
using ColorVision.UserSpace;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            PoiParam? param = PoiParam.AddPoiParam(templateName);
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


        public static PoiParam? AddPoiParam(string TemplateName)
        {
            PoiMasterModel poiMasterModel = new PoiMasterModel(TemplateName, UserConfig.Instance.TenantId);
            PoiMasterDao.Instance.Save(poiMasterModel);

            int pkId = poiMasterModel.Id;
            if (pkId > 0)
            {
                PoiMasterModel Service = PoiMasterDao.Instance.GetById(pkId);
                if (Service != null) return new PoiParam(Service);
                else return null;
            }
            return null;
        }

        public PoiParam()
        {
            Id = No++;
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
            var Template = new TemplateValidateCIEAVGParam();
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
