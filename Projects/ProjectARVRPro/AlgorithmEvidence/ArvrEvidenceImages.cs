using ProjectARVRPro.Process.W51;
using System.Text.RegularExpressions;

namespace ProjectARVRPro.AlgorithmEvidence;

internal static partial class ArvrEvidenceImages
{
    public static IReadOnlyList<ProjectARVRReuslt> Select(IEnumerable<ProjectARVRReuslt> records, bool w51Only) =>
        records.Where(record => !w51Only || IsW51(record)).ToArray();

    public static bool IsW51(ProjectARVRReuslt record) =>
        record.ProcessTypeFullName == typeof(White51Process).FullName || W51Name().IsMatch(record.Model);

    public static IReadOnlyList<ResultImageFileCandidate> GetSources(ProjectARVRReuslt record, Func<string, bool>? exists = null) =>
        ResultImageFileCandidates.GetExisting(record, exists)
            .Where(candidate => candidate.Kind != ResultImageFileKind.SavedResult).ToArray();

    [GeneratedRegex(@"(?:^|[^a-z0-9])(?:white[ _-]?51|w51)(?:$|[^0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex W51Name();
}
