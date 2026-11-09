using System;
using System.IO;
using System.Text;
using System.Threading;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace ColorVision.Copilot
{
    // Exposes a PDF's text layer as line-addressable text so the existing bounded read and
    // continuation cursors apply. Scanned pages have no text layer, so nothing here does OCR.
    internal static class CopilotPdfTextExtractor
    {
        internal const long MaximumFileBytes = 64L * 1024 * 1024;
        internal const int MaximumExtractedCharacters = 2_000_000;
        private static readonly object CacheLock = new();
        private static (string Path, long Length, DateTime LastWriteUtc, string Text)? _lastExtraction;

        internal static bool HasPdfSignature(ReadOnlySpan<byte> prefix) => prefix.StartsWith("%PDF-"u8);

        internal static string Extract(string path, CancellationToken cancellationToken)
        {
            var info = new FileInfo(path);
            if (info.Length > MaximumFileBytes)
                throw new InvalidDataException($"The PDF exceeds the {MaximumFileBytes / 1024 / 1024} MiB read limit. Ask the user for the relevant pages or a text export.");

            // A continuation read asks for the same document again; parsing it each time would be the slow part.
            lock (CacheLock)
            {
                if (_lastExtraction is { } cached
                    && string.Equals(cached.Path, path, StringComparison.OrdinalIgnoreCase)
                    && cached.Length == info.Length
                    && cached.LastWriteUtc == info.LastWriteTimeUtc)
                {
                    return cached.Text;
                }
            }

            var text = ExtractCore(path, cancellationToken);
            lock (CacheLock)
                _lastExtraction = (path, info.Length, info.LastWriteTimeUtc, text);
            return text;
        }

        private static string ExtractCore(string path, CancellationToken cancellationToken)
        {
            try
            {
                using var document = PdfDocument.Open(path);
                var builder = new StringBuilder();
                var hasText = false;
                foreach (var page in document.GetPages())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var pageText = ContentOrderTextExtractor.GetText(page, true).Trim();
                    hasText |= pageText.Length > 0;
                    builder.Append("[Page ").Append(page.Number).Append(" of ").Append(document.NumberOfPages).Append("]\n");
                    builder.Append(pageText.Length > 0 ? pageText : "<no text on this page>").Append("\n\n");
                    if (builder.Length > MaximumExtractedCharacters)
                    {
                        if (page.Number < document.NumberOfPages)
                            builder.Append($"[Text limit reached; pages {page.Number + 1}-{document.NumberOfPages} were not extracted. Ask the user for those pages.]\n");
                        break;
                    }
                }

                if (!hasText)
                    throw new InvalidDataException("The PDF has no extractable text layer, so it is probably scanned. Ask the user for a text version, or for page images to attach.");
                return builder.ToString();
            }
            catch (PdfDocumentEncryptedException)
            {
                throw new InvalidDataException("The PDF is password-protected. Ask the user for an unprotected copy.");
            }
            catch (Exception ex) when (ex is not (OperationCanceledException or InvalidDataException or OutOfMemoryException))
            {
                throw new InvalidDataException("The PDF could not be parsed: " + ex.Message, ex);
            }
        }
    }
}
