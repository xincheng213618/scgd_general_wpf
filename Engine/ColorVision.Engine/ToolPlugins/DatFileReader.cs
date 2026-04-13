using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI.Menus;
using Microsoft.Win32;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.Engine.ToolPlugins
{







    public class DatFileReaderAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "DAT File Reader",
                    Group = Properties.Resources.InternalTools,
                    Order = 7,
                    LaunchAction = Execute,
                }
            };
        }

        private static void Execute()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "DAT Files (*.dat)|*.dat|All Files (*.*)|*.*",
                Title = "选择要读取的 DAT 文件"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            try
            {
                DatFileReader.PositionData data = DatFileReader.ReadPositionFile(openFileDialog.FileName);

                int cy = data.MatData.Rows / 2;
                int cx = data.MatData.Cols / 2;
                Vec2d pointVal = data.MatData.At<Vec2d>(cy, cx);

                System.Windows.MessageBox.Show(
                    $"加载成功: {openFileDialog.FileName}\n" +
                    $"Mat 尺寸: {data.MatData.Rows}x{data.MatData.Cols}\n" +
                    $"Mat 类型: {data.MatData.Type()}\n" +
                    $"Attribute Angle: {data.Attribute.angle}\n" +
                    $"Attribute Center: {data.Attribute.center}\n" +
                    $"中心点数据 (Row:{cy}, Col:{cx}): X={pointVal.Item0}, Y={pointVal.Item1}",
                    "DAT Reader",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "DAT Reader", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    public partial class DatFileReader
    {
        // C++: PositionAttribute 对应结构体
        // 注意：虽然可以使用 StructLayout 整体读取，但为了避免 C++ 编译器的字节对齐(Padding)陷阱，
        // 建议保持逐字段读取，或者在 C++ 端确保是 #pragma pack(1)
        public struct PositionAttribute
        {
            public double step_x;
            public double step_y;
            public double angle;
            public int rows;
            public int cols;
            public Point2f center;      // OpenCvSharp.Point2f
            public Rect outline;        // OpenCvSharp.Rect
            public double mean_value;
            public int type;
            public Point2f[] cornet_pts; // 4个点
            public Point2f shift_x;
            public Point2f shift_y;
        }

        public class PositionData
        {
            public PositionAttribute Attribute { get; set; }

            // 使用 OpenCvSharp 的 Mat 存储数据
            // 类型为 CV_64FC2 (双通道 double)
            public Mat MatData { get; set; }
        }

        public static PositionData ReadPositionFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                var result = new PositionData();

                // 1. Read Header
                int height = (int)reader.ReadUInt32();
                int width = (int)reader.ReadUInt32();

                // --- 关键修改开始 ---

                // 记录读取 Attribute 之前的文件位置
                long startPos = fs.Position;

                // 2. Read Attribute (逐字段读取)
                var attr = new PositionAttribute();
                attr.step_x = reader.ReadDouble();
                attr.step_y = reader.ReadDouble();
                attr.angle = reader.ReadDouble();
                attr.rows = reader.ReadInt32();
                attr.cols = reader.ReadInt32();
                attr.center = new Point2f(reader.ReadSingle(), reader.ReadSingle());
                attr.outline = new Rect(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                attr.mean_value = reader.ReadDouble();
                attr.type = reader.ReadInt32(); // 读完这个 int (4 bytes)

                // C++ 可能会在这里插入 Padding！

                attr.cornet_pts = new Point2f[4];
                for (int i = 0; i < 4; i++) attr.cornet_pts[i] = new Point2f(reader.ReadSingle(), reader.ReadSingle());
                attr.shift_x = new Point2f(reader.ReadSingle(), reader.ReadSingle());
                attr.shift_y = new Point2f(reader.ReadSingle(), reader.ReadSingle());

                result.Attribute = attr;

                // --- 关键纠正逻辑 ---

                // 此时我们手动读取了 116 字节。
                // 但如果 C++ 的 sizeof(attri) 是 120 字节，文件流现在还差 4 个字节才到 Mat 数据。
                // 我们需要跳过这些 Padding。

                // 假设：我们知道文件结构是 Header(8) + Struct(N) + MatData
                // MatData 的大小是固定的：height * width * 16
                // 文件总大小 = 8 + StructSize + MatDataSize
                // 所以 StructSize = 文件总大小 - 8 - MatDataSize

                long totalFileSize = fs.Length;
                long matDataSize = (long)height * width * 16;
                long expectedStructSize = totalFileSize - 8 - matDataSize;

                long bytesReadForStruct = fs.Position - startPos; // 应该是 116

                if (expectedStructSize > bytesReadForStruct)
                {
                    int paddingToSkip = (int)(expectedStructSize - bytesReadForStruct);
                    Console.WriteLine($"检测到 C++ 结构体 Padding: {paddingToSkip} 字���。正在跳过...");
                    reader.ReadBytes(paddingToSkip); // 跳过 Padding
                }
                else if (expectedStructSize < bytesReadForStruct)
                {
                    // 这种情况很少见，说明甚至还没读完结构体文件就结束了，或者是数据算错了
                    Console.WriteLine("警告：文件大小小于预期，可能文件损坏或结构体定义不匹配。");
                }

                // --- 关键修改结束，现在文件指针正好指向 Mat 数据的开头 ---

                // 3. Read Mat Data
                byte[] buffer = reader.ReadBytes((int)matDataSize);
                Mat mat = new Mat(height, width, MatType.CV_64FC2);
                Marshal.Copy(buffer, 0, mat.Data, buffer.Length);
                result.MatData = mat;

                return result;
            }
        }

    }
}
