using System.Threading.Tasks;

namespace ColorVision.UI
{

    public interface IInitializer
    {
        public int Order { get; }
        Task InitializeAsync();
    }
}
