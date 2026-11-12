using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotConvertBatchImagesTool : ICopilotWorkspaceMutationEvidenceSource
    {
        IReadOnlyList<string> ICopilotWorkspaceMutationEvidenceSource.GetWorkspaceRecheckPaths(
            CopilotAgentRequest request,
            CopilotAgentToolInput input)
        {
            if (!TryParseOptions(input, out var options, out _))
                return Array.Empty<string>();

            var sourceDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in options.Sources)
            {
                if (!CopilotBatchImageSourceResolver.TryResolveSource(request, source, out var fullPath, out _))
                    return Array.Empty<string>();
                var directory = Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(directory))
                    return Array.Empty<string>();
                sourceDirectories.Add(Path.GetFullPath(directory));
            }

            // A collision can renumber an output while processing. Directories preserve
            // the inspection scope without claiming a filename or a completed write.
            if (!string.IsNullOrWhiteSpace(options.OutputDirectory))
            {
                try
                {
                    var outputDirectory = Path.GetFullPath(options.OutputDirectory);
                    return IsPotentialOutputPathWithinRoots(outputDirectory, request.WritableLocalRootPaths)
                        ? [outputDirectory]
                        : Array.Empty<string>();
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
                {
                    return Array.Empty<string>();
                }
            }

            return sourceDirectories.All(directory => IsPotentialOutputPathWithinRoots(directory, request.WritableLocalRootPaths))
                ? sourceDirectories.ToArray()
                : Array.Empty<string>();
        }
    }
}
