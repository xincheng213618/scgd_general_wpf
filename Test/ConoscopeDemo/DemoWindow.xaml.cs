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
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ConoscopeDemo
{
    /// <summary>
    /// DemoWindow.xaml 的交互逻辑
    /// Demo窗口：打开CVCIE文件，提取通道，应用Jet伪彩色，绘制可选角度的直径线，导出CSV
    /// </summary>
    public partial class DemoWindow : System.Windows.Window
    {
        private Mat? xChannelMat;
        private Mat? yChannelMat;
        private Mat? zChannelMat;
        private Mat? pseudoColorMat;
        
        private System.Windows.Point imageCenter;
        private int imageRadius;
        private double maxAngle = 60; // Default max angle
        private double conoscopeCoefficient = 1.0; // Pixels per degree

        private int displayAngle = 120; // Default display angle
        private ExportChannel displayChannel = ExportChannel.Y; // Default display channel

        public DemoWindow()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            InitializeComponent();
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
                if (!CVFileUtil.ReadCVCIE(filePath, out CVCIEFile fileInfo))
                {
                    MessageBox.Show("无法读取CVCIE文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 提取XYZ通道
                ExtractChannels(fileInfo);

                if (yChannelMat == null || yChannelMat.Empty())
                {
                    MessageBox.Show("无法提取通道数据", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 获取图像中心和半径
                imageCenter = new System.Windows.Point(yChannelMat.Width / 2.0, yChannelMat.Height / 2.0);
                imageRadius = Math.Min(yChannelMat.Width, yChannelMat.Height) / 2;
                
                // 计算conoscopeCoefficient
                conoscopeCoefficient = imageRadius / maxAngle;

                // 更新显示
                UpdateDisplay();

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
        /// 提取XYZ通道
        /// </summary>
        private void ExtractChannels(CVCIEFile fileInfo)
        {
            if (fileInfo.Channels < 3)
            {
                // 如果只有一个通道，假设它是Y通道
                if (fileInfo.Channels == 1)
                {
                    yChannelMat = CreateMatFromData(fileInfo.Data, fileInfo.Rows, fileInfo.Cols, fileInfo.Bpp);
                }
                return;
            }

            // 计算单个通道的大小
            int channelSize = fileInfo.Rows * fileInfo.Cols * fileInfo.Bpp / 8;

            // 提取X, Y, Z通道
            byte[] xData = new byte[channelSize];
            byte[] yData = new byte[channelSize];
            byte[] zData = new byte[channelSize];

            Buffer.BlockCopy(fileInfo.Data, 0, xData, 0, channelSize);
            Buffer.BlockCopy(fileInfo.Data, channelSize, yData, 0, channelSize);
            Buffer.BlockCopy(fileInfo.Data, channelSize * 2, zData, 0, channelSize);

            xChannelMat = CreateMatFromData(xData, fileInfo.Rows, fileInfo.Cols, fileInfo.Bpp);
            yChannelMat = CreateMatFromData(yData, fileInfo.Rows, fileInfo.Cols, fileInfo.Bpp);
            zChannelMat = CreateMatFromData(zData, fileInfo.Rows, fileInfo.Cols, fileInfo.Bpp);
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
        /// 更新显示
        /// </summary>
        private void UpdateDisplay()
        {
            // Get the selected channel
            Mat? selectedMat = GetSelectedChannelMat(displayChannel);
            if (selectedMat == null || selectedMat.Empty())
                return;

            // 应用Jet伪彩色
            pseudoColorMat?.Dispose();
            pseudoColorMat = ApplyJetColormap(selectedMat);

            // 显示图像
            DisplayPseudoColorImage(pseudoColorMat);

            // 更新标题
            tbImageTitle.Text = $"{displayChannel}通道伪彩色图像 (Jet)";

            // 绘制并显示直径线图表
            PlotDiameterLineChart();

            // 更新图表标题
            tbChartTitle.Text = $"直径线分析 ({displayAngle}° - {displayChannel}通道)";
        }

        /// <summary>
        /// 获取选中的通道Mat
        /// </summary>
        private Mat? GetSelectedChannelMat(ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => xChannelMat,
                ExportChannel.Y => yChannelMat,
                ExportChannel.Z => zChannelMat,
                _ => yChannelMat
            };
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
        /// 使用ScottPlot绘制直径线图表
        /// </summary>
        private void PlotDiameterLineChart()
        {
            Mat? selectedMat = GetSelectedChannelMat(displayChannel);
            if (selectedMat == null || selectedMat.Empty())
                return;

            // Create diameter line data for the selected angle
            var diameterLine = CreateDiameterLine(displayAngle, selectedMat);

            plotChart.Plot.Clear();

            if (diameterLine.RgbData.Count == 0)
                return;

            // Get values for the selected channel
            double[] positions = diameterLine.RgbData.Select(s => s.Position).ToArray();
            double[] values = diameterLine.RgbData.Select(s => GetChannelValue(s, displayChannel)).ToArray();

            // 绘制线图
            var scatter = plotChart.Plot.Add.Scatter(positions, values);
            scatter.LineWidth = 2;
            scatter.Color = ScottPlot.Color.FromHex("#1f77b4");

            plotChart.Plot.Title($"{displayAngle}度直径线 - {displayChannel}通道");
            plotChart.Plot.XLabel("位置 (角度)");
            plotChart.Plot.YLabel("像素值");
            plotChart.Plot.Axes.AutoScale();

            plotChart.Refresh();
        }

        /// <summary>
        /// 创建指定角度的直径线数据
        /// </summary>
        private PolarAngleLine CreateDiameterLine(double angle, Mat mat)
        {
            PolarAngleLine polarLine = new PolarAngleLine
            {
                Angle = angle
            };

            double radians = angle * Math.PI / 180.0;

            // Sample from center (theta=0) to edge (theta=MaxAngle)
            for (int theta = 0; theta <= (int)maxAngle; theta++)
            {
                double radiusPixels = theta / conoscopeCoefficient;
                double x = imageCenter.X + radiusPixels * Math.Cos(radians);
                double y = imageCenter.Y + radiusPixels * Math.Sin(radians);

                int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                double r = 0, g = 0, b = 0;
                double X = 0, Y = 0, Z = 0;

                ExtractPixelValues(mat, ix, iy, out r, out g, out b, out X, out Y, out Z);

                polarLine.RgbData.Add(new RgbSample
                {
                    Position = theta,
                    R = r,
                    G = g,
                    B = b,
                    X = X,
                    Y = Y,
                    Z = Z
                });
            }

            return polarLine;
        }

        /// <summary>
        /// 从Mat中提取像素值
        /// </summary>
        private void ExtractPixelValues(Mat mat, int ix, int iy, out double r, out double g, out double b, out double X, out double Y, out double Z)
        {
            r = 0; g = 0; b = 0;
            X = Y = Z = 0;

            double pixelValue = GetPixelValue(mat, ix, iy);
            r = g = b = pixelValue;

            // Get XYZ values
            if (xChannelMat != null)
                X = GetPixelValue(xChannelMat, ix, iy);
            if (yChannelMat != null)
                Y = GetPixelValue(yChannelMat, ix, iy);
            if (zChannelMat != null)
                Z = GetPixelValue(zChannelMat, ix, iy);
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
        /// 获取指定通道的值
        /// </summary>
        private double GetChannelValue(RgbSample sample, ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.R => sample.R,
                ExportChannel.G => sample.G,
                ExportChannel.B => sample.B,
                ExportChannel.X => sample.X,
                ExportChannel.Y => sample.Y,
                ExportChannel.Z => sample.Z,
                _ => 0
            };
        }

        /// <summary>
        /// 导出直径线CSV - 导出0°到180°所有角度
        /// </summary>
        private void BtnExportDiameter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (yChannelMat == null || yChannelMat.Empty())
                {
                    MessageBox.Show("没有可导出的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"DiameterLine_Export_{displayChannel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Title = "保存直径线数据"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportAngleModeToCSV(saveFileDialog.FileName, displayChannel);
                    MessageBox.Show($"直径线数据导出成功！\n已导出0°-180°所有角度数据", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 按角度模式导出数据到CSV文件（参考原始代码）
        /// 格式: Phi \ Theta 矩阵格式
        /// 行: Theta (采样点位置，从0到MaxAngle)
        /// 列: Phi (角度线，从0°到180°)
        /// </summary>
        private void ExportAngleModeToCSV(string filePath, ExportChannel channel)
        {
            Mat? selectedMat = GetSelectedChannelMat(channel);
            if (selectedMat == null || selectedMat.Empty())
                return;

            // Create angle lines from 0° to 180°
            var angleLines = CreateAngleLinesForExport(selectedMat);

            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                if (angleLines.Count == 0)
                    return;

                // Write header comments
                writer.WriteLine($"# Diameter Line Export Data (Phi \\ Theta Format)");
                writer.WriteLine($"# Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"# Export Channel: {channel}");
                writer.WriteLine($"# Max Angle: {maxAngle}°");
                writer.WriteLine($"# Phi (Column): Diameter line direction (0°-180°)");
                writer.WriteLine($"# Theta (Row): Sample point position (0 to MaxAngle)");
                writer.WriteLine();

                // Write CSV header: Phi \ Theta, followed by each Phi angle (0-180)
                StringBuilder headerLine = new StringBuilder();
                headerLine.Append("Phi \\ Theta");
                foreach (var line in angleLines)
                {
                    headerLine.Append($",{line.Angle:F0}");
                }
                writer.WriteLine(headerLine.ToString());

                // Find the maximum number of samples across all lines
                int maxSamples = angleLines.Max(l => l.RgbData.Count);
                if (maxSamples == 0) return;

                // Export each row (Theta position from 0 to MaxAngle)
                for (int i = 0; i < maxSamples; i++)
                {
                    StringBuilder dataLine = new StringBuilder();

                    // Get Theta position from first line
                    double theta = angleLines[0].RgbData.Count > i ? angleLines[0].RgbData[i].Position : 0;
                    dataLine.Append($"{theta:F2}");

                    // Add value for each Phi angle
                    foreach (var line in angleLines)
                    {
                        if (line.RgbData.Count > i)
                        {
                            double value = GetChannelValue(line.RgbData[i], channel);
                            dataLine.Append($",{value:F2}");
                        }
                        else
                        {
                            dataLine.Append(",");
                        }
                    }
                    writer.WriteLine(dataLine.ToString());
                }
            }
        }

        /// <summary>
        /// 为导出创建从0°到180°的直径线数据
        /// </summary>
        private List<PolarAngleLine> CreateAngleLinesForExport(Mat mat)
        {
            var angleLines = new List<PolarAngleLine>();

            // Create lines from 0° to 180° (181 lines)
            for (int phi = 0; phi <= 180; phi++)
            {
                angleLines.Add(CreateDiameterLine(phi, mat));
            }

            return angleLines;
        }

        /// <summary>
        /// 导出R圆CSV
        /// </summary>
        private void BtnExportCircle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (yChannelMat == null || yChannelMat.Empty())
                {
                    MessageBox.Show("没有可导出的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"RCircle_Export_{displayChannel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Title = "保存R圆数据"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportCircleModeToCSV(saveFileDialog.FileName, displayChannel);
                    MessageBox.Show("R圆数据导出成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 按同心圆模式导出数据到CSV文件（参考原始代码）
        /// 格式: Phi \ Theta 矩阵格式
        /// 行: Theta (圆周角度，0-359°)
        /// 列: Phi (半径角度，0-MaxAngle)
        /// </summary>
        private void ExportCircleModeToCSV(string filePath, ExportChannel channel)
        {
            Mat? selectedMat = GetSelectedChannelMat(channel);
            if (selectedMat == null || selectedMat.Empty())
                return;

            // Create concentric circles data
            var concentricCircles = CreateConcentricCirclesData(selectedMat);

            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                if (concentricCircles.Count == 0)
                    return;

                // Write header comments
                writer.WriteLine($"# Concentric Circle Export Data (Phi \\ Theta Format)");
                writer.WriteLine($"# Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"# Export Channel: {channel}");
                writer.WriteLine($"# Max Angle: {maxAngle}°");
                writer.WriteLine($"# Phi (Column): Radius angle (0° to {maxAngle}°)");
                writer.WriteLine($"# Theta (Row): Circle position (0-359°)");
                writer.WriteLine();

                // Write CSV header
                StringBuilder headerLine = new StringBuilder();
                headerLine.Append("Phi \\ Theta");
                foreach (var circle in concentricCircles)
                {
                    headerLine.Append($",{circle.RadiusAngle:F0}");
                }
                writer.WriteLine(headerLine.ToString());

                // Export each row (360 positions)
                for (int anglePos = 0; anglePos < 360; anglePos++)
                {
                    StringBuilder dataLine = new StringBuilder();
                    dataLine.Append($"{anglePos}");

                    // Add value for each radius
                    foreach (var circle in concentricCircles)
                    {
                        if (circle.RgbData.Count > anglePos)
                        {
                            double value = GetChannelValue(circle.RgbData[anglePos], channel);
                            dataLine.Append($",{value:F2}");
                        }
                        else
                        {
                            dataLine.Append(",");
                        }
                    }
                    writer.WriteLine(dataLine.ToString());
                }
            }
        }

        /// <summary>
        /// 创建同心圆数据（参考原始代码）
        /// </summary>
        private List<ConcentricCircleLine> CreateConcentricCirclesData(Mat mat)
        {
            var concentricCircles = new List<ConcentricCircleLine>();

            // Create circles from 0 to MaxAngle
            for (int degree = 0; degree <= (int)maxAngle; degree++)
            {
                ConcentricCircleLine circleLine = new ConcentricCircleLine
                {
                    RadiusAngle = degree
                };

                if (degree == 0)
                {
                    // Center point: Use the center pixel value for all 360 samples
                    int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(imageCenter.X)));
                    int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(imageCenter.Y)));

                    double r = 0, g = 0, b = 0;
                    double X = 0, Y = 0, Z = 0;

                    ExtractPixelValues(mat, ix, iy, out r, out g, out b, out X, out Y, out Z);

                    // Fill all 360 samples with the center point value
                    for (int anglePos = 0; anglePos < 360; anglePos++)
                    {
                        circleLine.RgbData.Add(new RgbSample
                        {
                            Position = anglePos,
                            R = r,
                            G = g,
                            B = b,
                            X = X,
                            Y = Y,
                            Z = Z
                        });
                    }
                }
                else
                {
                    // Calculate radius in pixels for this degree angle
                    double radiusPixels = degree / conoscopeCoefficient;

                    // Sample points along the circle (360 samples)
                    for (int anglePos = 0; anglePos < 360; anglePos++)
                    {
                        double radians = anglePos * Math.PI / 180.0;
                        double x = imageCenter.X + radiusPixels * Math.Cos(radians);
                        double y = imageCenter.Y + radiusPixels * Math.Sin(radians);

                        int ix = Math.Max(0, Math.Min(mat.Width - 1, (int)Math.Round(x)));
                        int iy = Math.Max(0, Math.Min(mat.Height - 1, (int)Math.Round(y)));

                        double r = 0, g = 0, b = 0;
                        double X = 0, Y = 0, Z = 0;

                        ExtractPixelValues(mat, ix, iy, out r, out g, out b, out X, out Y, out Z);

                        circleLine.RgbData.Add(new RgbSample
                        {
                            Position = anglePos,
                            R = r,
                            G = g,
                            B = b,
                            X = X,
                            Y = Y,
                            Z = Z
                        });
                    }
                }

                concentricCircles.Add(circleLine);
            }

            return concentricCircles;
        }

        /// <summary>
        /// 显示角度选择改变
        /// </summary>
        private void CbDisplayAngle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDisplayAngle.SelectedItem is ComboBoxItem item && item.Tag is string angleStr)
            {
                if (int.TryParse(angleStr, out int angle))
                {
                    displayAngle = angle;
                    if (yChannelMat != null && !yChannelMat.Empty())
                    {
                        UpdateDisplay();
                    }
                }
            }
        }

        /// <summary>
        /// 显示通道选择改变
        /// </summary>
        private void CbDisplayChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDisplayChannel.SelectedItem is ComboBoxItem item && item.Tag is string channelStr)
            {
                if (Enum.TryParse<ExportChannel>(channelStr, out var channel))
                {
                    displayChannel = channel;
                    if (yChannelMat != null && !yChannelMat.Empty())
                    {
                        UpdateDisplay();
                    }
                }
            }
        }

        /// <summary>
        /// 窗口关闭时释放资源
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            xChannelMat?.Dispose();
            yChannelMat?.Dispose();
            zChannelMat?.Dispose();
            pseudoColorMat?.Dispose();
        }
    }
}
