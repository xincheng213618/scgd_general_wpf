#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services;
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


        public override void Load(ViewResultContext view, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmCaliAngleShift), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext view, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

        }
    }
}
