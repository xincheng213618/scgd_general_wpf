using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.Net;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.ToolPlugins
{
    [DisplayName("绘制")]
    public class LedToolConfig : ViewModelBase, IConfig
    {

        public static LedToolConfig Instance => ConfigService.Instance.GetRequiredService<LedToolConfig>();

        [DisplayName("灯珠数据文件"), PropertyEditorType(PropertyEditorType.TextSelectFile)]
        public string SelectedPath { get => _SelectedPath; set { _SelectedPath = value; NotifyPropertyChanged(); } }
        private string _SelectedPath;

        [DisplayName("图像文件"), PropertyEditorType(PropertyEditorType.TextSelectFile)]
        public string SelectedPath1 { get => _SelectedPath1; set { _SelectedPath1 = value; NotifyPropertyChanged(); } }
        private string _SelectedPath1;

        [DisplayName("半径"), PropertyEditorType(PropertyEditorType.TextSelectFile)]
        public int Radius { get => _Radius; set { _Radius = value; NotifyPropertyChanged(); } }
        private int _Radius = 4;

        [DisplayName("宽度"), PropertyEditorType(PropertyEditorType.TextSelectFile)]
        public int Thickness { get => _Thickness; set { _Thickness = value; NotifyPropertyChanged(); } }
        private int _Thickness = 1;

    }

    public class LedTool : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;

        public override string Header => nameof(LedTool);
        public override int Order => 100;

        public List<List<Point>> Points { get; set; } = new List<List<Point>>();
        public (int pointIndex, int listIndex) FindNearbyPoints(int mousex, int mousey)
        {
            for (int listIndex = 0; listIndex < Points.Count; listIndex++)
            {
                var pointList = Points[listIndex];
                for (int pointIndex = 0; pointIndex < pointList.Count; pointIndex++)
                {
                    var point = pointList[pointIndex];
                    double deltaX = point.X - mousex;
                    double deltaY = point.Y - mousey;

                    if (Math.Abs(deltaX) > 5 || Math.Abs(deltaY) > 5)
                        continue;

                    double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    if (distance < 5)
                    {
                        return (pointIndex, listIndex);
                    }
                }
            }
            return (-1, -1); // Return a more appropriate value when no point is found
        }

        public override void Execute()
        {
            Points = new List<List<Point>>();
            new PropertyEditorWindow(LedToolConfig.Instance).ShowDialog();

            if (!File.Exists(LedToolConfig.Instance.SelectedPath))
            {
                MessageBox.Show("找不到灯珠数据文件");
                return;
            }

            if (!File.Exists(LedToolConfig.Instance.SelectedPath1))
            {
                MessageBox.Show("找不到图像文件");
                return;
            }
            int count = 0;
            try
            {
                if (File.Exists(LedToolConfig.Instance.SelectedPath))
                {
                    string[] lines = File.ReadAllLines(LedToolConfig.Instance.SelectedPath);
                    string[] dates = lines[0].Split(',');
                    int rows = int.Parse(dates[0]);
                    int cols = int.Parse(dates[1]);

                    for (int lineIndex = 2; lineIndex < lines.Length; lineIndex++)
                    {
                        string[] xy = lines[lineIndex].Split(',');
                        List<Point> points = new List<Point>();
                        for (int i = 0; i < xy.Length; i += 4)
                        {
                            if (double.TryParse(xy[i], out double x) && double.TryParse(xy[i + 1], out double y))
                            {
                                points.Add(new Point(x, y));
                                count++;
                            }
                        }
                        Points.Add(points);
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            if (!CVFileUtil.IsCIEFile(LedToolConfig.Instance.SelectedPath1))
            {
                MessageBox.Show("图像文件不是CVCIE");
                return;
            }

            HImage? hImage = new NetFileUtil().OpenLocalCVFile(LedToolConfig.Instance.SelectedPath1).ToWriteableBitmap()?.ToHImage();

            if (hImage == null)
            {
                MessageBox.Show("图像文件打开失败");
                return;
            }
            int z = 0;
            int[] ints = new int[count * 2];
            for (int i = 0; i < Points.Count; i++)
            {
                for (int j = 0; j < Points[i].Count; j++)
                {
                    ints[2 * z] = (int)Points[i][j].X;
                    ints[2 * z + 1] = (int)Points[i][j].Y;
                    z += 1;
                }
            }


            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.Filter = "PNG|*.png";
            saveFileDialog.FileName = "test.png";
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                int ret = OpenCVMediaHelper.M_DrawPoiImage((HImage)hImage, out HImage hImageProcessed, LedToolConfig.Instance.Radius, ints, ints.Length, LedToolConfig.Instance.Thickness);
                hImageProcessed.ToWriteableBitmap().SaveImageSourceToFile(saveFileDialog.FileName);
            }


        }
    }
}

