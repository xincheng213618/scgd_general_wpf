#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.ImageCropping;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.Solution.Editor.AvalonEditor;
using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons.CompoundImg
{

    public class ViewHandleCompoundImg : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleCompoundImg));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.CompoundImg };


        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string filePath = selectedPath + "//" + result.Batch + result.ResultType + ".csv";
        }


        public override void Load(ViewResultContext view, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                var values = AlgResultImageDao.Instance.GetAllByPid(result.Id);
                result.ViewResults = new ObservableCollection<IViewResult>(values);

                if (values.Count == 1)
                {
                    AlgResultImageModel algResultImageModel = values[0];
                    if (File.Exists(algResultImageModel.FileName))
                        result.ContextMenu.Items.Add(new MenuItem() { Header = "打开输出文件位置", Command = new RelayCommand(a => { PlatformHelper.OpenFolderAndSelectFile(algResultImageModel.FileName); }) });
                }

                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmCompoundImg), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext view, ViewResultAlg result)
        {
            var values = result.ViewResults.ToSpecificViewResults<AlgResultImageModel>();
            if (values.Count == 1)
            {
                AlgResultImageModel algResultImageModel = values[0];
                if (File.Exists(algResultImageModel.FileName))
                    view.ImageView.OpenImage(algResultImageModel.FileName);
            }
            else
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
            }
        }
    }
}
