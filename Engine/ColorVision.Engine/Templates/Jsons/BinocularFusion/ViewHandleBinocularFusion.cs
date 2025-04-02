#pragma warning disable CS8602

using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.Ghost;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.Jsons.BinocularFusion
{
    public class ViewHandleBinocularFusion : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.ARVR_BinocularFusion};

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',' ) || field.Contains('"') || field.Contains('\n'))
            {
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }
            return field;
        }

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            var ViewResults = result.ViewResults.ToSpecificViewResults<BinocularFusionModel>();
            var csvBuilder = new StringBuilder();
            List<string> header = new() { "id", "中心点x", "中心点y", "x轴", "y轴", "z轴" };

            csvBuilder.AppendLine(string.Join(",", header));

            foreach (var item in ViewResults)
            {
                List<string> content = new List<string>();
                content.Add(EscapeCsvField(item.Id.ToString()));
                content.Add(EscapeCsvField(item.CrossMarkCenterX.ToString()));
                content.Add(EscapeCsvField(item.CrossMarkCenterY.ToString()));
                content.Add(EscapeCsvField(item.XDegree.ToString()));
                content.Add(EscapeCsvField(item.YDegree.ToString()));
                content.Add(EscapeCsvField(item.ZDegree.ToString()));

                csvBuilder.AppendLine(string.Join(",", content));
            }
            csvBuilder.AppendLine();
            csvBuilder.AppendLine();
            File.AppendAllText(selectedPath, csvBuilder.ToString(), Encoding.UTF8);

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
                List<BinocularFusionModel> AlgResultModels = BinocularFusionDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultModels)
                {
                    result.ViewResults.Add(item);
                }
            }

            List<string> header = new() { "中心点x", "中心点y", "x轴" , "y轴", "z轴" };
            List<string> bdHeader = new() { "CrossMarkCenterX", "CrossMarkCenterY", "XDegree" , "YDegree", "ZDegree" };

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
