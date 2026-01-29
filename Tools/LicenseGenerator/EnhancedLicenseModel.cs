using Newtonsoft.Json;

namespace LicenseGenerator
{
    /// <summary>
    /// 增强型许可证模型 - 包含设备信息、有效期等扩展字段
    /// 与 ColorVision.Engine.Services.PhyCameras.Dao.ColorVisionLicense 兼容
    /// </summary>
    public class EnhancedLicenseModel
    {
        /// <summary>
        /// 授权机构签名
        /// </summary>
        [JsonProperty("authority_signature")]
        public string AuthoritySignature { get; set; } = string.Empty;

        /// <summary>
        /// 设备型号
        /// </summary>
        [JsonProperty("device_mode")]
        public string DeviceMode { get; set; } = string.Empty;

        /// <summary>
        /// 过期日期（Unix时间戳，秒）
        /// </summary>
        [JsonProperty("expiry_date")]
        public string ExpiryDate { get; set; } = string.Empty;

        /// <summary>
        /// 过期日期的DateTime表示
        /// </summary>
        [JsonIgnore]
        public DateTime ExpiryDateTime
        {
            get
            {
                if (string.IsNullOrEmpty(ExpiryDate))
                    return DateTime.Now.AddYears(1);
                
                try
                {
                    long timestamp = long.Parse(ExpiryDate);
                    return DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                }
                catch
                {
                    return DateTime.Now.AddYears(1);
                }
            }
            set
            {
                DateTimeOffset offset = new DateTimeOffset(value);
                ExpiryDate = offset.ToUnixTimeSeconds().ToString();
            }
        }

        /// <summary>
        /// 签发日期（Unix时间戳，秒）
        /// </summary>
        [JsonProperty("issue_date")]
        public string IssueDate { get; set; } = string.Empty;

        /// <summary>
        /// 签发日期的DateTime表示
        /// </summary>
        [JsonIgnore]
        public DateTime IssueDateDateTime
        {
            get
            {
                if (string.IsNullOrEmpty(IssueDate))
                    return DateTime.Now;
                
                try
                {
                    long timestamp = long.Parse(IssueDate);
                    return DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                }
                catch
                {
                    return DateTime.Now;
                }
            }
            set
            {
                DateTimeOffset offset = new DateTimeOffset(value);
                IssueDate = offset.ToUnixTimeSeconds().ToString();
            }
        }

        /// <summary>
        /// 签发机构
        /// </summary>
        [JsonProperty("issuing_authority")]
        public string IssuingAuthority { get; set; } = "ColorVision";

        /// <summary>
        /// 被许可人（客户名称）
        /// </summary>
        [JsonProperty("licensee")]
        public string Licensee { get; set; } = string.Empty;

        /// <summary>
        /// 被许可人签名（机器码）
        /// </summary>
        [JsonProperty("licensee_signature")]
        public string LicenseeSignature { get; set; } = string.Empty;

        /// <summary>
        /// 创建默认的增强许可证
        /// </summary>
        public EnhancedLicenseModel()
        {
            IssueDateDateTime = DateTime.Now;
            ExpiryDateTime = DateTime.Now.AddYears(1);
        }

        /// <summary>
        /// 验证许可证是否已过期
        /// </summary>
        /// <returns>如果已过期返回true</returns>
        public bool IsExpired()
        {
            return ExpiryDateTime < DateTime.Now;
        }

        /// <summary>
        /// 获取剩余有效天数
        /// </summary>
        /// <returns>剩余天数</returns>
        public int GetRemainingDays()
        {
            TimeSpan remaining = ExpiryDateTime - DateTime.Now;
            return Math.Max(0, (int)remaining.TotalDays);
        }
    }
}
