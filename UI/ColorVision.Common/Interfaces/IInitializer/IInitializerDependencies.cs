using System.Collections.Generic;

namespace ColorVision.UI;

/// <summary>
/// Opts into dependency-ordered startup. Names describe ordering, not successful
/// connectivity: offline initializers may complete normally. Legacy initializers
/// remain serial barriers. An explicitly skipped dependency is not re-enabled.
/// </summary>
public interface IInitializerDependencies
{
    IReadOnlyCollection<string> Dependencies { get; }
}
