namespace Conoscope.Core
{
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
        CieV,

        /// <summary>
        /// CIE 1976 uv 色差
        /// </summary>
        ColorDifference,

        /// <summary>
        /// 白/黑对比度
        /// </summary>
        Contrast
    }

    public enum ColorDifferenceReferenceMode
    {
        D65,
        D50,
        A,
        D75,
        ImageCenter,
        Custom,
        ReferenceImage
    }

    public enum ContrastReferenceKind
    {
        Black,
        White
    }
}
