using ColorVision.UI;

namespace Conoscope.Core
{

    public sealed class ConoscopeManager
    {
        public static ConoscopeManager Instance { get; } = new();

        public ConoscopeConfig Config { get; }
        public ConoscopeGlobalReferenceStore GlobalReferences { get; }

        private ConoscopeManager()
        {
            Config = ConfigService.Instance.GetRequiredService<ConoscopeConfig>();
            Config.NormalizeAfterLoad();
            GlobalReferences = new ConoscopeGlobalReferenceStore(Config);
        }

    }
}
