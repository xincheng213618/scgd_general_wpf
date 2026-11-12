using System.Collections.Generic;

namespace ColorVision.Database;

/// <summary>Lets a query publish its ordered rows to a list in one batch.</summary>
public interface IQueryResultList<T> : IList<T>
{
    void AddRange(IEnumerable<T> rows);
}
