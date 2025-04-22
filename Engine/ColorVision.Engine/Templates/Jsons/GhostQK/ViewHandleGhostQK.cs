#pragma warning disable CS8602

using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using log4net;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.Jsons.GhostQK
{

    public class ViewHandleGhostQK : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleGhostQK));

        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.GhostQK};

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
            var blackMuraViews = result.ViewResults.ToSpecificViewResults<GhostView>();
            var csvBuilder = new StringBuilder();

            List<string> header = new List<string>();
            var properties = typeof(GhostView).GetProperties();

            // 递归构建头部
            foreach (var prop in properties)
            {
                var columnName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                {
                    var nestedProperties = prop.PropertyType.GetProperties();
                    foreach (var nestedProp in nestedProperties)
                    {
                        var nestedColumnName = $"{nestedProp.Name}";
                        header.Add(nestedColumnName);
                    }
                }
                else
                {
                    header.Add(columnName);
                }
            }

            string filePath = selectedPath + "//" + result.ResultType + ".csv";

            // 检查文件是否存在
            if (File.Exists(filePath))
            {
                // 读取文件末尾的几行，检查是否包含头信息
                var lines = File.ReadLines(filePath).ToList();
                if (!lines.Any(line => line == string.Join(",", header)))
                {
                    // 如果文件存在但不含头信息，则追加头信息
                    File.AppendAllText(filePath, "\n" + string.Join(",", header) + "\n", Encoding.UTF8);
                }
            }
            else
            {
                // 文件不存在，创建新文件并写入头信息
                File.WriteAllText(filePath, string.Join(",", header) + "\n", Encoding.UTF8);
            }

            // 追加内容
            foreach (var item in blackMuraViews)
            {
                List<string> content = new List<string>();
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(item);
                    if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                    {
                        var nestedProperties = prop.PropertyType.GetProperties();
                        foreach (var nestedProp in nestedProperties)
                        {
                            var nestedValue = nestedProp.GetValue(value);
                            content.Add(EscapeCsvField(nestedValue?.ToString() ?? string.Empty));
                        }
                    }
                    else
                    {
                        content.Add(EscapeCsvField(value?.ToString() ?? string.Empty));
                    }
                }
                csvBuilder.AppendLine(string.Join(",", content));
            }

            // 追加内容到文件
            File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);

        }


        public override void Load(AlgorithmView view, AlgorithmResult result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<DetailCommonModel> AlgResultModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultModels)
                {
                    GhostView blackMuraView = new GhostView(item);
                    result.ViewResults.Add(blackMuraView);
                }
            }
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

            void OpenSource()
            {
                view.ImageView.ImageShow.Clear();
                foreach (var item in result.ViewResults)
                {
                    if (item is GhostView blackMuraModel)
                    {
                        if (File.Exists(result.FilePath))
                            view.ImageView.OpenImage(result.FilePath);
                        log.Info(result.FilePath);
                    }
                }
            }

            Load(view, result);
            OpenSource();

            List<string> header = new() { "LvAvg", "LvMax", "LvMin", "Uniformity(%)", "ZaRelMax", "AreaJsonVal" };
            List<string> bdHeader = new() { "ResultJson.LvAvg", "ResultJson.LvMax", "ResultJson.LvMin", "ResultJson.Uniformity", "ResultJson.ZaRelMax", "AreaJsonVal" };

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
