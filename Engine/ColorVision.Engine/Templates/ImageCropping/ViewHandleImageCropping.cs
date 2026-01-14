using ColorVision.Common.MVVM;
using ColorVision.Database;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.ImageCropping
{
    public class ViewHandleImageCropping : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.Image_Cropping };

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            var ViewResults = result.ViewResults.ToSpecificViewResults<AlgResultImageModel>();

            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "Id", "file_name", "order_index" , "FileInfo" };
            // 写入列头
            csvBuilder.AppendLine(string.Join(",", properties));
            // 写入数据行
            foreach (var item in ViewResults)
            {
                List<string> values = new()
                {
                    item.Id.ToString(),
                    item.FileName ?? string.Empty,
                    item.OrderIndex.ToString() ?? string.Empty,
                    item.FileInfo ?? string.Empty
                };

                csvBuilder.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(selectedPath, csvBuilder.ToString(), Encoding.UTF8);

            string saveng = System.IO.Path.Combine(selectedPath, $"{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.png");
            AlgorithmView.ImageView.Save(saveng);
        }

        public ViewResultContext AlgorithmView { get; set; }


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>(AlgResultImageDao.Instance.GetAllByPid(result.Id));
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmImageCropping), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            AlgorithmView = ctx;

            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);


            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();
            List<string> header = new() { "file_name", "order_index", "FileInfo" };
            List<string> bdHeader = new() { "FileName", "OrderIndex", "FileInfo" };

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = result.ViewResults;
            }
        }

    }

}
