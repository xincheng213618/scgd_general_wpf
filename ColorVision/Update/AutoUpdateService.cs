using ColorVision.UI;
using System.Threading.Tasks;

namespace ColorVision.Update
{
    public class AutoUpdateService : MainWindowInitializedBase
    {
        public override Task Initialize() => CombinedUpdateCoordinator.CheckForUpdatesOnStartupAsync();
    }
}
