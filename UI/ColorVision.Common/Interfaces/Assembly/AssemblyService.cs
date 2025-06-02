namespace ColorVision.UI
{
    public class AssemblyService
    {
        public static IAssemblyService Instance {  get; private set; }
        public static void SetInstance(IAssemblyService instance)
        {
            Instance = instance;
        }
    }


}
