#pragma warning disable CS8602

using ColorVision.Engine.Media;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost
{
    public class ViewHandleGhost : IResultHandle
    {
        public List<AlgorithmResultType> CanHandle { get; set; } = new List<AlgorithmResultType>() { AlgorithmResultType.Ghost};
        /// <summary>
        /// 专门位鬼影设计的类
        /// </summary>
        sealed class Point1
        {
            public int X { get; set; }
            public int Y { get; set; }
        }
        public static void OpenGhostImage(ImageView ImageView,string? filePath, int[] LEDpixelX, int[] LEDPixelY, int[] GhostPixelX, int[] GhostPixelY)
        {
            if (filePath == null)
                return;
            int i = OpenCVHelper.ReadGhostImage(filePath, LEDpixelX.Length, LEDpixelX, LEDPixelY, GhostPixelX.Length, GhostPixelX, GhostPixelY, out HImage hImage);
            if (i != 0) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                ImageView.SetImageSource(hImage.ToWriteableBitmap());
                OpenCVHelper.FreeHImageData(hImage.pData);
                hImage.pData = IntPtr.Zero;
            });
        }

        public void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();
            if (result.ResultCode != 0)
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
                return;
            }


            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<AlgResultGhostModel> AlgResultGhostModels = AlgResultGhostDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultGhostModels)
                {
                    ViewResultGhost ghostResultData = new(item);
                    result.ViewResults.Add(ghostResultData);
                }
            }
            if (result.ViewResults.Count != 0 && result.ViewResults[0] is ViewResultGhost viewResultGhost)
            {
                try
                {
                    string GhostPixels = viewResultGhost.GhostPixels;
                    List<List<Point1>> GhostPixel = JsonConvert.DeserializeObject<List<List<Point1>>>(GhostPixels);
                    int[] Ghost_pixel_X;
                    int[] Ghost_pixel_Y;
                    List<Point1> Points = new();
                    foreach (var item in GhostPixel)
                        foreach (var item1 in item)
                            Points.Add(item1);

                    if (Points != null)
                    {
                        Ghost_pixel_X = new int[Points.Count];
                        Ghost_pixel_Y = new int[Points.Count];
                        for (int i = 0; i < Points.Count; i++)
                        {
                            Ghost_pixel_X[i] = (int)Points[i].X;
                            Ghost_pixel_Y[i] = (int)Points[i].Y;
                        }
                    }
                    else
                    {
                        Ghost_pixel_X = new int[1] { 1 };
                        Ghost_pixel_Y = new int[1] { 1 };
                    }

                    string LedPixels = viewResultGhost.LedPixels;
                    List<List<Point1>> LedPixel = JsonConvert.DeserializeObject<List<List<Point1>>>(LedPixels);
                    int[] LED_pixel_X;
                    int[] LED_pixel_Y;

                    Points.Clear();
                    foreach (var item in LedPixel)
                        foreach (var item1 in item)
                            Points.Add(item1);

                    if (Points != null)
                    {
                        LED_pixel_X = new int[Points.Count];
                        LED_pixel_Y = new int[Points.Count];
                        for (int i = 0; i < Points.Count; i++)
                        {
                            LED_pixel_X[i] = (int)Points[i].X;
                            LED_pixel_Y[i] = (int)Points[i].Y;
                        }
                    }
                    else
                    {
                        LED_pixel_X = new int[1] { 1 };
                        LED_pixel_Y = new int[1] { 1 };
                    }
                    OpenGhostImage(view.ImageView,result.FilePath, LED_pixel_X, LED_pixel_Y, Ghost_pixel_X, Ghost_pixel_Y);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

            }
            List<string> header = new() { "质心坐标", "光斑灰度", "鬼影灰度" };
            List<string> bdHeader = new() { "LedCenters", "LedBlobGray", "GhostAvrGray" };

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
