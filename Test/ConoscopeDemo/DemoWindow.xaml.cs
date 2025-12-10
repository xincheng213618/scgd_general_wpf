using ColorVision.FileIO;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ConoscopeDemo
{
    /// <summary>
    /// DemoWindow.xaml 的交互逻辑
    /// Demo窗口：打开CVCIE文件，提取Y通道，应用Jet伪彩色，绘制120度直径线，导出CSV
    /// </summary>
    public partial class DemoWindow : System.Windows.Window
    {
        private Mat? yChannelMat;
        private Mat? pseudoColorMat;
        private List<System.Windows.Point> diameterLinePoints;
        private List<(double angle, List<System.Windows.Point> points)> circlePoints;
        private System.Windows.Point imageCenter;
        private int imageRadius;

        public DemoWindow()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            InitializeComponent();
            diameterLinePoints = new List<System.Windows.Point>();
            circlePoints = new List<(double, List<System.Windows.Point>)>();
        }

        /// <summary>
        /// 打开文件按钮点击事件
        /// </summary>
        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CVCIE Files (*.cvcie)|*.cvcie|All Files (*.*)|*.*",
                Title = "选择CVCIE文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ProcessCVCIEFile(openFileDialog.FileName);
            }
        }

        /// <summary>
        /// 处理CVCIE文件
        /// </summary>
        private void ProcessCVCIEFile(string filePath)
        {
            try
            {

                // 读取CVCIE文件
                if (!CVFileUtil.Read(filePath, out CVCIEFile fileInfo))
                {
                    MessageBox.Show("无法读取CVCIE文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 提取Y通道
                Mat yChannel = ExtractYChannel(fileInfo);
                if (yChannel == null || yChannel.Empty())
                {
                    MessageBox.Show("无法提取Y通道", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                yChannelMat = yChannel;

                // 应用Jet伪彩色
                pseudoColorMat = ApplyJetColormap(yChannel);

                // 显示图像
                DisplayPseudoColorImage(pseudoColorMat);

                // 获取图像中心和半径
                imageCenter = new System.Windows.Point(yChannel.Width / 2.0, yChannel.Height / 2.0);
                imageRadius = Math.Min(yChannel.Width, yChannel.Height) / 2;

                // 绘制120度直径线
                DrawDiameterLine(120);

                // 绘制ScottPlot图表
                PlotDiameterLineChart();

                // 启用导出按钮
                btnExportDiameter.IsEnabled = true;
                btnExportCircle.IsEnabled = true;

                fileInfo.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"处理文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 提取Y通道
        /// </summary>
        private Mat ExtractYChannel(CVCIEFile fileInfo)
        {
            if (fileInfo.Channels < 3)
            {
                // 如果只有一个通道，假设它是Y通道
                if (fileInfo.Channels == 1)
                {
                    return CreateMatFromData(fileInfo.Data, fileInfo.Rows, fileInfo.Cols, fileInfo.Bpp);
                }
                return null;
            }

            // 计算单个通道的大小
            int channelSize = fileInfo.Rows * fileInfo.Cols * fileInfo.Bpp / 8;

            // 提取Y通道 (第二个通道，索引为1)
            byte[] yData = new byte[channelSize];
            Buffer.BlockCopy(fileInfo.Data, channelSize, yData, 0, channelSize);

            return CreateMatFromData(yData, fileInfo.Rows, fileInfo.Cols, fileInfo.Bpp);
        }

        /// <summary>
        /// 从字节数组创建Mat
        /// </summary>
        private Mat CreateMatFromData(byte[] data, int rows, int cols, int bpp)
        {
            MatType matType;
            switch (bpp)
            {
                case 8:
                    matType = MatType.CV_8UC1;
                    break;
                case 16:
                    matType = MatType.CV_16UC1;
                    break;
                case 32:
                    matType = MatType.CV_32FC1;
                    break;
                case 64:
                    matType = MatType.CV_64FC1;
                    break;
                default:
                    throw new NotSupportedException($"不支持的位深度: {bpp}");
            }

            return Mat.FromPixelData(rows, cols, matType, data);
        }

        /// <summary>
        /// 应用Jet伪彩色映射
        /// </summary>
        private Mat ApplyJetColormap(Mat inputMat)
        {
            Mat normalizedMat = new Mat();
            Mat colorMat = new Mat();

            // 归一化到0-255范围
            Cv2.Normalize(inputMat, normalizedMat, 0, 255, NormTypes.MinMax);

            // 转换为8位
            Mat mat8U = new Mat();
            normalizedMat.ConvertTo(mat8U, MatType.CV_8UC1);

            // 应用Jet伪彩色映射
            Cv2.ApplyColorMap(mat8U, colorMat, ColormapTypes.Jet);

            normalizedMat.Dispose();
            mat8U.Dispose();

            return colorMat;
        }

        /// <summary>
        /// 显示伪彩色图像
        /// </summary>
        private void DisplayPseudoColorImage(Mat colorMat)
        {
            // 转换为WriteableBitmap显示
            WriteableBitmap writeableBitmap = colorMat.ToWriteableBitmap();
            imgDisplay.Source = writeableBitmap;
        }

        /// <summary>
        /// 绘制直径线（指定角度）
        /// </summary>
        private void DrawDiameterLine(double angleDegrees)
        {
            if (yChannelMat == null || yChannelMat.Empty())
                return;

            diameterLinePoints.Clear();

            // 将角度转换为弧度
            double angleRadians = angleDegrees * Math.PI / 180.0;

            // 计算直径线的起点和终点
            double x1 = imageCenter.X - imageRadius * Math.Cos(angleRadians);
            double y1 = imageCenter.Y - imageRadius * Math.Sin(angleRadians);
            double x2 = imageCenter.X + imageRadius * Math.Cos(angleRadians);
            double y2 = imageCenter.Y + imageRadius * Math.Sin(angleRadians);

            // 沿直径线采样像素值
            int numSamples = imageRadius * 2;
            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)(numSamples - 1);
                double x = x1 + t * (x2 - x1);
                double y = y1 + t * (y2 - y1);

                if (x >= 0 && x < yChannelMat.Width && y >= 0 && y < yChannelMat.Height)
                {
                    diameterLinePoints.Add(new System.Windows.Point(x, y));
                }
            }
        }

        /// <summary>
        /// 使用ScottPlot绘制直径线图表
        /// </summary>
        private void PlotDiameterLineChart()
        {
            if (diameterLinePoints.Count == 0 || yChannelMat == null)
                return;

            plotChart.Plot.Clear();

            // 获取沿直径线的像素值
            double[] distances = new double[diameterLinePoints.Count];
            double[] values = new double[diameterLinePoints.Count];

            for (int i = 0; i < diameterLinePoints.Count; i++)
            {
                var point = diameterLinePoints[i];
                int x = (int)Math.Round(point.X);
                int y = (int)Math.Round(point.Y);

                // 读取像素值
                double pixelValue = GetPixelValue(yChannelMat, x, y);

                distances[i] = i;
                values[i] = pixelValue;
            }

            // 绘制线图
            var scatter = plotChart.Plot.Add.Scatter(distances, values);
            scatter.LineWidth = 2;
            scatter.Color = ScottPlot.Color.FromHex("#1f77b4");

            plotChart.Plot.Title("120度直径线像素值分布");
            plotChart.Plot.XLabel("距离 (像素)");
            plotChart.Plot.YLabel("像素值");
            plotChart.Plot.Axes.AutoScale();

            plotChart.Refresh();
        }

        /// <summary>
        /// 获取Mat中指定位置的像素值
        /// </summary>
        private double GetPixelValue(Mat mat, int x, int y)
        {
            if (x < 0 || x >= mat.Width || y < 0 || y >= mat.Height)
                return 0;

            MatType matType = mat.Type();
            
            if (matType == MatType.CV_8UC1)
                return mat.At<byte>(y, x);
            else if (matType == MatType.CV_16UC1)
                return mat.At<ushort>(y, x);
            else if (matType == MatType.CV_32FC1)
                return mat.At<float>(y, x);
            else if (matType == MatType.CV_64FC1)
                return mat.At<double>(y, x);
            else
                return 0;
        }

        /// <summary>
        /// 导出直径线CSV
        /// </summary>
        private void BtnExportDiameter_Click(object sender, RoutedEventArgs e)
        {
            if (diameterLinePoints.Count == 0 || yChannelMat == null)
            {
                MessageBox.Show("没有可导出的直径线数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = "diameter_line_120deg.csv",
                Title = "保存直径线数据"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    StringBuilder csv = new StringBuilder();
                    csv.AppendLine("Index,X,Y,PixelValue");

                    for (int i = 0; i < diameterLinePoints.Count; i++)
                    {
                        var point = diameterLinePoints[i];
                        int x = (int)Math.Round(point.X);
                        int y = (int)Math.Round(point.Y);
                        double pixelValue = GetPixelValue(yChannelMat, x, y);

                        csv.AppendLine($"{i},{point.X:F2},{point.Y:F2},{pixelValue:F4}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, csv.ToString(), Encoding.UTF8);
                    MessageBox.Show("直径线数据导出成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 导出R圆CSV
        /// </summary>
        private void BtnExportCircle_Click(object sender, RoutedEventArgs e)
        {
            if (yChannelMat == null || yChannelMat.Empty())
            {
                MessageBox.Show("没有可导出的图像数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = "r_circle.csv",
                Title = "保存R圆数据"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 计算多个半径的圆数据
                    GenerateCircleData();

                    StringBuilder csv = new StringBuilder();
                    csv.AppendLine("Radius,Angle,X,Y,PixelValue");

                    foreach (var (angle, points) in circlePoints)
                    {
                        foreach (var point in points)
                        {
                            int x = (int)Math.Round(point.X);
                            int y = (int)Math.Round(point.Y);
                            double pixelValue = GetPixelValue(yChannelMat, x, y);
                            double radius = Math.Sqrt(Math.Pow(point.X - imageCenter.X, 2) + Math.Pow(point.Y - imageCenter.Y, 2));

                            csv.AppendLine($"{radius:F2},{angle:F2},{point.X:F2},{point.Y:F2},{pixelValue:F4}");
                        }
                    }

                    File.WriteAllText(saveFileDialog.FileName, csv.ToString(), Encoding.UTF8);
                    MessageBox.Show("R圆数据导出成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 生成圆数据（采样不同半径和角度）
        /// </summary>
        private void GenerateCircleData()
        {
            circlePoints.Clear();

            // 采样5个不同的半径
            double[] radiusRatios = { 0.2, 0.4, 0.6, 0.8, 1.0 };

            foreach (double ratio in radiusRatios)
            {
                double radius = imageRadius * ratio;

                // 对于每个半径，采样360度
                List<System.Windows.Point> points = new List<System.Windows.Point>();

                for (int angle = 0; angle < 360; angle += 5) // 每5度采样一次
                {
                    double angleRadians = angle * Math.PI / 180.0;
                    double x = imageCenter.X + radius * Math.Cos(angleRadians);
                    double y = imageCenter.Y + radius * Math.Sin(angleRadians);

                    if (x >= 0 && x < yChannelMat.Width && y >= 0 && y < yChannelMat.Height)
                    {
                        points.Add(new System.Windows.Point(x, y));
                    }
                }

                if (points.Count > 0)
                {
                    circlePoints.Add((radius, points));
                }
            }
        }

        /// <summary>
        /// 窗口关闭时释放资源
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            yChannelMat?.Dispose();
            pseudoColorMat?.Dispose();
        }
    }
}
