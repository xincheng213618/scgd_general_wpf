using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.Algorithms;

/// <summary>Shared ImageView categories for catalog projections and dedicated analysis tools.</summary>
public static class AlgorithmMenuGroups
{
    public static readonly AlgorithmInteractiveGroupPresentation Tone = new("AlgorithmTone", 1, "灰度与色彩", "Algorithm_GroupTone");
    public static readonly AlgorithmInteractiveGroupPresentation Filters = new("AlgorithmFilters", 2, "滤波与增强", "Algorithm_FilterCategory");
    public static readonly AlgorithmInteractiveGroupPresentation Morphology = new("AlgorithmMorphology", 3, "阈值与形态学", "Algorithm_GroupMorphology");
    public static readonly AlgorithmInteractiveGroupPresentation Correction = new("AlgorithmCorrection", 4, "几何变换与校正", "Algorithm_GroupCorrection");

    public static readonly AlgorithmInteractiveGroupPresentation Localization = new("AnalysisLocalization", 1, "定位与几何", "Algorithm_GroupLocalization");
    public static readonly AlgorithmInteractiveGroupPresentation FieldGeometry = new("AnalysisFieldGeometry", 2, "视场与畸变", "Algorithm_GroupFieldGeometry");
    public static readonly AlgorithmInteractiveGroupPresentation ImageQuality = new("AnalysisImageQuality", 3, "清晰度与频域", "Algorithm_GroupImageQuality");
    public static readonly AlgorithmInteractiveGroupPresentation ColorRegistration = new("AnalysisColorRegistration", 4, "色彩与套色", "Algorithm_GroupColorRegistration");
    public static readonly AlgorithmInteractiveGroupPresentation Defects = new("AnalysisDefects", 5, "缺陷与鬼影", "Algorithm_GroupDefects");
    public static readonly AlgorithmInteractiveGroupPresentation Statistics = new("AnalysisStatistics", 6, "灰度与统计", "Algorithm_GroupStatistics");
    public static readonly AlgorithmInteractiveGroupPresentation Stereo = new("AnalysisStereo", 7, "双目与视区", "Algorithm_GroupStereo");

    // Dedicated native tools also contribute to these groups, outside the catalog projection.
    public static IReadOnlyList<AlgorithmInteractiveGroupPresentation> Analysis { get; } =
        Array.AsReadOnly(new[] { Localization, FieldGeometry, ImageQuality, ColorRegistration, Defects, Statistics, Stereo });

    public static string GetOwnerGuid(AlgorithmInteractiveGroupPresentation group) =>
        Analysis.Any(item => string.Equals(item.Id, group.Id, StringComparison.OrdinalIgnoreCase)) ? "AlgorithmsCall" : "Algorithms";
}
