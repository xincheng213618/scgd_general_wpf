namespace ColorVision.UI
{
    public interface IConfigService
    {
        public T1 GetRequiredService<T1>() where T1 : IConfig;
        public void SaveConfigs();
        public void LoadConfigs();

    }

}
