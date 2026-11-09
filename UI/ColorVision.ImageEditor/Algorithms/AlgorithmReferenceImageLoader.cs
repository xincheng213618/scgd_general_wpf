using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Single WPF file-to-canonical-buffer boundary shared by interactive and batch hosts.</summary>
    internal static class AlgorithmReferenceImageLoader
    {
        public static AlgorithmInput LoadTransferred(string role, string filePath, string colorSpace = "encoded-device-values",
            BatchCache? cache = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(role);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            string fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath)) throw new FileNotFoundException($"Reference image '{role}' does not exist.", fullPath);
            // Keep the Windows read-sharing handle through hashing and canonical pixel copying.
            // Otherwise a writer can change the reference between these two reads.
            using FileStream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            string checksum = $"sha256:{Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()}";
            cancellationToken.ThrowIfCancellationRequested();
            return new AlgorithmInput
            {
                Name = role,
                Image = cache == null ? Decode(stream, role) : cache.GetOrDecode(role, fullPath, checksum, stream),
                Ownership = AlgorithmInputOwnership.Transferred,
                SourceUri = fullPath,
                Checksum = checksum,
                ColorSpace = colorSpace,
            };
        }

        private static AlgorithmImageBuffer Decode(Stream stream, string role)
        {
            stream.Position = 0;
            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) throw new InvalidDataException($"Reference image '{role}' has no readable frame.");
            BitmapSource frame = decoder.Frames[0];
            _ = ImageAlgorithmInputFactory.FromPixelFormat(frame.Format);
            return ImageAlgorithmInputFactory.Copy(frame);
        }

        public static IReadOnlyList<AlgorithmInput> LoadEnabledReferences(ImagingCorrectionParameters parameters,
            BatchCache? cache = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            List<AlgorithmInput> inputs = new(4);
            try
            {
                Add(parameters.EnableDarkFrame, "dark-frame", parameters.DarkFramePath);
                Add(parameters.EnableFlatField, "flat-field", parameters.FlatFieldPath);
                Add(parameters.EnableShading, "shading-reference", parameters.ShadingReferencePath);
                Add(parameters.EnableBadPixelCorrection, "bad-pixel-map", parameters.BadPixelMapPath);
                cancellationToken.ThrowIfCancellationRequested();
                return inputs;
            }
            catch
            {
                foreach (AlgorithmInput input in inputs) input.Image.Dispose();
                throw;
            }

            void Add(bool enabled, string role, string path)
            {
                if (!enabled) return;
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException($"Correction stage '{role}' is enabled, but no reference image path is configured.");
                inputs.Add(LoadTransferred(role, path, cache: cache, cancellationToken: cancellationToken));
            }
        }

        /// <summary>Retains at most one verified pixel snapshot per correction role for one sequential batch.</summary>
        internal sealed class BatchCache : IDisposable
        {
            private sealed record Entry(string Path, string Checksum, AlgorithmImageBuffer Image);

            private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

            internal AlgorithmImageBuffer GetOrDecode(string role, string path, string checksum, Stream stream)
            {
                if (_entries.TryGetValue(role, out Entry? previous)
                    && string.Equals(previous.Path, path, StringComparison.OrdinalIgnoreCase)
                    && previous.Checksum == checksum)
                    return previous.Image.RetainReadOnly();

                AlgorithmImageBuffer image = Decode(stream, role);
                _entries[role] = new Entry(path, checksum, image);
                previous?.Image.Dispose();
                return image.RetainReadOnly();
            }

            public void Dispose()
            {
                foreach (Entry entry in _entries.Values) entry.Image.Dispose();
                _entries.Clear();
            }
        }
    }
}
