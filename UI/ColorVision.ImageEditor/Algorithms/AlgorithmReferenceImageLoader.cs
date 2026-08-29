using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Single WPF file-to-canonical-buffer boundary shared by interactive and batch hosts.</summary>
    internal static class AlgorithmReferenceImageLoader
    {
        public static AlgorithmInput LoadTransferred(string role, string filePath, string colorSpace = "encoded-device-values")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(role);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            string fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath)) throw new FileNotFoundException($"Reference image '{role}' does not exist.", fullPath);
            byte[] checksumBytes;
            BitmapSource frame;
            using (FileStream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                checksumBytes = SHA256.HashData(stream);
                stream.Position = 0;
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0) throw new InvalidDataException($"Reference image '{role}' has no readable frame.");
                frame = decoder.Frames[0];
                _ = ImageAlgorithmInputFactory.FromPixelFormat(frame.Format);
            }
            WriteableBitmap snapshot = new(frame);
            if (snapshot.CanFreeze) snapshot.Freeze();
            return new AlgorithmInput
            {
                Name = role,
                Image = ImageAlgorithmInputFactory.Copy(snapshot),
                Ownership = AlgorithmInputOwnership.Transferred,
                SourceUri = fullPath,
                Checksum = $"sha256:{Convert.ToHexString(checksumBytes).ToLowerInvariant()}",
                ColorSpace = colorSpace,
            };
        }

        public static IReadOnlyList<AlgorithmInput> LoadEnabledReferences(ImagingCorrectionParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            List<AlgorithmInput> inputs = new(4);
            try
            {
                Add(inputs, parameters.EnableDarkFrame, "dark-frame", parameters.DarkFramePath);
                Add(inputs, parameters.EnableFlatField, "flat-field", parameters.FlatFieldPath);
                Add(inputs, parameters.EnableShading, "shading-reference", parameters.ShadingReferencePath);
                Add(inputs, parameters.EnableBadPixelCorrection, "bad-pixel-map", parameters.BadPixelMapPath);
                return inputs;
            }
            catch
            {
                foreach (AlgorithmInput input in inputs) input.Image.Dispose();
                throw;
            }
        }

        private static void Add(List<AlgorithmInput> inputs, bool enabled, string role, string path)
        {
            if (!enabled) return;
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException($"Correction stage '{role}' is enabled, but no reference image path is configured.");
            inputs.Add(LoadTransferred(role, path));
        }
    }
}
