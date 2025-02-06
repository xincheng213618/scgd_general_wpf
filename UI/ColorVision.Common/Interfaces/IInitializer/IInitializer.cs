using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.UI
{

    public interface IInitializer
    {
        public string Name { get; }
        IEnumerable<string> Dependencies { get; }
        public int Order { get; }
        Task InitializeAsync();
    }
}
