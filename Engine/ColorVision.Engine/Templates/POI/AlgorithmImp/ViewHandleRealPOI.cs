#pragma warning disable CA1725,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Results;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{

    public class ColorInformation
    {
        public double KeyLv { get; set; }
        public int HaloLv { get; set; }
        public CieColor KeyCIE { get; set; }
        public CieColor HaloCIE { get; set; }
    }

    public class CieColor
    {
        public double U { get; set; }
        public double V { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double CCT { get; set; }
        public double Wave { get; set; }
    }





    public class ViewHandleRealPOI : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleRealPOI));
        private readonly Func<int, IReadOnlyList<PoiPointResultModel>?> _readDetails;

        public ViewHandleRealPOI() : this(PoiHistoryDetails.Read) { }

        internal ViewHandleRealPOI(Func<int, IReadOnlyList<PoiPointResultModel>?> readDetails) => _readDetails = readDetails;

        public override List<ViewResultAlgType> CanHandle { get;  } = new List<ViewResultAlgType>() { ViewResultAlgType.RealPOI, ViewResultAlgType.POI_XYZ_V2, ViewResultAlgType.POI_Y_V2 , ViewResultAlgType.KB_Output_Lv, ViewResultAlgType.KB_Output_CIE };

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            IEnumerable<PoiPointResultModel> points = PoiHistoryDetails.RequireForExport(result).OfType<PoiPointResultModel>();
            string[] headers;
            IEnumerable<string?[]> rows;
            if (result.ResultType == ViewResultAlgType.KB_Output_CIE)
            {
                headers =
                [
                    "PoiName", "KeyLv", "HaloLv", "KeyCIE_U", "KeyCIE_V", "KeyCIE_X", "KeyCIE_Y", "KeyCIE_CCT", "KeyCIE_Wave",
                    "HaloCIE_U", "HaloCIE_V", "HaloCIE_X", "HaloCIE_Y", "HaloCIE_CCT", "HaloCIE_Wave",
                ];
                rows = points.Select(CreateColorRow);
            }
            else
            {
                headers = ["PoiName", "Value"];
                rows = points.Select(item => new[] { item.PoiName, item.Value });
            }

            AlgorithmSideDataFiles.WriteCsv(result, selectedPath, $"{result.Batch}.csv", headers, rows);
        }

        private static string?[] CreateColorRow(PoiPointResultModel item)
        {
            var colorInfo = JsonConvert.DeserializeObject<ColorInformation>(item.Value) ?? new ColorInformation();
            return
            [
                item.PoiName.ToString(),
                colorInfo.KeyLv.ToString(),
                colorInfo.HaloLv.ToString(),
                colorInfo.KeyCIE.U.ToString(),
                colorInfo.KeyCIE.V.ToString(),
                colorInfo.KeyCIE.X.ToString(),
                colorInfo.KeyCIE.Y.ToString(),
                colorInfo.KeyCIE.CCT.ToString(),
                colorInfo.KeyCIE.Wave.ToString(),
                colorInfo.HaloCIE.U.ToString(),
                colorInfo.HaloCIE.V.ToString(),
                colorInfo.HaloCIE.X.ToString(),
                colorInfo.HaloCIE.Y.ToString(),
                colorInfo.HaloCIE.CCT.ToString(),
                colorInfo.HaloCIE.Wave.ToString(),
            ];
        }
        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            PoiHistoryDetails.TryLoad(result, _readDetails);
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            OpenSourceImage(ctx, result);
            var details = PoiHistoryDetails.GetForDisplay(ctx, result);
            if (details == null) return;

            if (details.Count < 1000)
            {
                List<PoiPoint> poiPoints = new();
                foreach (var item in details)
                {
                    if (item is PoiPointResultModel poiPointResultModel)
                    {
                        poiPoints.Add(new PoiPoint(poiPointResultModel.PoiId, -1, poiPointResultModel.PoiName, poiPointResultModel.PoiType.ToPoiShape(), poiPointResultModel.PoiX ?? 0, poiPointResultModel.PoiY ?? 0, poiPointResultModel.PoiWidth ?? 0, poiPointResultModel.PoiHeight ?? 0));
                    }

                }
                PoiOverlayRenderer.AddRange(ctx.ImageView, poiPoints);
            }
            else
            {
                log.Info($"点阵信息太多{details.Count}");
            }


            List<string> header;
            List<string> bdHeader;
            header = new() { "PoiName", "Value" };
            bdHeader = new() { "PoiName", "Value" };

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = details;
            }
        }
    }
}
