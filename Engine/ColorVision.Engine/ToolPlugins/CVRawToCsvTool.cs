using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.UI.Menus;
using Microsoft.Win32;
using OpenCvSharp;
using System;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.ToolPlugins
{
    public class CVRawToCsvTool : MenuItemBase
    {
        public override string OwnerGuid => "Tool";
        public override string GuidId => "SingleChannelImageToCsvTool";
        public override int Order => 8;
        public override string Header => "转换单通道图到 CSV";

        public override void Execute()
        {
            // 1. 弹窗选中 cvraw/cvcie/tif/tiff 文件
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Supported Image Files (*.cvraw;*.cvcie;*.tif;*.tiff)|*.cvraw;*.cvcie;*.tif;*.tiff|CVRaw/CVCIE Files (*.cvraw;*.cvcie)|*.cvraw;*.cvcie|TIFF Files (*.tif;*.tiff)|*.tif;*.tiff|All Files (*.*)|*.*",
                Title = "选择要转换的 单通道 文件"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            string filePath = openFileDialog.FileName;

            try
            {
                Mat mat = null;
                string ext = Path.GetExtension(filePath).ToLower();

                // 2. 根据文件扩展名采取不同的读取策略
                if (ext == ".cvraw" || ext == ".cvcie")
                {
                    using CVCIEFile cvcieFile = CVFileUtil.OpenLocalCVFile(filePath);
                    if (cvcieFile == null)
                    {
                        MessageBox.Show("文件读取失败或不支持的格式。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (cvcieFile.Channels != 1)
                    {
                        MessageBox.Show("无法提取：该文件不是单通道图像。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    mat = cvcieFile.ToMat();
                }
                else if (ext == ".tif" || ext == ".tiff")
                {
                    // 使用 Unchanged 读取以保留原始通道和位深（例如16bit或32bit）
                    mat = Cv2.ImRead(filePath, ImreadModes.Unchanged);
                    if (mat.Empty())
                    {
                        mat.Dispose();
                        MessageBox.Show("文件读取失败或损坏的 TIFF 文件。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (mat.Channels() != 1)
                    {
                        mat.Dispose();
                        MessageBox.Show("无法提取：该文件不是单通道图像。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("不支持的文件类型。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. 使用 mat 时用 using 包装以确保及时释放内存
                using (mat)
                {
                    if (mat == null || mat.Empty())
                    {
                        MessageBox.Show("转换为 Mat 失败。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 4. 提示保存位置
                    SaveFileDialog saveFileDialog = new SaveFileDialog
                    {
                        Filter = "CSV Files (*.csv)|*.csv",
                        FileName = Path.GetFileNameWithoutExtension(filePath) + ".csv",
                        Title = "保存 CSV 文件"
                    };

                    if (saveFileDialog.ShowDialog() != true)
                        return;

                    string savePath = saveFileDialog.FileName;

                    // 5. 把 mat 中的数组转换成 csv 保存到提示位置
                    SaveMatToCsv(mat, savePath);

                    MessageBox.Show($"成功保存到:\n{savePath}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生错误: {ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 提取 Mat 像素数组并转存为 CSV
        /// </summary>
        private void SaveMatToCsv(Mat mat, string filePath)
        {
            // 对于较大图像避免大量 string 相加，使用 StreamWriter 高效逐行写入
            using StreamWriter sw = new StreamWriter(filePath);
            int rows = mat.Rows;
            int cols = mat.Cols;

            if (mat.Type() == MatType.CV_8UC1)
            {
                mat.GetArray<byte>(out byte[] data);
                WriteCsv(sw, data, rows, cols);
            }
            else if (mat.Type() == MatType.CV_16UC1)
            {
                mat.GetArray<ushort>(out ushort[] data);
                WriteCsv(sw, data, rows, cols);
            }
            else if (mat.Type() == MatType.CV_16SC1)
            {
                mat.GetArray<short>(out short[] data);
                WriteCsv(sw, data, rows, cols);
            }
            else if (mat.Type() == MatType.CV_32FC1)
            {
                mat.GetArray<float>(out float[] data);
                WriteCsv(sw, data, rows, cols);
            }
            else if (mat.Type() == MatType.CV_64FC1)
            {
                mat.GetArray<double>(out double[] data);
                WriteCsv(sw, data, rows, cols);
            }
            else
            {
                throw new NotSupportedException($"不支持的 Mat 类型: {mat.Type()}");
            }
        }

        /// <summary>
        /// 将泛型一维数组以 CSV 格式写出
        /// </summary>
        private void WriteCsv<T>(StreamWriter sw, T[] data, int rows, int cols)
        {
            for (int i = 0; i < rows; i++)
            {
                int offset = i * cols;
                for (int j = 0; j < cols; j++)
                {
                    sw.Write(data[offset + j]?.ToString());
                    if (j < cols - 1)
                        sw.Write(",");
                }
                sw.WriteLine(); // 换行
            }
        }
    }
}