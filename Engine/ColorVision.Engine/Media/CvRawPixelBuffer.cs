using ColorVision.FileIO;
using System;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Media
{
    /// <summary>One reusable RAW input per view. The opener serializes reads and retires previous consumers before overwriting it.</summary>
    internal sealed class CvRawPixelBuffer
    {
        private byte[]? pixels;
        private (int Width, int Height, int Bpp, int Channels) layout;

        internal CVCIEFile Read(string path)
        {
            using Stream stream = CVFileReadCache.OpenRead(path);
            int offset = CVFileUtil.ReadCIEFileHeader(stream, out CVCIEFile file);
            try
            {
                if (offset <= 0 || file.Cols <= 0 || file.Rows <= 0
                    || file.Bpp is not (8 or 16 or 32 or 64) || file.Channels is not (1 or 3 or 4))
                    throw new InvalidDataException("CVRAW 文件头或像素格式无效。");

                using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
                long length = file.Version == 2 ? reader.ReadInt64() : reader.ReadInt32();
                long expected = checked((long)file.Cols * file.Rows * file.Channels * (file.Bpp / 8));
                if (length != expected || length > Array.MaxLength || length > stream.Length - stream.Position)
                    throw new InvalidDataException("CVRAW 像素长度与宽高、通道或位深不匹配。");

                var nextLayout = (file.Cols, file.Rows, file.Bpp, file.Channels);
                if (pixels == null || layout != nextLayout)
                {
                    pixels = null;
                    // A stable address avoids repeatedly pinning/moving a full camera frame on the managed heap.
                    pixels = GC.AllocateUninitializedArray<byte>((int)length, pinned: true);
                    layout = nextLayout;
                }
                stream.ReadExactly(pixels);
                file.Data = pixels;
                file.FileExtType = CVType.Raw;
                file.FilePath = path;
                return file;
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        internal void Clear() => pixels = null;
    }
}
