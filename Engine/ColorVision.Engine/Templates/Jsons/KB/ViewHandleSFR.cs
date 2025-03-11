#pragma warning disable CS8604,CS8602
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor.Draw;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class ViewHandleKB : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.KB , AlgorithmResultType.KB_Raw};

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            var ViewResults = result.ViewResults.ToSpecificViewResults<ViewResultKB>();

            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "pdfrequency", "pdomainSamplingData" };
            // 写入列头
            csvBuilder.AppendLine(string.Join(",", properties));
            // 写入数据行
            //foreach (var item in ViewResults)
            //{
            //    List<string> values = new()
            //    {
            //        item.pdfrequency.ToString(),
            //        item.pdomainSamplingData.ToString(),
            //    };

            //    csvBuilder.AppendLine(string.Join(",", values));
            //}

            File.WriteAllText(selectedPath, csvBuilder.ToString(), Encoding.UTF8);
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
                result.ViewResults = new ObservableCollection<IViewResult>();
            }


            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();
            List<string> header = new() { "pdfrequency", "pdomainSamplingData" };
            List<string> bdHeader = new() { "pdfrequency", "pdomainSamplingData" };


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
