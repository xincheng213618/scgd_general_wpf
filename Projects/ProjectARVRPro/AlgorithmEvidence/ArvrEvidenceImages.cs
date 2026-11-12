using ProjectARVRPro.Process.W51;
using ProjectARVRPro.Process.OpticCenter;
using ProjectARVRPro.Process.Distortion;
using System.Text.RegularExpressions;

namespace ProjectARVRPro.AlgorithmEvidence;

internal enum EvidenceCategory { LuminousArea, RgbCross, Distortion }

internal static partial class ArvrEvidenceImages
{
    public static IReadOnlyList<ProjectARVRReuslt> Select(IEnumerable<ProjectARVRReuslt> records, bool matchingOnly, EvidenceCategory category = EvidenceCategory.LuminousArea) =>
        records.Where(record => !matchingOnly || Matches(record, category)).ToArray();

    public static bool Matches(ProjectARVRReuslt record, EvidenceCategory category) => category switch
    {
        EvidenceCategory.RgbCross => record.ProcessTypeFullName == typeof(RgbCrossProcess).FullName || CrossName().IsMatch(record.Model),
        EvidenceCategory.Distortion => record.ProcessTypeFullName == typeof(DistortionProcess).FullName
            || record.ProcessTypeFullName == typeof(DistortionDynamicProcess).FullName || DistortionName().IsMatch(record.Model),
        _ => IsW51(record)
    };

    public static bool IsW51(ProjectARVRReuslt record) =>
        record.ProcessTypeFullName == typeof(White51Process).FullName || W51Name().IsMatch(record.Model);

    public static IReadOnlyList<ResultImageFileCandidate> GetSources(ProjectARVRReuslt record, Func<string, bool>? exists = null) =>
        ResultImageFileCandidates.GetExisting(record, exists)
            .Where(candidate => candidate.Kind != ResultImageFileKind.SavedResult).ToArray();

    [GeneratedRegex(@"(?:^|[^a-z0-9])(?:white[ _-]?51|w51)(?:$|[^0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex W51Name();

    [GeneratedRegex(@"(?:^|[^a-z0-9])(?:optic[ _-]?center|rgb[ _-]?cross|find[ _-]?cross)(?:$|[^a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CrossName();

    [GeneratedRegex(@"(?:^|[^a-z0-9])distortion(?:$|[^a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DistortionName();
}
