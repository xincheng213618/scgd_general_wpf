namespace ColorVision.UI
{

    public class ConfigService
    {
        public static IConfigService Instance { get; private set; }
        public static void SetInstance(IConfigService instance)
        {
            Instance = instance;
        }
    }

}
