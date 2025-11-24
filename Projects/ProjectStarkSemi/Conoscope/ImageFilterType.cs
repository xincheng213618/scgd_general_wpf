namespace ProjectStarkSemi.Conoscope
{
    /// <summary>
    /// 图像滤波类型枚举
    /// </summary>
    public enum ImageFilterType
    {
        /// <summary>
        /// 无滤波
        /// </summary>
        None,
        
        /// <summary>
        /// 低通滤波（均值滤波）
        /// </summary>
        LowPass,
        
        /// <summary>
        /// 移动平均滤波（方框滤波）
        /// </summary>
        MovingAverage,
        
        /// <summary>
        /// 高斯滤波
        /// </summary>
        Gaussian,
        
        /// <summary>
        /// 中值滤波
        /// </summary>
        Median,
        
        /// <summary>
        /// 双边滤波
        /// </summary>
        Bilateral
    }
}
