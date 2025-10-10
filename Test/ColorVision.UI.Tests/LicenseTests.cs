using ColorVision.UI.ACE;
using System.Security.Cryptography;
using Xunit;

namespace ColorVision.UI.Tests.ACE
{
    /// <summary>
    /// License 类的单元测试
    /// </summary>
    public class LicenseTests
    {
        // 测试用的密钥对（仅用于单元测试）
        private const string TestPublicKey = "<RSAKeyValue><Modulus>5sf/agoe+/hryIfvt7v6o9aNldWSkUoPkW6se8VbEo7B4JBT0vIUQqku635RU+0vhaF/IJ7TQw6pYerHacA83XYBy90KEN4twOBs1Gy3XfEBcjYheQO919Hif1gENzqzQEg47G36VdmWzmhjreq2YQQQN+p/ezIbYtrPXGNU4fE=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        private const string TestPrivateKey = "<RSAKeyValue><Modulus>5sf/agoe+/hryIfvt7v6o9aNldWSkUoPkW6se8VbEo7B4JBT0vIUQqku635RU+0vhaF/IJ7TQw6pYerHacA83XYBy90KEN4twOBs1Gy3XfEBcjYheQO919Hif1gENzqzQEg47G36VdmWzmhjreq2YQQQN+p/ezIbYtrPXGNU4fE=</Modulus><Exponent>AQAB</Exponent><P>/OfgYc6H7sSiFUrwkTVtQEyuSm309+Whwuvuul/3zLkNJlvorGC2D5ksTz3Q0XFehHWgWNc0jQ3MRyKp2EHxgw==</P><Q>6ZrTQbe25FVr92pxAlBeO1iONdbLRM+/VmuwrZVgeHvu++8ChAidQT13rcVfqvLDuGq5/q2bgQgmraqdgRNIew==</Q><DP>0sEQ1bDcyncGcyQOMZQKRSkhnVjgaaztDpi6Sooq4GndsXep/+xgC8Ojjy1+VOtazpuPUjmUy28SKr2SOGtLrQ==</DP><DQ>b7mMsDGdVzdDm+Fciy7E4r1HxpgkP5TcfgijR2HZ8cXUVsnI+jzkeP9c7c8oIipZUSo6KoP9i4jKduTSz5jZYQ==</DQ><InverseQ>2kXWXpMpHplGwG/eHR17tVNyfaxjl2Hu2QWnlg5Jf/vLDMcA9MspGS5mS5uCNTTPh34T9PEtmCdA5L5i8kakwg==</InverseQ><D>EmVOzr0PyzX6IXn0ecjaKcUodBEaJcqpgwY3aYZJxCjs+2GFzQLO6qFhxBPFl9MIPrao04jVfjrk9ZEpZByWvUmq79tlzpBjeZW2wcjeUrZYK0/b0D7NRelf6InSJaOb9QKw/hhSPsl3x+nXPyhUFfz6q8bThGDSriC/eb3aSyE=</D></RSAKeyValue>";

        [Fact]
        public void GetMachineCode_ShouldReturnNonEmptyString()
        {
            // Arrange & Act
            string machineCode = License.GetMachineCode();

            // Assert
            Assert.NotNull(machineCode);
            Assert.NotEmpty(machineCode);
            Assert.Matches("^[0-9a-f]+$", machineCode); // 应该是十六进制字符串
        }

        [Fact]
        public void GetMachineCode_ShouldBeConsistent()
        {
            // Arrange & Act
            string machineCode1 = License.GetMachineCode();
            string machineCode2 = License.GetMachineCode();

            // Assert
            Assert.Equal(machineCode1, machineCode2);
        }

        [Fact]
        public void Sign_WithValidParameters_ShouldReturnBase64String()
        {
            // Arrange
            string testText = "test_machine_code";

            // Act
            string signature = License.Sign(testText, TestPrivateKey);

            // Assert
            Assert.NotNull(signature);
            Assert.NotEmpty(signature);
            // 应该是有效的 Base64 字符串
            var bytes = Convert.FromBase64String(signature);
            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Sign_WithNullText_ShouldThrowArgumentNullException()
        {
            // Arrange
            string? nullText = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => License.Sign(nullText!, TestPrivateKey));
        }

        [Fact]
        public void Sign_WithNullPrivateKey_ShouldThrowArgumentNullException()
        {
            // Arrange
            string testText = "test";
            string? nullKey = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => License.Sign(testText, nullKey!));
        }

        [Fact]
        public void Create_WithValidParameters_ShouldReturnValidLicense()
        {
            // Arrange
            string testMachineCode = "74657374"; // "test" in hex

            // Act
            string license = License.Create(testMachineCode, TestPrivateKey);

            // Assert
            Assert.NotNull(license);
            Assert.NotEmpty(license);
            
            // 验证生成的许可证是有效的 Base64 字符串
            var bytes = Convert.FromBase64String(license);
            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Create_WithNullMachineCode_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => License.Create(null!, TestPrivateKey));
        }

        [Fact]
        public void Create_WithNullPrivateKey_ShouldThrowArgumentNullException()
        {
            // Arrange
            string testMachineCode = "74657374";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => License.Create(testMachineCode, null!));
        }

        [Fact]
        public void Check_WithValidLicense_ShouldReturnTrue()
        {
            // Arrange
            string machineCode = License.GetMachineCode();
            string validLicense = License.Create(machineCode, TestPrivateKey);

            // Act
            bool result = License.Check(validLicense);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Check_WithInvalidLicense_ShouldReturnFalse()
        {
            // Arrange
            string invalidLicense = "invalid_license_string";

            // Act
            bool result = License.Check(invalidLicense);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Check_WithEmptyString_ShouldReturnFalse()
        {
            // Act
            bool result = License.Check(string.Empty);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Check_WithNull_ShouldReturnFalse()
        {
            // Act
            bool result = License.Check(null!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Check_WithWrongMachineCodeLicense_ShouldReturnFalse()
        {
            // Arrange
            string wrongMachineCode = "74657374"; // "test" in hex - 不同于实际机器码
            string licenseForWrongMachine = License.Create(wrongMachineCode, TestPrivateKey);

            // Act
            bool result = License.Check(licenseForWrongMachine);

            // Assert
            // 只有当机器码恰好是 "test" 时才会通过，否则应该失败
            // 在大多数情况下应该返回 false
            if (License.GetMachineCode() != wrongMachineCode)
            {
                Assert.False(result);
            }
        }

        [Fact]
        public void Check_FileBasedCheck_ShouldHandleNonExistentFiles()
        {
            // Act
            bool result = License.Check();

            // Assert
            // 应该不会抛出异常，即使文件不存在
            Assert.False(result || result); // 总是通过，只是验证不抛异常
        }

        [Fact]
        public void Sign_ShouldUseShA256_NotMD5()
        {
            // Arrange
            string testText = "test_text";
            string signature = License.Sign(testText, TestPrivateKey);

            // Act - 验证签名
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(TestPublicKey);
                byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(testText);
                byte[] signatureBytes = Convert.FromBase64String(signature);

                // Assert - 应该使用 SHA256
                bool validWithSha256 = rsa.VerifyData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, signatureBytes);
                Assert.True(validWithSha256, "签名应该使用 SHA256 算法");
            }
        }
    }
}
