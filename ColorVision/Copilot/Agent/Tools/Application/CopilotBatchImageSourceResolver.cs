using ColorVision.ImageEditor.BatchProcessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ColorVision.Copilot
{
    internal static class CopilotBatchImageSourceResolver
    {
        internal const int MaximumFiles = 500;

        internal static bool TryResolveSource(
            CopilotAgentRequest request,
            string source,
            out string fullPath,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(request);
            string requestedPath = source?.Trim() ?? string.Empty;
            if (CopilotWorkspaceSearchSupport.IsExplicitlyAllowedPath(requestedPath, request.ReadableLocalFilePaths))
            {
                fullPath = Path.GetFullPath(requestedPath);
                if (CopilotWorkspaceSearchSupport.HasReparsePointInPath(fullPath))
                {
                    error = $"The source path crosses a file-system reparse point and is not allowed: {fullPath}";
                    fullPath = string.Empty;
                    return false;
                }
                if (!File.Exists(fullPath))
                {
                    error = $"The source image file does not exist: {fullPath}";
                    fullPath = string.Empty;
                    return false;
                }

                error = string.Empty;
                return true;
            }

            var allowedRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(
                request.SearchRootPaths.Concat(request.ReadableLocalDirectoryPaths));
            return CopilotWorkspaceSearchSupport.TryResolveExistingPathWithinRoots(requestedPath, allowedRoots, out fullPath, out error);
        }

        internal static bool TryResolveItems(
            CopilotAgentRequest request,
            IReadOnlyList<string> sources,
            bool recursive,
            BatchImageProcessor processor,
            CancellationToken cancellationToken,
            out BatchImageItem[] items,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(sources);
            ArgumentNullException.ThrowIfNull(processor);
            items = Array.Empty<BatchImageItem>();
            var resolved = new List<BatchImageItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryResolveSource(request, source, out var fullPath, out error))
                    return false;

                if (File.Exists(fullPath))
                {
                    if (!processor.IsSupported(fullPath))
                    {
                        error = $"Unsupported image extension: {fullPath}";
                        return false;
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (seen.Add(fullPath))
                        resolved.Add(new BatchImageItem(fullPath, Path.GetDirectoryName(fullPath)));
                }
                else
                {
                    var firstDirectoryItem = resolved.Count;
                    var enumerationOptions = new EnumerationOptions
                    {
                        AttributesToSkip = FileAttributes.ReparsePoint,
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = recursive,
                        ReturnSpecialDirectories = false,
                    };
                    foreach (var file in Directory.EnumerateFiles(fullPath, "*", enumerationOptions))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var supported = processor.IsSupported(file);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!supported)
                            continue;
                        if (seen.Add(file))
                            resolved.Add(new BatchImageItem(Path.GetFullPath(file), fullPath));
                        if (resolved.Count > MaximumFiles)
                        {
                            error = $"The batch exceeds the {MaximumFiles}-file safety limit. Narrow the sources and retry.";
                            return false;
                        }
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    resolved.Sort(firstDirectoryItem, resolved.Count - firstDirectoryItem,
                        Comparer<BatchImageItem>.Create((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.FilePath, right.FilePath)));
                }
            }

            if (resolved.Count == 0)
            {
                error = "No supported image files were found in the approved sources.";
                return false;
            }
            if (resolved.Count > MaximumFiles)
            {
                error = $"The batch exceeds the {MaximumFiles}-file safety limit. Narrow the sources and retry.";
                return false;
            }

            items = resolved.ToArray();
            error = string.Empty;
            return true;
        }
    }
}
