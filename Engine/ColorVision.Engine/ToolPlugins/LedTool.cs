using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor;
using ColorVision.Net;
using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.UI.PropertyEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.ToolPlugins
{
    [DisplayName("绘制")]
    public class LedToolConfig : ViewModelBase, IConfig
    {
        
        public static LedToolConfig Instance => ConfigService.Instance.GetRequiredService<LedToolConfig>();

        [DisplayName("灯珠数据文件"),PropertyEditorType(PropertyEditorType.TextSelectFile)]
        public string SelectedPath { get => _SelectedPath; set { _SelectedPath = value; NotifyPropertyChanged(); } }
        private string _SelectedPath;

        [DisplayName("图像文件"), PropertyEditorType(PropertyEditorType.TextSelectFile)]
        public string SelectedPath1 { get => _SelectedPath1; set { _SelectedPath1 = value; NotifyPropertyChanged(); } }
        private string _SelectedPath1;

    }


    public class LedTool : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;

        public override string GuidId => nameof(LedTool);

        public override string Header => nameof(LedTool);


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


        }

    }
}

