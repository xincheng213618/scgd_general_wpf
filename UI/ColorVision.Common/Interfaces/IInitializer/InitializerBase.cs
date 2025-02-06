using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.UI
{
    public abstract class InitializerBase : IInitializer
    {
        public virtual string Name => string.Empty;
        public virtual IEnumerable<string> Dependencies => new List<string>();

        public virtual int Order => 1;

        public virtual Task InitializeAsync()
        {
            throw new System.NotImplementedException();
        }
    }
}
