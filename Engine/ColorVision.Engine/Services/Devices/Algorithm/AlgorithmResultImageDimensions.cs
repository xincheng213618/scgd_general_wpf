using ColorVision.Database;
using ColorVision.Engine.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Algorithm;

internal static class AlgorithmResultImageDimensions
{
    public static bool TryRecoverFromMeasureResults(ViewResultAlg result, out int width, out int height)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Capture already persists the coordinate space in t_scgd_measure_result_img.file_data.
        // Reuse it here so older deployments do not require new result-master columns before querying.
        int? batchId = result.AlgResultMasterModel?.BatchId;
        if (batchId is not > 0)
        {
            width = 0;
            height = 0;
            return false;
        }

        List<MeasureResultImgModel> measureResults = MeasureImgResultDao.Instance.GetAllByBatchId(batchId.Value);
        return TrySelectFromMeasureResults(
            measureResults,
            SplitPaths(result.FilePath),
            result.AlgResultMasterModel?.Zindex,
            out width,
            out height);
    }

    internal static bool TrySelectFromMeasureResults(
        IReadOnlyCollection<MeasureResultImgModel> measureResults,
        IEnumerable<string?> expectedPaths,
        int? expectedZIndex,
        out int width,
        out int height)
    {
        ArgumentNullException.ThrowIfNull(measureResults);
        ArgumentNullException.ThrowIfNull(expectedPaths);

        string[] paths = expectedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string path in paths)
        {
            IEnumerable<MeasureResultImgModel> exactPathMatches = measureResults.Where(item =>
                PathsEqual(path, item.FileUrl) || PathsEqual(path, item.RawFile));
            if (TrySelectUniqueSize(exactPathMatches, out width, out height))
                return true;
        }

        if (expectedZIndex.HasValue
            && TrySelectUniqueSize(
                measureResults.Where(item => item.ZIndex == expectedZIndex),
                out width,
                out height))
        {
            return true;
        }

        return TrySelectUniqueSize(measureResults, out width, out height);
    }

    private static IEnumerable<string> SplitPaths(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Enumerable.Empty<string>()
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TrySelectUniqueSize(
        IEnumerable<MeasureResultImgModel> measureResults,
        out int width,
        out int height)
    {
        (int Width, int Height)[] sizes = measureResults
            .Select(item => TryReadFrameInfo(item, out int itemWidth, out int itemHeight)
                ? (Width: itemWidth, Height: itemHeight)
                : default)
            .Where(size => ResultImageDimensions.IsValid(size.Width, size.Height))
            .Distinct()
            .ToArray();

        if (sizes.Length == 1)
        {
            width = sizes[0].Width;
            height = sizes[0].Height;
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    private static bool TryReadFrameInfo(MeasureResultImgModel? measureResult, out int width, out int height)
    {
        if (measureResult != null)
            return ResultImageDimensions.TryReadFrameInfo(measureResult.ImgFrameInfo, out width, out height);

        width = 0;
        height = 0;
        return false;
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            if (string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase))
                return true;

            string leftFileName = Path.GetFileName(left);
            string rightFileName = Path.GetFileName(right);
            bool leftIsFileNameOnly = string.Equals(left.Trim(), leftFileName, StringComparison.OrdinalIgnoreCase);
            bool rightIsFileNameOnly = string.Equals(right.Trim(), rightFileName, StringComparison.OrdinalIgnoreCase);
            return (leftIsFileNameOnly || rightIsFileNameOnly)
                && !string.IsNullOrWhiteSpace(leftFileName)
                && string.Equals(leftFileName, rightFileName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
