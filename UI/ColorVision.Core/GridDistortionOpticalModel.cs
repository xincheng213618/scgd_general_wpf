using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Core
{
    internal static class GridDistortionOpticalModel
    {
        private const int ParameterCount = 7;
        // Acceptance limits describe model agreement, not metrological accuracy.
        private const double MaximumRmsPitchFraction = 0.01;
        private const double MaximumPointPitchFraction = 0.03;

        public static GridDistortionOpticalEstimate Calculate(GridDistortionPoint[,] grid, int rows, int cols, GridDistortionPoint center)
        {
            var estimate = new GridDistortionOpticalEstimate { Origin = new(center.X, center.Y) };
            int mr = rows / 2, mc = cols / 2, extent = Math.Max(mr, mc), count = rows * cols;
            double scale = 0, minimumPitch = double.PositiveInfinity;
            foreach (GridDistortionPoint point in grid) scale = Math.Max(scale, Length(point.X - center.X, point.Y - center.Y));
            if (!double.IsFinite(scale) || scale <= 1e-9) return Fail(estimate, "点阵参考尺度退化。");
            var coordinates = new double[count, 2];
            var observed = new double[count * 2];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int i = row * cols + col;
                    GridDistortionPoint point = grid[row, col];
                    coordinates[i, 0] = (double)(col - mc) / extent;
                    coordinates[i, 1] = (double)(row - mr) / extent;
                    observed[2 * i] = (point.X - center.X) / scale;
                    observed[2 * i + 1] = (point.Y - center.Y) / scale;
                    if (row > 0) minimumPitch = Math.Min(minimumPitch, Length(point.X - grid[row - 1, col].X, point.Y - grid[row - 1, col].Y));
                    if (col > 0) minimumPitch = Math.Min(minimumPitch, Length(point.X - grid[row, col - 1].X, point.Y - grid[row, col - 1].Y));
                }
            }
            if (!double.IsFinite(minimumPitch) || minimumPitch <= 1e-9)
                return Fail(estimate, "相邻点距退化，无法验证光学模型。");
            double factor = extent / (2 * scale);
            double[] parameters =
            [
                (grid[mr, mc + 1].X - grid[mr, mc - 1].X) * factor,
                (grid[mr + 1, mc].X - grid[mr - 1, mc].X) * factor,
                (grid[mr, mc + 1].Y - grid[mr, mc - 1].Y) * factor,
                (grid[mr + 1, mc].Y - grid[mr - 1, mc].Y) * factor, 0, 0, 0
            ];
            if (!Fit(coordinates, observed, parameters, out int iterations))
                return Fail(estimate with { FitIterations = iterations }, "投影径向模型未收敛或参数不可辨识。");
            Predict(coordinates, parameters, out double[] predicted);
            double sumSquared = 0, maximum = 0;
            for (int i = 0; i < count; i++)
            {
                double residual = Length(observed[2 * i] - predicted[2 * i], observed[2 * i + 1] - predicted[2 * i + 1]) * scale;
                sumSquared += residual * residual;
                maximum = Math.Max(maximum, residual);
            }
            double rms = Math.Sqrt(sumSquared / count);
            estimate = estimate with
            {
                ColumnPitch = new(parameters[0] * scale / extent, parameters[2] * scale / extent),
                RowPitch = new(parameters[1] * scale / extent, parameters[3] * scale / extent),
                RadialCoefficientPerPixelSquared = parameters[6] / (scale * scale),
                FitRmsPixels = rms, MaxResidualPixels = maximum, FitResidualFraction = rms / minimumPitch, FitIterations = iterations
            };
            if (minimumPitch <= 1e-9 || rms > MaximumRmsPitchFraction * minimumPitch || maximum > MaximumPointPitchFraction * minimumPitch)
                return Fail(estimate, "点阵与居中一阶径向模型不符（RMS 超过最小点距的 1% 或单点残差超过 3%）；请检查定位、光轴偏心、切向或高阶畸变。");

            var samples = new List<GridDistortionOpticalSample>(count - 1);
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (row == mr && col == mc) continue;
                    Reference(coordinates[row * cols + col, 0], coordinates[row * cols + col, 1], parameters, out double x, out double y);
                    double reference = Length(x, y) * scale;
                    double actual = Length(grid[row, col].X - center.X, grid[row, col].Y - center.Y);
                    if (reference <= 1e-9 || !double.IsFinite(reference)) return Fail(estimate, "拟合参考半径退化。");
                    double ratio = 100 * (actual - reference) / reference;
                    samples.Add(new GridDistortionOpticalSample
                    {
                        PointId = grid[row, col].Id, ActualRadiusPixels = actual, ReferenceRadiusPixels = reference,
                        RadialPercent = Math.Abs(ratio) < 1e-9 ? 0 : ratio,
                        ReferencePosition = new(center.X + x * scale, center.Y + y * scale)
                    });
                }
            }
            GridDistortionOpticalSample worst = samples.OrderByDescending(sample => Math.Abs(sample.RadialPercent)).ThenBy(sample => sample.PointId).First();
            return estimate with
            {
                IsAvailable = true, OpticRatioPercent = worst.RadialPercent,
                MaxAbsoluteRatioPercent = Math.Abs(worst.RadialPercent), MaxErrorPointId = worst.PointId, Samples = samples
            };
        }

        private static GridDistortionOpticalEstimate Fail(GridDistortionOpticalEstimate estimate, string reason) =>
            estimate with { Warnings = estimate.Warnings.Append(reason).ToArray() };

        // H is centred at the measured middle dot. Its local derivative supplies
        // the undistorted pitch; the observed central neighbours only initialise it.
        private static void Reference(double x, double y, double[] p, out double u, out double v)
        {
            double denominator = 1 + p[4] * x + p[5] * y;
            u = (p[0] * x + p[1] * y) / denominator;
            v = (p[2] * x + p[3] * y) / denominator;
        }

        private static bool Predict(double[,] coordinates, double[] p, out double[] predicted)
        {
            predicted = new double[coordinates.GetLength(0) * 2];
            double determinant = p[0] * p[3] - p[1] * p[2];
            if (!double.IsFinite(determinant) || Math.Abs(determinant) < 1e-10) return false;
            for (int i = 0; i < coordinates.GetLength(0); i++)
            {
                double x = coordinates[i, 0], y = coordinates[i, 1];
                if (1 + p[4] * x + p[5] * y < 0.2) return false;
                Reference(x, y, p, out double u, out double v);
                double radial = p[6] * (u * u + v * v);
                // Reject folded/non-monotone radial maps over the sampled field.
                if (!double.IsFinite(radial) || 1 + radial < 0.2 || 1 + 3 * radial < 0.1) return false;
                predicted[2 * i] = u * (1 + radial);
                predicted[2 * i + 1] = v * (1 + radial);
            }
            return true;
        }

        private static bool Fit(double[,] coordinates, double[] observed, double[] p, out int iterations)
        {
            double damping = 1e-3;
            bool converged = false;
            iterations = 0;
            for (; iterations < 100; iterations++)
            {
                if (!Predict(coordinates, p, out double[] predicted)) return false;
                double cost = Cost(observed, predicted);
                if (cost < 1e-24) { converged = true; break; }
                if (!NormalEquations(coordinates, observed, p, predicted, out double[,] normal, out double[] gradient)) return false;
                for (int j = 0; j < ParameterCount; j++) normal[j, j] += damping * Math.Max(normal[j, j], 1e-8);
                if (!Solve(normal, gradient, out double[] step)) return false;
                double[] trial = p.Zip(step, (value, delta) => value + delta).ToArray();
                if (Predict(coordinates, trial, out double[] candidate) && Cost(observed, candidate) < cost)
                {
                    Array.Copy(trial, p, ParameterCount);
                    damping = Math.Max(damping / 3, 1e-12);
                    if (step.Max(Math.Abs) < 1e-11) { converged = true; iterations++; break; }
                }
                else
                {
                    // A stationary least-squares solution can have nonzero residual.
                    if (gradient.Max(Math.Abs) < 1e-11) { converged = true; break; }
                    damping *= 10;
                    if (damping > 1e12) return false;
                }
            }
            if (!converged || !Predict(coordinates, p, out double[] final)) return false;
            // Check the undamped Jacobian: damping must not mask unidentifiability.
            return NormalEquations(coordinates, observed, p, final, out double[,] information, out double[] finalGradient)
                && Solve(information, finalGradient, out _);
        }

        private static double Cost(double[] observed, double[] predicted)
        {
            double cost = 0;
            for (int i = 0; i < observed.Length; i++) cost += (observed[i] - predicted[i]) * (observed[i] - predicted[i]);
            return cost;
        }

        private static bool NormalEquations(double[,] coordinates, double[] observed, double[] p, double[] predicted, out double[,] normal, out double[] gradient)
        {
            normal = new double[ParameterCount, ParameterCount];
            gradient = new double[ParameterCount];
            var jacobian = new double[observed.Length, ParameterCount];
            for (int j = 0; j < ParameterCount; j++)
            {
                double epsilon = 1e-6 * (1 + Math.Abs(p[j]));
                double[] plus = (double[])p.Clone(), minus = (double[])p.Clone();
                plus[j] += epsilon;
                minus[j] -= epsilon;
                if (!Predict(coordinates, plus, out double[] upper) || !Predict(coordinates, minus, out double[] lower)) return false;
                for (int i = 0; i < observed.Length; i++) jacobian[i, j] = (upper[i] - lower[i]) / (2 * epsilon);
            }
            for (int j = 0; j < ParameterCount; j++)
            {
                for (int i = 0; i < observed.Length; i++)
                {
                    gradient[j] += jacobian[i, j] * (observed[i] - predicted[i]);
                    for (int k = 0; k < ParameterCount; k++) normal[j, k] += jacobian[i, j] * jacobian[i, k];
                }
            }
            return true;
        }

        private static bool Solve(double[,] matrix, double[] rhs, out double[] solution)
        {
            var a = (double[,])matrix.Clone();
            solution = (double[])rhs.Clone();
            double maximumDiagonal = Enumerable.Range(0, ParameterCount).Max(i => Math.Abs(a[i, i]));
            for (int column = 0; column < ParameterCount; column++)
            {
                int pivot = column;
                for (int row = column + 1; row < ParameterCount; row++) if (Math.Abs(a[row, column]) > Math.Abs(a[pivot, column])) pivot = row;
                if (!double.IsFinite(a[pivot, column]) || Math.Abs(a[pivot, column]) <= 1e-10 * maximumDiagonal) return false;
                for (int j = column; j < ParameterCount; j++) (a[column, j], a[pivot, j]) = (a[pivot, j], a[column, j]);
                (solution[column], solution[pivot]) = (solution[pivot], solution[column]);
                for (int row = column + 1; row < ParameterCount; row++)
                {
                    double multiplier = a[row, column] / a[column, column];
                    for (int j = column; j < ParameterCount; j++) a[row, j] -= multiplier * a[column, j];
                    solution[row] -= multiplier * solution[column];
                }
            }
            for (int row = ParameterCount - 1; row >= 0; row--)
            {
                for (int j = row + 1; j < ParameterCount; j++) solution[row] -= a[row, j] * solution[j];
                solution[row] /= a[row, row];
            }
            return solution.All(double.IsFinite);
        }

        private static double Length(double x, double y) => Math.Sqrt(x * x + y * y);
    }
}
