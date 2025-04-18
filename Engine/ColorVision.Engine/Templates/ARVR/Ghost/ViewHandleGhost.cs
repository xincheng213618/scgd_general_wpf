#pragma warning disable CS8602,CS8604

using ColorVision.Engine.Interfaces;
using ColorVision.Engine.Media;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor;
using ColorVision.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.Ghost
{
    public class  ViewHandleGhost : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.Ghost};

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            var ViewResults = result.ViewResults.ToSpecificViewResults<AlgResultGhostModel>();
            var csvBuilder = new StringBuilder();
            List<string> headers = new List<string>();
            headers.Add("id");
            headers.Add("质心坐标");
            headers.Add("光斑灰度");
            headers.Add("鬼影灰度");
            csvBuilder.AppendLine(string.Join(",", headers));

            foreach (var item in ViewResults)
            {
                List<string> content = new List<string>();
                content.Add(EscapeCsvField(item.Id.ToString()));
                content.Add(EscapeCsvField(item.LEDCenters));
                content.Add(EscapeCsvField(item.LEDBlobGray));
                content.Add(EscapeCsvField(item.GhostAverageGray));
                csvBuilder.AppendLine(string.Join(",", content));
            }
            csvBuilder.AppendLine();
            csvBuilder.AppendLine();
            File.AppendAllText(selectedPath, csvBuilder.ToString(), Encoding.UTF8);
        }

        public static void OpenGhostImage(ImageView ImageView,string? filePath, int[] LEDpixelX, int[] LEDPixelY, int[] GhostPixelX, int[] GhostPixelY)
        {
            if (filePath == null)
                return;
            if (CVFileUtil.IsCIEFile(filePath))
            {
                HImage hImage1 = new NetFileUtil().OpenLocalCVFile(filePath).ToWriteableBitmap().ToHImage();

                int i = OpenCVHelper.GhostImage(hImage1, out HImage hImage, LEDpixelX.Length, LEDpixelX, LEDPixelY, GhostPixelX.Length, GhostPixelX, GhostPixelY);
                if (i != 0) return;
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    ImageView.SetImageSource(hImage.ToWriteableBitmap());
                    OpenCVHelper.FreeHImageData(hImage.pData);
                    hImage1.Dispose();
                    hImage.pData = IntPtr.Zero;
                    ImageView.UpdateZoomAndScale();
                });
            }
            else
            {
                int i = OpenCVHelper.ReadGhostImage(filePath, LEDpixelX.Length, LEDpixelX, LEDPixelY, GhostPixelX.Length, GhostPixelX, GhostPixelY, out HImage hImage);
                if (i != 0) return;
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    ImageView.SetImageSource(hImage.ToWriteableBitmap());
                    OpenCVHelper.FreeHImageData(hImage.pData);
                    hImage.pData = IntPtr.Zero;
                    ImageView.UpdateZoomAndScale();
                });
            }
        }


        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',' ) || field.Contains('"') || field.Contains('\n'))
            {
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }
            return field;
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

            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<AlgResultGhostModel> AlgResultGhostModels = AlgResultGhostDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultGhostModels)
                {
                    result.ViewResults.Add(item);
                }
            }
            if (result.ViewResults.Count != 0 && result.ViewResults[0] is AlgResultGhostModel viewResultGhost)
            {
                try
                {
                    int[] Ghost_pixel_X;
                    int[] Ghost_pixel_Y;
                    List<Point1> Points = new();
                    if (viewResultGhost.GhostPixel !=null)
                        foreach (var item in viewResultGhost.GhostPixel)
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

                    int[] LED_pixel_X;
                    int[] LED_pixel_Y;

                    Points.Clear();
                    if (viewResultGhost.LedPixel !=null)
                        foreach (var item in viewResultGhost.LedPixel)
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
            List<string> bdHeader = new() { "LEDCenters", "LEDBlobGray", "GhostAverageGray" };

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
