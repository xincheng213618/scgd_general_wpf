using ColorVision.Common.MVVM;
using ColorVision.Database;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.FOV
{
    public class ViewHandleFOV : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.FOV };


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<AlgResultFOVModel> AlgResultFOVModels = AlgResultFOVDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultFOVModels)
                {
                    ViewResultFOV fOVResultData = new(item);
                    result.ViewResults.Add(fOVResultData);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Debug, Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmFOV), ImageFilePath = result.FilePath })) });
            }
        }


        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);

            List<string> header =  new() { "Pattern", "Type", "Degrees" };
            List<string> bdHeader = new() { "Pattern", "Type", "Degrees" };


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
