using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.EditorTools.Histogram
{
    /// <summary>
    /// Represents a tone curve for histogram editing with support for interpolation
    /// </summary>
    public class ToneCurve
    {
        private readonly List<CurvePoint> _points;
        private readonly int[] _lut; // Lookup table for fast access

        public IReadOnlyList<CurvePoint> Points => _points.AsReadOnly();

        public ToneCurve()
        {
            _points = new List<CurvePoint>
            {
                new CurvePoint(0, 0),     // Black point
                new CurvePoint(255, 255)  // White point
            };
            _lut = new int[256];
            UpdateLUT();
        }

        /// <summary>
        /// Add or update a curve point
        /// </summary>
        public void AddOrUpdatePoint(int input, int output)
        {
            input = Math.Clamp(input, 0, 255);
            output = Math.Clamp(output, 0, 255);

            // Don't allow moving the black and white points horizontally
            if (input == 0 || input == 255)
            {
                var existingPoint = _points.FirstOrDefault(p => p.Input == input);
                if (existingPoint != null)
                {
                    existingPoint.Output = output;
                }
            }
            else
            {
                var existingPoint = _points.FirstOrDefault(p => p.Input == input);
                if (existingPoint != null)
                {
                    existingPoint.Output = output;
                }
                else
                {
                    _points.Add(new CurvePoint(input, output));
                    _points.Sort();
                }
            }

            UpdateLUT();
        }

        /// <summary>
        /// Remove a curve point (except black and white points)
        /// </summary>
        public void RemovePoint(int input)
        {
            if (input == 0 || input == 255) return; // Can't remove endpoints

            _points.RemoveAll(p => p.Input == input);
            UpdateLUT();
        }

        /// <summary>
        /// Find the closest point to the given input value
        /// </summary>
        public CurvePoint? FindClosestPoint(int input, int threshold = 10)
        {
            CurvePoint? closest = null;
            int minDistance = int.MaxValue;

            foreach (var point in _points)
            {
                int distance = Math.Abs(point.Input - input);
                if (distance < minDistance && distance <= threshold)
                {
                    minDistance = distance;
                    closest = point;
                }
            }

            return closest;
        }

        /// <summary>
        /// Get the output value for a given input using the lookup table
        /// </summary>
        public int GetOutput(int input)
        {
            input = Math.Clamp(input, 0, 255);
            return _lut[input];
        }

        /// <summary>
        /// Get the entire lookup table
        /// </summary>
        public int[] GetLUT()
        {
            return (int[])_lut.Clone();
        }

        /// <summary>
        /// Reset the curve to linear (identity mapping)
        /// </summary>
        public void Reset()
        {
            _points.Clear();
            _points.Add(new CurvePoint(0, 0));
            _points.Add(new CurvePoint(255, 255));
            UpdateLUT();
        }

        /// <summary>
        /// Update the lookup table based on current curve points using linear interpolation
        /// </summary>
        private void UpdateLUT()
        {
            for (int i = 0; i < 256; i++)
            {
                _lut[i] = InterpolateOutput(i);
            }
        }

        /// <summary>
        /// Interpolate output value for a given input using linear interpolation
        /// </summary>
        private int InterpolateOutput(int input)
        {
            // Find the two points that bracket this input value
            CurvePoint? lower = null;
            CurvePoint? upper = null;

            foreach (var point in _points)
            {
                if (point.Input <= input)
                {
                    lower = point;
                }
                else if (point.Input > input)
                {
                    upper = point;
                    break;
                }
            }

            // If we have both points, interpolate
            if (lower != null && upper != null)
            {
                // Prevent division by zero
                if (upper.Input == lower.Input)
                {
                    return lower.Output;
                }
                
                double t = (double)(input - lower.Input) / (upper.Input - lower.Input);
                return (int)Math.Round(lower.Output + t * (upper.Output - lower.Output));
            }

            // If only lower exists (at the end)
            if (lower != null)
            {
                return lower.Output;
            }

            // If only upper exists (at the beginning)
            if (upper != null)
            {
                return upper.Output;
            }

            // Fallback (shouldn't happen with properly initialized curve)
            return input;
        }
    }
}
