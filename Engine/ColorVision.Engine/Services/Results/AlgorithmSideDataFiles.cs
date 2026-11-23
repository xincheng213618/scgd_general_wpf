using ColorVision.Common.Utilities;
using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Services.Results;

internal static class AlgorithmSideDataFiles
{
    private const int MaximumNameAttempts = 128;
    private static readonly ILog Log = LogManager.GetLogger(typeof(AlgorithmSideDataFiles));

    internal static void WriteCsv(ViewResultAlg result, string directory, string csvName, string[] headers,
        IEnumerable<string?[]> rows, string? annotatedImagePath = null)
    {
        Write(result, directory, csvName, path =>
        {
            using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
            using var csv = new CsvHelper.CsvWriter(writer, CultureInfo.InvariantCulture, leaveOpen: true);
            WriteRow(headers);
            foreach (string?[] row in rows)
                WriteRow(row);

            void WriteRow(string?[] values)
            {
                foreach (string? value in values)
                    csv.WriteField(value);
                csv.NextRecord();
            }
        }, annotatedImagePath);
    }

    internal static void Write(ViewResultAlg result, string directory, string fileName, Action<string> writeFile, string? annotatedImagePath = null)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        fileName = Tool.SanitizeFileName(fileName);
        string? imageName = annotatedImagePath == null ? null : Tool.SanitizeFileName(Path.GetFileName(annotatedImagePath));
        if (string.Equals(imageName, fileName, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"The annotated image and data file would have the same name: {fileName}");

        string stagedFile = Path.Combine(directory, $".{Guid.NewGuid():N}.cv-side-data.tmp");
        string? stagedImage = annotatedImagePath == null ? null : Path.Combine(directory, $".{Guid.NewGuid():N}.cv-side-data.tmp");
        bool writeFailed = false;
        try
        {
            writeFile(stagedFile);
            if (stagedImage != null)
            {
                using FileStream source = File.OpenRead(annotatedImagePath!);
                using FileStream destination = new(stagedImage, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                source.CopyTo(destination);
            }

            string resultSuffix = $"_result-{result.ResultType}-{result.Id.ToString(CultureInfo.InvariantCulture)}";
            for (int attempt = 0; attempt < MaximumNameAttempts; attempt++)
            {
                string suffix = attempt switch
                {
                    0 => string.Empty,
                    1 => resultSuffix,
                    _ => resultSuffix + "-" + attempt.ToString(CultureInfo.InvariantCulture),
                };
                string dataFile = Path.Combine(directory, WithSuffix(fileName, suffix));
                string? image = imageName == null ? null : Path.Combine(directory, WithSuffix(imageName, suffix));
                if (IsOccupied(dataFile) || (image != null && IsOccupied(image)))
                    continue;

                if (!TryPublish(stagedFile, dataFile, stagedImage, image))
                    continue;

                if (attempt > 0)
                    Log.Info($"算法数据列导出使用带结果编号的文件：resultId={result.Id}, dataFile={dataFile}, annotatedImage={image}");
                return;
            }

            throw new IOException($"No available filename for result {result.Id} in '{directory}'.");
        }
        catch
        {
            writeFailed = true;
            throw;
        }
        finally
        {
            try { DeleteTemporaryFile(stagedFile, writeFailed); }
            finally { if (stagedImage != null) DeleteTemporaryFile(stagedImage, writeFailed); }
        }
    }

    private static bool TryPublish(string stagedFile, string dataFile, string? stagedImage, string? image)
    {
        string destination = image ?? dataFile;
        bool imagePublished = false;
        try
        {
            if (image != null)
            {
                File.Move(stagedImage!, image);
                imagePublished = true;
            }

            // The data file is published last for a paired export. Neither move replaces an existing file.
            destination = dataFile;
            File.Move(stagedFile, dataFile);
            return true;
        }
        catch (Exception error) when (imagePublished || error is IOException or UnauthorizedAccessException)
        {
            bool nameCollision = (error is IOException or UnauthorizedAccessException) && IsOccupied(destination);
            if (imagePublished)
            {
                try { File.Move(image!, stagedImage!); }
                catch (Exception rollbackError)
                {
                    throw new IOException($"Unable to roll back the annotated image '{image}'.", new AggregateException(error, rollbackError));
                }
            }
            if (nameCollision) return false;
            throw;
        }
    }

    private static bool IsOccupied(string path) => File.Exists(path) || Directory.Exists(path);

    private static void DeleteTemporaryFile(string path, bool writeFailed)
    {
        try { File.Delete(path); }
        catch (Exception error) when (writeFailed && (error is IOException or UnauthorizedAccessException))
        {
            Log.Warn($"算法数据列导出失败后无法清理暂存文件：{path}", error);
        }
    }

    private static string WithSuffix(string name, string suffix) =>
        string.IsNullOrEmpty(suffix) ? name : Path.GetFileNameWithoutExtension(name) + suffix + Path.GetExtension(name);
}
