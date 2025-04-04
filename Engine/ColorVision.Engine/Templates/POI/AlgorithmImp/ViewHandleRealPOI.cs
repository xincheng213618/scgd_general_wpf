﻿using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class ViewHandleRealPOI : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get;  } = new List<AlgorithmResultType>() { AlgorithmResultType.POI_XYZ_V2, AlgorithmResultType.POI_Y_V2 , AlgorithmResultType.KB_Output_Lv, AlgorithmResultType.KB_Output_CIE };

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "PoiName","Value" };

            // 写入列头
            csvBuilder.AppendLine(string.Join(",", properties));

            // 写入数据行
            foreach (var item in result.ViewResults.OfType<PoiPointResultModel>())
            {
                List<string> values = new()
                {
                    item.PoiName,
                    item.Value
                };

                csvBuilder.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(selectedPath +"//" + result.Batch + ".csv", csvBuilder.ToString(), Encoding.UTF8);


        }

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();
            if (result.ResultCode != 0)
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
                return;
            }

            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>(PoiPointResultDao.Instance.GetAllByPid(result.Id));
            }

            List<POIPoint> DrawPoiPoint = new();
            foreach (var item in result.ViewResults)
            {
                if (item is PoiPointResultModel poiPointResultModel)
                {
                    POIPoint pOIPoint = new POIPoint(poiPointResultModel.PoiId ?? -1, -1, poiPointResultModel.PoiName, poiPointResultModel.PoiType, (int)poiPointResultModel.PoiX, (int)poiPointResultModel.PoiY, poiPointResultModel.PoiWidth ?? 0, poiPointResultModel.PoiHeight ?? 0);
                    DrawPoiPoint.Add(pOIPoint);
                }

            }
            view.AddPOIPoint(DrawPoiPoint);

            List<string> header;
            List<string> bdHeader;
            header = new() { "PoiName", "Value" };
            bdHeader = new() { "PoiName", "Value" };

            if (view.listViewSide.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.listViewSide.ItemsSource = result.ViewResults;
            }
        }
    }
}
