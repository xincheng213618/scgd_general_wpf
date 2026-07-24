using System.Threading.Tasks;

namespace ColorVision.UI
{
    public abstract class InitializerBase : IInitializer
    {
        public virtual string Name => GetType().Name;

        public virtual int Order => 1;

        public abstract Task InitializeAsync();
    }
}
