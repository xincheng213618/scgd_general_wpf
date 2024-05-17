namespace ColorVision.UI
{
    //属性继承配置，用于配置属性继承，例如：配置文件中的属性继承
    public interface IConfig
    {

    }

    public interface IConfigSecure : IConfig
    {
        // 加密
        void Encryption();

        // 解密
        void Decrypt();
    }

}
