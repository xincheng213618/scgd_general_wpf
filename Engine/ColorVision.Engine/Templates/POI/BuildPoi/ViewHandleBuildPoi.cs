﻿using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    public class ViewHandleBuildPoi : IResultHandle
    {
        public List<AlgorithmResultType> CanHandle { get; set; } = new List<AlgorithmResultType>() { AlgorithmResultType.BuildPOI};


        public void Handle(AlgorithmView view, AlgorithmResult result)
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
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultMTFModels)
                {
                    ViewResultBuildPoi mTFResultData = new(item);
                    result.ViewResults.Add(mTFResultData);
                }
            }

            List<POIPoint> DrawPoiPoint = new();
            foreach (var item in result.ViewResults)
            {
                if (item is PoiResultData poiResultData)
                    DrawPoiPoint.Add(poiResultData.Point);
            }
            view.AddPOIPoint(DrawPoiPoint);

            List<string> header;
            List<string> bdHeader;
            header = new() { "Name", "位置", "大小", "形状" };
            bdHeader = new() { "Name", "PixelPos", "PixelSize", "Shapes" };

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
