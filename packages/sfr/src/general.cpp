#include <sfr/general.h>
#include <Eigen/Dense>
#include <algorithm>
#include <cmath>

// n: frequency data length [0-half-sampling (Nyquist) frequency]
// m: length of difference filter (2 -> 2-point, 3 -> 3-point)
// 输出: 长度为 n 的校正因子 correct
//      一般用法: MTF_corrected[i] = MTF_raw[i] * correct[i];
std::vector<double> sfr::fir2fix(int n, int m)
{
    std::vector<double> correct(n, 1.0);
    m = m - 1;
    double scale = 1.0;

    // i: 2:n (MATLAB) -> C++ 1..n-1
    for (int i = 1; i < n; ++i)
    {
        // 注意 MATLAB 用的是 (n+1)
        double x = CV_PI * i * m / (2.0 * (n + 1));
        double denom = std::sin(x);

        if (std::abs(denom) < 1e-12)
        {
            correct[i] = 10.0;
            continue;
        }

        double val = std::abs(x / denom);
        // MATLAB: correct(i) = 1 + scale*(correct(i)-1);
        val = 1.0 + scale * (val - 1.0);

        // clip 到 [1, 10]
        if (val > 10.0) val = 10.0;
        if (val < 1.0)  val = 1.0;

        correct[i] = val;
    }

    return correct;
}

// 内部对称 Tukey 窗，对应 MATLAB 里的 tukey(n, alpha)
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

    // 限制 alpha 到 [0,1]
    if (alpha < 0.0) alpha = 0.0;
    if (alpha > 1.0) alpha = 1.0;

    w.assign(n, 0.0);
    const double M = (n - 1) / 2.0;

    for (int k = 0; k <= static_cast<int>(M); ++k) {
        double value;
        if (k <= alpha * M) {
            // 0.5*(1 + cos(pi*(k/alpha/M - 1)))
            value = 0.5 * (1.0 + std::cos(CV_PI * (k / (alpha * M) - 1.0)));
        }
        else {
            value = 1.0;
        }
        w[k] = value;
        w[n - 1 - k] = value; // 对称
    }

    return w;
}

std::vector<double> sfr::tukey2(int n, double alpha, double mid)
{
    std::vector<double> w;
    if (n <= 0) return w;

    if (n < 3) {
        w.assign(n, 1.0);
        return w;
    }

    // 对应 MATLAB 默认值逻辑：
    // if nargin<3, mid = n/2; end
    // if nargin<2, alpha = 1; end
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
    if (n2 < n) n2 = n; // 防止太小

    auto big = tukey_symmetric(n2, alpha);
    w.assign(n, 0.0);

    if (mid >= m1) {
        // w = w(1:n);
        std::copy(big.begin(), big.begin() + n, w.begin());
    }
    else {
        // w = w(1+end-n:end);
        int start = n2 - n;
        std::copy(big.begin() + start, big.begin() + start + n, w.begin());
    }

    return w;
}

/**
 * 非对称 Hamming-type 窗，对齐 MATLAB ahamming.m
 * n0  : 窗长 n
 * mid_in : MATLAB 中的 mid（未加 0.5 之前）
 *
 * MATLAB ahamming:
 * mid = mid+0.5;
 * wid1 = mid-1;
 * wid2 = n-mid;
 * wid = max(wid1,wid2);
 * for i=1:n
 *   arg = i-mid;
 *   data(i) = cos(pi*arg/wid);
 * end
 * data = 0.54 + 0.46*data;
 */
std::vector<double> sfr::tukey(const int n0, const double mid_in) {
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
        double arg = (i + 1) - mid;          // MATLAB 下标从 1 开始
        double c = std::cos(CV_PI * arg / wid);
        w[i] = 0.54 + 0.46 * c;
    }
    return w;
}

/**
 * center_shift: 对齐 MATLAB cent.m
 * 输入向量 x，令 x[center] 平移至中间位置（保留长度，空位补 0）
 *
 * MATLAB:
 * n = length(a);
 * mid = round((n+1)/2);
 * del = round(center - mid);
 * if del > 0
 *   b(i) = a(i+del); i=1:n-del
 * elseif del < 1
 *   b(i) = a(i+del); i=-del+1:n
 * else
 *   b = a;
 * end
 */
std::vector<double> sfr::center_shift(const std::vector<double>& x, const int center) {
    const int n = static_cast<int>(x.size());
    std::vector<double> out(n, 0.0);
    if (n == 0) return out;

    const int mid = static_cast<int>(std::round((n + 1) / 2.0));
    const int del = static_cast<int>(std::round(center - mid));

    if (del > 0) {
        // b(i) = a(i + del); i = 1 : n-del
        for (int i = 0; i < n - del; ++i) {
            out[i] = x[i + del];
        }
    }
    else if (del < 1) {
        // b(i) = a(i + del); i = -del+1 : n
        for (int i = -del; i < n; ++i) {
            out[i] = x[i + del];
        }
    }
    else {
        out = x;
    }

    return out;
}

// 注意：polyfit / polyval 使用的是“低次项在前”的约定：
// coeff[0] 是常数项，coeff[1] 是 x^1，依此类推。
std::vector<double> sfr::polyfit(const std::vector<double>& x,
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

std::vector<double> sfr::polyval(const std::vector<double>& x,
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


