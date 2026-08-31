using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotImageAttachmentAdmissionFailureKind
    {
        RejectedInput,
        Storage,
    }

    internal sealed class CopilotImageAttachmentAdmissionException : Exception
    {
        public CopilotImageAttachmentAdmissionFailureKind FailureKind { get; }

        public CopilotImageAttachmentAdmissionException(
            CopilotImageAttachmentAdmissionFailureKind failureKind,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            FailureKind = failureKind;
        }
    }

    internal static class CopilotImageAttachmentAdmission
    {
        public static async Task<IReadOnlyList<CopilotAttachmentItem>> PersistAsync(
            IReadOnlyList<CopilotAttachmentItem> attachments,
            string attachmentDirectoryPath,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(attachments);
            if (!attachments.Any(attachment => attachment?.Type == CopilotAttachmentType.Image))
                return attachments;

            IReadOnlyList<CopilotImagePayload> payloads;
            try
            {
                payloads = await CopilotImagePayloadLoader.LoadAsync(
                    attachments,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CopilotImageAttachmentAdmissionException(
                    CopilotImageAttachmentAdmissionFailureKind.RejectedInput,
                    CopilotUserFacingErrorFormatter.Sanitize(ex.Message),
                    ex);
            }

            try
            {
                var attachmentRoot = PrepareStorageDirectory(attachmentDirectoryPath);
                var preparedImages = payloads
                    .Select(payload => PrepareStoredImage(payload, attachmentRoot))
                    .ToArray();

                foreach (var preparedImage in preparedImages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await EnsureStoredAsync(preparedImage, cancellationToken).ConfigureAwait(false);
                }

                var admitted = new List<CopilotAttachmentItem>(attachments.Count);
                var imageIndex = 0;
                foreach (var attachment in attachments)
                {
                    if (attachment.Type != CopilotAttachmentType.Image)
                    {
                        admitted.Add(attachment.CreateSnapshot());
                        continue;
                    }

                    var snapshot = attachment.CreateSnapshot();
                    snapshot.Value = preparedImages[imageIndex++].FilePath;
                    admitted.Add(snapshot);
                }
                return admitted;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CopilotImageAttachmentAdmissionException(
                    CopilotImageAttachmentAdmissionFailureKind.Storage,
                    "无法将图片保存到 Copilot 会话附件目录。请检查磁盘空间和目录权限后重试。",
                    ex);
            }
        }

        internal static string PrepareStorageDirectory(string attachmentDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(attachmentDirectoryPath))
                throw new InvalidOperationException("The Copilot attachment directory is unavailable.");

            var attachmentRoot = Path.GetFullPath(attachmentDirectoryPath);
            EnsureSafeStoragePath(attachmentRoot);
            Directory.CreateDirectory(attachmentRoot);
            EnsureSafeStoragePath(attachmentRoot);
            return attachmentRoot;
        }

        private static void EnsureSafeStoragePath(string path)
        {
            for (string? current = path; !string.IsNullOrWhiteSpace(current); current = Path.GetDirectoryName(current))
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(current);
                }
                catch (FileNotFoundException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("The Copilot attachment storage path crosses a file-system reparse point.");
            }
        }

        private static PreparedStoredImage PrepareStoredImage(
            CopilotImagePayload payload,
            string attachmentRoot)
        {
            var bytes = Convert.FromBase64String(payload.Base64Data);
            var hash = SHA256.HashData(bytes);
            var extension = payload.MediaType switch
            {
                "image/gif" => ".gif",
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => throw new InvalidOperationException(
                    $"Unsupported prepared image media type: {payload.MediaType}"),
            };
            var fileName = "image-" + Convert.ToHexString(hash).ToLowerInvariant() + extension;
            return new PreparedStoredImage(
                Path.Combine(attachmentRoot, fileName),
                bytes,
                hash);
        }

        private static async Task EnsureStoredAsync(
            PreparedStoredImage image,
            CancellationToken cancellationToken)
        {
            EnsureSafeStoragePath(image.FilePath);
            if (File.Exists(image.FilePath))
            {
                await VerifyStoredAsync(image, cancellationToken).ConfigureAwait(false);
                return;
            }

            var temporaryPath = image.FilePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(image.Bytes, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }

                try
                {
                    File.Move(temporaryPath, image.FilePath);
                }
                catch (IOException) when (File.Exists(image.FilePath))
                {
                    await VerifyStoredAsync(image, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        private static async Task VerifyStoredAsync(
            PreparedStoredImage image,
            CancellationToken cancellationToken)
        {
            var fileInfo = new FileInfo(image.FilePath);
            if (fileInfo.Length != image.Bytes.LongLength)
                throw new IOException("An existing managed image attachment does not match its content address.");

            var storedBytes = await File.ReadAllBytesAsync(
                image.FilePath,
                cancellationToken).ConfigureAwait(false);
            var storedHash = SHA256.HashData(storedBytes);
            if (!CryptographicOperations.FixedTimeEquals(storedHash, image.Hash))
                throw new IOException("An existing managed image attachment failed integrity verification.");
        }

        private static void TryDeleteTemporaryFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
            }
        }

        private sealed record PreparedStoredImage(
            string FilePath,
            byte[] Bytes,
            byte[] Hash);
    }
}
