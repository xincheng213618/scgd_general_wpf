#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services;
using log4net;
using NPOI.SS.Formula.Functions;
using SqlSugar;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.Jsons.CaliAngleShift
{

    public class ViewHandleCaliAngleShift : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleCaliAngleShift));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.CaliAngleShift };
        

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string filePath = selectedPath + "//" + result.Batch + result.ResultType + ".csv";

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine($"name,x,y,w,h,value");
            File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
        }


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();

                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var detailImageEntities = db.Queryable<DetailImageEntity>().Where(x => x.PId == result.Id).ToList();
                

                foreach (var detailImage in detailImageEntities)
                {
                    result.ViewResults.Add(detailImage);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmCaliAngleShift), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            var detailImageEntities = result.ViewResults.OfType<DetailImageEntity>();

            if (detailImageEntities.Count() == 1)
            {
                var detailImage = detailImageEntities.First();
                ctx.ImageView.OpenImage(detailImage.FileName);
            }
            else
            {

                if (File.Exists(result.FilePath))
                    ctx.ImageView.OpenImage(result.FilePath);
            }

            List<string> header = new() { "FileName", "FileInfo", "OrderIndex" };
            List<string> bdHeader = new() { "FileName", "FileInfo", "OrderIndex"};

            ctx.LeftGridViewColumnVisibilitys.Clear();
            if (ctx.ListView.View is GridView gridView)
            {
                gridView.Columns.Clear();

                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = result.ViewResults;
            }
        }
    }
}
