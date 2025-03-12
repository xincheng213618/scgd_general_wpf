#pragma warning disable CS8604,CS8602
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor.Draw;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;
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
            if (!File.Exists(result.ResultImagFile)) return;
            try
            {
                // 获取文件名
                string fileName = Path.GetFileName(result.ResultImagFile);

                // 组合目标路径
                string destinationFilePath = Path.Combine(selectedPath, fileName);

                // 复制文件
                File.Copy(result.ResultImagFile, destinationFilePath, true);

            }
            catch (Exception ex)
            {

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

            if (File.Exists(result.ResultImagFile))
            {
                view.ImageView.OpenImage(result.ResultImagFile);
            }
            else
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
            }


            view.listViewSide.Visibility = Visibility.Collapsed;

        }
    }

}
