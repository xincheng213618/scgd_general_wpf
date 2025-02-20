namespace ColorVision.UI
{
    public interface IConfigSecure : IConfig
    {
        // 加密  
        void Encryption();

        // 解密
        void Decrypt();
    }

}
