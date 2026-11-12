using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI
{
    /// <summary>
    /// Optional asynchronous provider. Called on the search UI context; implementations may move
    /// thread-safe I/O to a worker, but must marshal UI-owned state back to its owning dispatcher.
    /// Implementing this alongside IDynamicSearchProvider makes the async path take precedence.
    /// </summary>
    public interface IAsyncSearchProvider
    {
        Task<IReadOnlyList<ISearch>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
    }
}
