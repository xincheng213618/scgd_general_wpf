using System;
using System.Windows;

namespace ColorVision.Guidance;

internal sealed record NewUserGuideStep(
    string Title,
    string Description,
    Func<FrameworkElement?> TargetResolver);
