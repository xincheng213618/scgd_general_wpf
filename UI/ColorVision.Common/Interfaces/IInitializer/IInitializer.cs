using System.Threading.Tasks;

namespace ColorVision.UI
{

    public interface IInitializer
    {
        public string Name { get; }
        public int Order { get; }
        Task InitializeAsync();
    }
}
