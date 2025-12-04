namespace ProjectStarkSemi.Conoscope
{
    /// <summary>
    /// 导出模式枚举
    /// </summary>
    public enum ExportMode
    {
        /// <summary>
        /// 按角度导出 (0° 到 180°)
        /// </summary>
        Angle,

        /// <summary>
        /// 按同心圆导出 (从中心点到边缘)
        /// VA60: 60个同心圆 (0-60°)
        /// VA80: 80个同心圆 (0-80°)
        /// </summary>
        Circle
    }

    /// <summary>
    /// 导出通道枚举
    /// </summary>
    public enum ExportChannel
    {
        /// <summary>
        /// 红色通道
        /// </summary>
        R,

        /// <summary>
        /// 绿色通道
        /// </summary>
        G,

        /// <summary>
        /// 蓝色通道
        /// </summary>
        B,

        /// <summary>
        /// X通道
        /// </summary>
        X,

        /// <summary>
        /// Y通道
        /// </summary>
        Y,

        /// <summary>
        /// Z通道
        /// </summary>
        Z
    }
}
