using OpenCvSharp;
using System.IO;
using System.Runtime.InteropServices;

namespace Conoscope.Core
{
    internal static class ConoscopeReferenceMatSerializer
    {
        private const int CurrentVersion = 1;

        public static void Save(string filePath, Mat source)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using Mat floatMat = source.Clone();

            if (floatMat.Type() != MatType.CV_32FC1)
            {
                using Mat converted = new();
                floatMat.ConvertTo(converted, MatType.CV_32FC1);
                WriteMat(filePath, converted);
                return;
            }

            WriteMat(filePath, floatMat);
        }

        public static Mat Load(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            using BinaryReader reader = new(stream);

            int version = reader.ReadInt32();
            if (version != CurrentVersion)
            {
                throw new InvalidDataException($"Unsupported reference mat version: {version}");
            }

            int rows = reader.ReadInt32();
            int cols = reader.ReadInt32();
            MatType matType = (MatType)reader.ReadInt32();
            int length = reader.ReadInt32();
            byte[] buffer = reader.ReadBytes(length);

            Mat mat = new(rows, cols, matType);
            int expectedLength = checked((int)(mat.Total() * mat.ElemSize()));
            if (buffer.Length != expectedLength)
            {
                mat.Dispose();
                throw new InvalidDataException($"Reference mat payload length mismatch. Expected {expectedLength}, actual {buffer.Length}.");
            }

            Marshal.Copy(buffer, 0, mat.Data, buffer.Length);
            return mat;
        }

        private static void WriteMat(string filePath, Mat source)
        {
            int length = checked((int)(source.Total() * source.ElemSize()));
            byte[] buffer = new byte[length];
            Marshal.Copy(source.Data, buffer, 0, length);

            using FileStream stream = File.Create(filePath);
            using BinaryWriter writer = new(stream);
            writer.Write(CurrentVersion);
            writer.Write(source.Rows);
            writer.Write(source.Cols);
            writer.Write((int)source.Type());
            writer.Write(buffer.Length);
            writer.Write(buffer);
        }
    }
}