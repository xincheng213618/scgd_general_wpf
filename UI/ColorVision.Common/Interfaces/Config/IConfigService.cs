namespace ColorVision.UI
{
    public interface IConfigService
    {
        T1 GetRequiredService<T1>() where T1 : IConfig;
        void SaveConfigs();
        void LoadConfigs();

    }


}
