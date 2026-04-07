using System;

namespace ColorVision.UI
{
    /// <summary>
    /// 标注在 <see cref="IConfig"/> 类的属性上，使其自动出现在设置窗口中。
    /// <para>
    /// 适用于简单的属性级设置（bool 开关、enum 选择、string 输入等）。
    /// <see cref="ConfigSettingManager"/> 会自动扫描所有 <see cref="IConfig"/> 实现类，
    /// 发现带有此特性的属性后，通过 <see cref="ConfigService"/> 获取持久化单例作为数据源，
    /// 自动生成 <see cref="ConfigSettingMetadata"/>。
    /// </para>
    /// <para>
    /// 对于需要自定义 UserControl 或条件逻辑的复杂设置，请改用 <see cref="IConfigSettingProvider"/> 接口。
    /// </para>
    /// <example>
    /// <code>
    /// public class SearchConfig : ViewModelBase, IConfig
    /// {
    ///     public static SearchConfig Instance => ConfigService.Instance.GetRequiredService&lt;SearchConfig&gt;();
    ///
    ///     [ConfigSetting(Order = 20)]
    ///     [DisplayName("搜索引擎")]
    ///     public SearchEngine SearchEngine { get; set; }
    ///
    ///     [ConfigSetting(Order = 21)]
    ///     public bool EnableBrowserSearch { get; set; }
    /// }
    /// </code>
    /// </example>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ConfigSettingAttribute : Attribute
    {
        /// <summary>
        /// 在设置面板中的排列顺序，数值越小越靠前。默认 999。
        /// </summary>
        public int Order { get; set; } = 999;

        /// <summary>
        /// 设置所属分组（对应设置窗口中的 TabItem）。默认 <see cref="ConfigSettingConstants.Universal"/>。
        /// </summary>
        public string Group { get; set; } = ConfigSettingConstants.Universal;

        public ConfigSettingAttribute() { }
    }
}
