﻿using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Templates.POI.Dao;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.POI
{
    public static class PoiParamExtension
    {
        public static void LoadPoiDetailFromDB(this PoiParam poiParam) => PoiParam.LoadPoiDetailFromDB(poiParam);
        public static int Save2DB(this PoiParam poiParam) => PoiParam.Save2DB(poiParam);
    }

    /// <summary>
    /// 关注点模板
    /// </summary>
    public class PoiParam : ParamModBase
    {
        public static int Save2DB(PoiParam poiParam)
        {
            PoiMasterModel poiMasterModel = new(poiParam);
            int ret = PoiMasterDao.Instance.Save(poiMasterModel);
            if (ret == -1) return ret;

            List<PoiDetailModel> poiDetails = new List<PoiDetailModel>();
            foreach (PoiPoint pt in poiParam.PoiPoints)
            {
                poiDetails.Add(new PoiDetailModel(poiParam.Id, pt));
            }
            return PoiDetailDao.Instance.SaveByPid(poiParam.Id, poiDetails);
        }
          

        public static void LoadPoiDetailFromDB(PoiParam poiParam)
        {
            poiParam.PoiPoints.Clear();
            List<PoiDetailModel> poiDetails = PoiDetailDao.Instance.GetAllByPid(poiParam.Id);
            foreach (var dbModel in poiDetails)
            {
                poiParam.PoiPoints.Add(new PoiPoint(dbModel));
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
            CfgJson = dbModel.CfgJson ?? string.Empty;
            PoiConfig.IsPoiCIEFile = dbModel.IsDynamics ??false;
        }

        public string CfgJson
        {
            get => JsonConvert.SerializeObject(PoiConfig);
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    PoiConfig ??= new PoiConfig();
                }
                else
                {
                    try
                    {
                        PoiConfig = JsonConvert.DeserializeObject<PoiConfig>(value) ?? new PoiConfig();
                    }
                    catch
                    {
                        PoiConfig = new PoiConfig();
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


        /// <summary>
        /// 关注点列表
        /// </summary>
        public ObservableCollection<PoiPoint> PoiPoints { get; set; } = new ObservableCollection<PoiPoint>();

        public PoiConfig PoiConfig { get; set; } = new PoiConfig();


    }

}
