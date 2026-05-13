#include "sfr_base.h"
#include <Eigen/Dense>
#include <algorithm>
#include <cmath>

namespace cvcore {
namespace sfr {

// FIR filter correction for MTF
// n: frequency data length [0-half-sampling (Nyquist) frequency]
// m: length of difference filter (2 -> 2-point, 3 -> 3-point)
std::vector<double> fir2fix(int n, int m)
{
    std::vector<double> correct(n, 1.0);
    m = m - 1;
    double scale = 1.0;

    // i: 2:n (MATLAB) -> C++ 1..n-1
    for (int i = 1; i < n; ++i)
    {
        double x = CV_PI * i * m / (2.0 * (n + 1));
        double denom = std::sin(x);

        if (std::abs(denom) < 1e-12)
        {
            correct[i] = 10.0;
            continue;
        }

        double val = std::abs(x / denom);
        val = 1.0 + scale * (val - 1.0);

        // clip to [1, 10]
        if (val > 10.0) val = 10.0;
        if (val < 1.0)  val = 1.0;

        correct[i] = val;
    }

    return correct;
}

// Internal symmetric Tukey window (corresponds to MATLAB tukey)
static std::vector<double> tukey_symmetric(int n, double alpha)
{
    std::vector<double> w;

    if (n <= 0) return w;

    if (n == 1) {
        w.assign(1, 1.0);
        return w;
    }

    if (alpha == 0.0) {
        w.assign(n, 1.0);
        return w;
    }

    // Clamp alpha to [0,1]
    if (alpha < 0.0) alpha = 0.0;
    if (alpha > 1.0) alpha = 1.0;

    w.assign(n, 0.0);
    const double M = (n - 1) / 2.0;

    for (int k = 0; k <= static_cast<int>(M); ++k) {
        double value;
        if (k <= alpha * M) {
            value = 0.5 * (1.0 + std::cos(CV_PI * (k / (alpha * M) - 1.0)));
        }
        else {
            value = 1.0;
        }
        w[k] = value;
        w[n - 1 - k] = value; // symmetric
    }

    return w;
}

std::vector<double> tukey2(int n, double alpha, double mid)
{
    std::vector<double> w;
    if (n <= 0) return w;

    if (n < 3) {
        w.assign(n, 1.0);
        return w;
    }

    if (mid <= 0.0) {
        mid = n / 2.0;
    }
    if (alpha <= 0.0) {
        alpha = 1.0;
    }

    const double m1 = n / 2.0;
    const double m2 = mid;
    const double m3 = n - mid;
    const double mm = std::max(m2, m3);

    int n2 = static_cast<int>(std::round(2.0 * mm));
    if (n2 < n) n2 = n;

    auto big = tukey_symmetric(n2, alpha);
    w.assign(n, 0.0);

    if (mid >= m1) {
        std::copy(big.begin(), big.begin() + n, w.begin());
    }
    else {
        int start = n2 - n;
        std::copy(big.begin() + start, big.begin() + start + n, w.begin());
    }

    return w;
}

// Asymmetric Hamming-type window (corresponds to MATLAB ahamming)
std::vector<double> tukey(int n0, double mid_in) {
    if (n0 <= 0) return {};

    std::vector<double> w(n0, 0.0);

    const double mid = mid_in + 0.5;
    const double wid1 = mid - 1.0;
    const double wid2 = n0 - mid;
    const double wid = std::max(wid1, wid2);

    if (wid <= 0.0) {
        std::fill(w.begin(), w.end(), 1.0);
        return w;
    }

    for (int i = 0; i < n0; ++i) {
        double arg = (i + 1) - mid;
        double c = std::cos(CV_PI * arg / wid);
        w[i] = 0.54 + 0.46 * c;
    }
    return w;
}

// Center shift (corresponds to MATLAB cent.m)
std::vector<double> center_shift(const std::vector<double>& x, int center) {
    const int n = static_cast<int>(x.size());
    std::vector<double> out(n, 0.0);
    if (n == 0) return out;

    const int mid = static_cast<int>(std::round((n + 1) / 2.0));
    const int del = static_cast<int>(std::round(center - mid));

    if (del > 0) {
        for (int i = 0; i < n - del; ++i) {
            out[i] = x[i + del];
        }
    }
    else if (del < 1) {
        for (int i = -del; i < n; ++i) {
            out[i] = x[i + del];
        }
    }
    else {
        out = x;
    }

    return out;
}

// Polynomial fitting using Eigen
// coeff[0] is constant term, coeff[1] is x^1 coefficient, etc.
std::vector<double> polyfit(const std::vector<double>& x,
                            const std::vector<double>& y,
                            int degree) {
    assert(x.size() == y.size());
    const int n = static_cast<int>(x.size());
    if (n == 0) return {};

    if (degree < 0) degree = 0;
    if (degree + 1 > n) degree = n - 1;

    Eigen::VectorXd Y = Eigen::Map<const Eigen::VectorXd>(y.data(), y.size());
    Eigen::MatrixXd A(n, degree + 1);
    for (int i = 0; i < n; i++) {
        double xp = 1.0;
        for (int j = 0; j <= degree; j++) {
            A(i, j) = xp;
            xp *= x[i];
        }
    }
    Eigen::VectorXd coef = A.householderQr().solve(Y);
    return std::vector<double>(coef.data(), coef.data() + coef.size());
}

std::vector<double> polyval(const std::vector<double>& x,
                            const std::vector<double>& coeff) {
    std::vector<double> out(x.size(), 0.0);
    const int m = static_cast<int>(coeff.size());
    for (int i = 0; i < static_cast<int>(x.size()); i++) {
        double v = 0.0;
        double xp = 1.0;
        for (int j = 0; j < m; j++) {
            v += coeff[j] * xp;
            xp *= x[i];
        }
        out[i] = v;
    }
    return out;
}

} // namespace sfr
} // namespace cvcore
