using System;

namespace ColorVision.ImageEditor.EditorTools.Histogram
{
    /// <summary>
    /// Represents a point on the tone curve for histogram editing
    /// </summary>
    public class CurvePoint : IComparable<CurvePoint>
    {
        /// <summary>
        /// Input value (0-255)
        /// </summary>
        public int Input { get; set; }

        /// <summary>
        /// Output value (0-255)
        /// </summary>
        public int Output { get; set; }

        public CurvePoint(int input, int output)
        {
            Input = Math.Clamp(input, 0, 255);
            Output = Math.Clamp(output, 0, 255);
        }

        public int CompareTo(CurvePoint? other)
        {
            if (other == null) return 1;
            return Input.CompareTo(other.Input);
        }

        public override bool Equals(object? obj)
        {
            if (obj is CurvePoint other)
            {
                return Input == other.Input && Output == other.Output;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Input, Output);
        }
    }
}
