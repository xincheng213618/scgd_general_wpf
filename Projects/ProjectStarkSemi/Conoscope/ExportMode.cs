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
        Z,

        /// <summary>
        /// CIE 1931 x色度坐标
        /// </summary>
        CieX,

        /// <summary>
        /// CIE 1931 y色度坐标
        /// </summary>
        CieY,

        /// <summary>
        /// CIE 1976 u色度坐标
        /// </summary>
        CieU,

        /// <summary>
        /// CIE 1976 v色度坐标
        /// </summary>
        CieV
    }
}
