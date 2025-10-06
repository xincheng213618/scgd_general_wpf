using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Common.Utilities
{
    /// <summary>
    /// 密码相关
    /// </summary>
    public static class Cryptography
    {
        public static class RSA
        {

            private static RSACryptoServiceProvider rsa;

            // 生成RSA密钥对
            public static void GenerateKeys()
            {
                rsa = new RSACryptoServiceProvider();
            }

            // 获取公钥（XML格式）
            public static string GetPublicKey()
            {
                return rsa.ToXmlString(false);
            }

            // 获取私钥（XML格式）
            public static string GetPrivateKey()
            {
                return rsa.ToXmlString(true);
            }

            // 使用公钥加密数据
            public static string Encrypt(string publicKey, string data)
            {
                rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(publicKey);

                byte[] plainBytes = Encoding.UTF8.GetBytes(data);
                byte[] encryptedBytes = rsa.Encrypt(plainBytes, false);

                return Convert.ToBase64String(encryptedBytes);
            }

            // 使用私钥解密数据
            public static string Decrypt(string privateKey, string encryptedData)
            {
                rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(privateKey);

                byte[] encryptedBytes = Convert.FromBase64String(encryptedData);
                byte[] decryptedBytes = rsa.Decrypt(encryptedBytes, false);

                return Encoding.UTF8.GetString(decryptedBytes);
            }




            public static string Sign(string Text, string PrivateKey)
            {
                RSACryptoServiceProvider rsa = new();
                rsa.FromXmlString(PrivateKey);
                return Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(Text), "MD5"));
            }
        }


        public static class AES
        {
            public static string Encrypt(string Data, string Key, string Vector)
            {
                byte[] bytes = Encrypt(Encoding.UTF8.GetBytes(Data), Key, Vector);
                return Convert.ToBase64String(bytes);
            }

            public static string Decrypt(string Data, string Key, string Vector)
            {
                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(Data);
                }
                catch
                {
                    bytes = new byte[1];
                }
                bytes = Decrypt(bytes, Key, Vector);
                return Encoding.UTF8.GetString(bytes);
            }

            /// <summary>
            /// AES加密，任意文件
            /// </summary>
            /// <param name="Data">被加密的明文</param>
            /// <param name="Key">密钥</param>
            /// <param name="Vector">密钥向量</param>
            /// <returns>密文</returns>
            public static byte[] Encrypt(byte[] Data, string Key, string Vector)
            {
                byte[] bKey = new byte[32];//采用32位密码加密
                Array.Copy(Encoding.UTF8.GetBytes(Key.PadRight(bKey.Length)), bKey, bKey.Length);//如果用户输入的密码不足32位，自动填充空格至32位
                byte[] bVector = new byte[16];//密钥向量，为16位
                Array.Copy(Encoding.UTF8.GetBytes(Vector.PadRight(bVector.Length)), bVector, bVector.Length);//如果用户定义的密钥向量不足16位，自动填充空格至16位
                byte[] Cryptograph = null;//加密后的密文
                Aes Ae = Aes.Create();
                try
                {
                    using (MemoryStream Memory = new())
                    {
                        //把内存流对象包装成加密流对象
                        using (CryptoStream Encryptor = new(Memory, Ae.CreateEncryptor(bKey, bVector), CryptoStreamMode.Write))
                        {
                            Encryptor.Write(Data, 0, Data.Length);
                            Encryptor.FlushFinalBlock();
                            Cryptograph = Memory.ToArray();
                        }
                    }
                    return Cryptograph;
                }
                catch
                {
                    return Array.Empty<byte>();
                }

            }

            /// <summary>
            /// AES解密，任意文件
            /// </summary>
            /// <param name="Data">被解密的密文</param>
            /// <param name="Key">密钥</param>
            /// <param name="Vector">密钥向量</param>
            /// <returns>明文</returns>
            public static byte[] Decrypt(byte[] Data, string Key, string Vector)
            {
                byte[] bKey = new byte[32];
                Array.Copy(Encoding.UTF8.GetBytes(Key.PadRight(bKey.Length)), bKey, bKey.Length);
                byte[] bVector = new byte[16];
                Array.Copy(Encoding.UTF8.GetBytes(Vector.PadRight(bVector.Length)), bVector, bVector.Length);
                byte[] original = null;//解密后的明文
                Aes Ae = Aes.Create();
                try
                {
                    using (MemoryStream Memory = new(Data))
                    {
                        //把内存流对象包装成加密对象
                        using (CryptoStream Decrypt = new(Memory, Ae.CreateDecryptor(bKey, bVector), CryptoStreamMode.Read))
                        {
                            //明文存储区
                            using (MemoryStream originalMemory = new())
                            {
                                byte[] Buffer = new byte[1024];
                                int readBytes = 0;
                                while ((readBytes = Decrypt.Read(Buffer, 0, Buffer.Length)) > 0)
                                {
                                    originalMemory.Write(Buffer, 0, readBytes);
                                }
                                original = originalMemory.ToArray();
                            }
                        }
                    }
                    return original;
                }
                catch
                {
                    return Array.Empty<byte>();
                }
            }
        }

        public static string AESEncrypt(string Data, string Key, string Vector) => AES.Encrypt(Data, Key, Vector);
        public static string AESDecrypt(string Data, string Key, string Vector) => AES.Decrypt(Data, Key, Vector);




        /// <summary>
        /// 生成字符串的MD5码
        /// </summary>
        public static string GetMd5Hash(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = MD5.HashData(inputBytes);
            StringBuilder sb = new();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public static string GetSha256Hash(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(inputBytes);

            StringBuilder sb = new();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }

            return sb.ToString();
        }

    }
}
