namespace Conoscope.Core
{
    /// <summary>
    /// RGB采样点数据
    /// </summary>
    public class RgbSample
    {

        public double Position { get; set; }

        public double DX { get; set; }
        public double DY { get; set; }


        /// <summary>
        /// X通道值
        /// </summary>
        public double X { get; set; }


        /// <summary>
        /// Y通道值
        /// </summary>
        public double Y { get; set; }


        /// <summary>
        /// Z通道值
        /// </summary>
        public double Z { get; set; }
    }
}
