using ColorVision.Database;
using ColorVision.Database;
using ColorVision.Engine.Templates.POI.Dao;
using log4net;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ColorVision.Engine.Templates.POI
{
    public static class PoiParamExtension
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PoiParamExtension));

        public static int Save2DB(this PoiParam poiParam)
        {
            PoiMasterModel poiMasterModel = new(poiParam);
            int ret = PoiMasterDao.Instance.Save(poiMasterModel);
            if (ret == -1) return ret;

            List<PoiDetailModel> poiDetails = new List<PoiDetailModel>();
            foreach (PoiPoint pt in poiParam.PoiPoints)
            {
                poiDetails.Add(new PoiDetailModel(poiParam.Id, pt));
            }
            int count;

            var db = MySqlControl.GetInstance().DB;
            Stopwatch sw2 = Stopwatch.StartNew();
            db.Deleteable<PoiDetailModel>().Where(x => x.Pid == poiParam.Id).ExecuteCommand();
            count = MySqlControl.GetInstance().DB.Fastest<PoiDetailModel>().BulkCopy(poiDetails);
            sw2.Stop();
            log.Debug("SqlSugar BulkCopy " + count + " 耗时: " + sw2.ElapsedMilliseconds + " ms");

            //Stopwatch sw3 = Stopwatch.StartNew();
            //db.Deleteable<PoiDetailModel>().Where(x => x.Pid == poiParam.Id).ExecuteCommand();
            //count = MySqlControl.GetInstance().DB.Insertable(poiDetails).ExecuteCommand();
            //sw3.Stop();
            //log.Debug("SqlSugar Insertable " + count + " 耗时: " + sw3.ElapsedMilliseconds + " ms");

            return  1;
        }
    }

    /// <summary>
    /// 关注点模板
    /// </summary>
    public class PoiParam : ParamModBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PoiParam));
        public static void LoadPoiDetailFromDB(PoiParam poiParam)
        {
            poiParam.PoiPoints.Clear();
            log.Debug($"Start loading PoiDetail for pid={poiParam.Id}");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            List<PoiDetailModel> poiDetails2 = null;
            try
            {
                poiDetails2 = MySqlControl.GetInstance().DB
                    .Queryable<PoiDetailModel>()
                    .Where(x => x.Pid == poiParam.Id)
                    .ToList();
                log.Debug($"Query finished, count={poiDetails2.Count}");
            }
            catch (Exception ex)
            {
                log.Error("Error querying PoiDetailModel", ex);
                return;
            }

            try
            {
                foreach (var dbModel in poiDetails2)
                {
                    poiParam.PoiPoints.Add(new PoiPoint(dbModel));
                }
                log.Debug($"PoiPoints filled, count={poiParam.PoiPoints.Count}");
            }
            catch (Exception ex)
            {
                log.Error("Error filling PoiPoints", ex);
            }

            stopwatch.Stop();
            log.Debug($"LoadPoiDetailFromDB finished in {stopwatch.ElapsedMilliseconds} ms for pid={poiParam.Id}");
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

            LeftTopX = dbModel.LeftTopX;
            LeftTopY = dbModel.LeftTopY;
            RightTopX = dbModel.RightTopX;
            RightTopY = dbModel.RightTopY;
            RightBottomX = dbModel.RightBottomX;
            RightBottomY = dbModel.RightBottomY;
            LeftBottomX = dbModel.LeftBottomX;
            LeftBottomY = dbModel.LeftBottomY;
        }

        public int? LeftTopX { get => _LeftTopX; set { _LeftTopX = value; OnPropertyChanged(); } }
        private int? _LeftTopX;
        public int? LeftTopY { get => _LeftTopY; set { _LeftTopY = value; OnPropertyChanged(); } }
        private int? _LeftTopY;
        public int? RightTopX { get => _RightTopX; set { _RightTopX = value; OnPropertyChanged(); } }
        private int? _RightTopX;
        public int? RightTopY { get => _RightTopY; set { _RightTopY = value; OnPropertyChanged(); } }
        private int? _RightTopY;
        public int? RightBottomX { get => _RightBottomX; set { _RightBottomX = value; OnPropertyChanged(); } }
        private int? _RightBottomX;
        public int? RightBottomY { get => _RightBottomY; set { _RightBottomY = value; OnPropertyChanged(); } }
        private int? _RightBottomY;
        public int? LeftBottomX { get => _LeftBottomX; set { _LeftBottomX = value; OnPropertyChanged(); } }
        private int? _LeftBottomX;
        public int? LeftBottomY { get => _LeftBottomY; set { _LeftBottomY = value; OnPropertyChanged(); } }
        private int? _LeftBottomY;


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

        public int Type { get => _Type; set { _Type = value; OnPropertyChanged(); } }
        private int _Type;


        public int Width { get => _Width; set { _Width = value; OnPropertyChanged(); } }
        private int _Width;

        public int Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private int _Height;







        /// <summary>
        /// 关注点列表
        /// </summary>
        public ObservableCollection<PoiPoint> PoiPoints { get; set; } = new ObservableCollection<PoiPoint>();

        public PoiConfig PoiConfig { get; set; } = new PoiConfig();


    }

}
