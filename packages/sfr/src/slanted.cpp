#include <sfr/slanted.h>
#include <sfr/general.h>
#include <Eigen/Dense>
#include<numeric>

// 严格对应 MATLAB rotatev2 的单通道版本（mm = 1 的情况）
cv::Mat sfr::auto_rotate_vertical(const cv::Mat& src) {
    // 对应：a = input array(npix, nlin, ncol)，这里只处理灰度

    cv::Mat a;
    if (src.channels() == 1) {
        src.convertTo(a, CV_64F); // 对应 a = double(a);
    }
    else {

        std::vector<cv::Mat> channels;
        cv::split(src, channels);

        channels[1].convertTo(a, CV_64F); // 对应 a = double(a);
    }
    int nlin = a.rows; // MATLAB: nlin = dim(1)
    int npix = a.cols; // MATLAB: npix = dim(2)

    // 和 rotatev2 里 nn=3 的假设保持一致；尺寸不足时直接不旋转
    int nn = 3;
    if (nlin < nn || npix < nn) {
        return src.clone();  // 太小不旋转
    }

    // MATLAB:
    // testv = abs(mean(a(end-nn,:,mm))-mean(a(nn,:,mm)));
    // 注意 MATLAB 索引从 1 开始：
    //  - a(nn,:,:)        → C++: row nn-1
    //  - a(end-nn,:,:)    → C++: row nlin-nn-1
    double v_top = cv::mean(a.row(nn - 1))[0];
    double v_bottom = cv::mean(a.row(nlin - nn - 1))[0];
    double testv = std::abs(v_bottom - v_top);

    // MATLAB:
    // testh = abs(mean(a(:,end-nn,mm))-mean(a(:,nn,mm)));
    //  - a(:,nn,mm)       → col nn-1
    //  - a(:,end-nn,mm)   → col npix-nn-1
    double h_left = cv::mean(a.col(nn - 1))[0];
    double h_right = cv::mean(a.col(npix - nn - 1))[0];
    double testh = std::abs(h_right - h_left);

    // MATLAB: if testv > testh, rflag=1; a = rotate90(a);
    // rotate90 是逆时针 90°
    cv::Mat out;
    if (testv > testh) {
        cv::rotate(src, out, cv::ROTATE_90_COUNTERCLOCKWISE);
    }
    else {
        out = src.clone();
    }

    return out;
}

// 计算一行加窗导数的质心（列方向），对应 MATLAB 的 centroid(c.*win) - 0.5
double row_centroid(const cv::Mat1d& row, const std::vector<double>& win) {
    const int n = row.cols;
    if (win.size() != n) {
        // 最好加上一个安全检查
        throw std::runtime_error("Row and window sizes do not match.");
    }

    double num = 0.0; // 分子
    double den = 0.0; // 分母

    for (int j = 0; j < n; ++j) {
        double v = row.at<double>(0, j) * win[j];

        // 关键修正：使用 (j + 1) 来模拟 MATLAB 的 1-based 索引
        num += v * (j + 1);

        den += v;
    }

    if (den == 0.0) {
        // 和 MATLAB 的 loc/sum(x) 在 sum(x)=0 时行为一致 (返回 NaN 或 Inf)。
        // 这里返回 0.0 是一个合理的处理，但要注意可能与 MATLAB 的 NaN 行为不同。
        // 如果需要完全一致，可以返回 std::numeric_limits<double>::quiet_NaN();
        return 0.0;
    }

    // 最后的 -0.5 移位与 MATLAB 一致
    return num / den - 0.5;
}
double mean(const std::vector<double>& v) {
    if (v.empty()) return 0.0;
    double sum = std::accumulate(v.begin(), v.end(), 0.0);
    return sum / v.size();
}

double stddev(const std::vector<double>& v, double m) {
    if (v.size() < 2) return 0.0;
    double accum = 0.0;
    std::for_each(v.begin(), v.end(), [&](const double d) {
        accum += (d - m) * (d - m);
        });
    return std::sqrt(accum / (v.size() - 1));
}

// 前置依赖：一个计算二项式系数 nchoosek(n, k) 的函数
// 您可以自己实现，或使用库。这是一个简单的实现。
long long nchoosek(int n, int k) {
    if (k < 0 || k > n) {
        return 0;
    }
    if (k == 0 || k == n) {
        return 1;
    }
    if (k > n / 2) {
        k = n - k;
    }
    long long res = 1;
    for (int i = 1; i <= k; ++i) {
        res = res * (n - i + 1) / i;
    }
    return res;
}

// ===== 2. 实现 polyfit_convert 的 C++ 版本 =====
// 这个函数直接翻译自 MATLAB 的数学逻辑
std::vector<double> polyfit_convert_cpp(const std::vector<double>& p_scaled_asc, double m, double s) {
    if (s == 0.0) {
        // 防止除以零
        throw std::runtime_error("Standard deviation cannot be zero.");
    }

    // 1. 获取多项式次数
    const int degree = static_cast<int>(p_scaled_asc.size()) - 1;
    if (degree < 0) {
        return {};
    }

    // 2. 初始化最终的（未缩放）系数向量
    std::vector<double> p_unscaled(degree + 1, 0.0);

    // 3. 核心转换逻辑
    // 遍历 p_scaled_asc 的每一个系数 p_i，它对应于缩放后的项 (x')^i
    // 其中 x' = (x - m) / s
    for (int i = 0; i <= degree; ++i) {
        double p_i = p_scaled_asc[i]; // 这是 c_i in c_i * ((x-m)/s)^i

        // 使用二项式定理展开 (x - m)^i = Σ [nchoosek(i, j) * x^j * (-m)^(i-j)]
        // 所以，p_i * ((x-m)/s)^i = (p_i / s^i) * Σ [nchoosek(i, j) * x^j * (-m)^(i-j)]

        double s_pow_i = std::pow(s, i);

        for (int j = 0; j <= i; ++j) {
            // 计算这个展开项对最终多项式中 x^j 项的贡献
            double contribution = p_i / s_pow_i * nchoosek(i, j) * std::pow(-m, i - j);

            // 将贡献累加到最终系数的相应位置
            p_unscaled[j] += contribution;
        }
    }

    return p_unscaled;
}

// 拟合 loc(line) 与行号的关系：loc = f(line)，多项式次数 degree
std::vector<double> edge_polyfit(const std::vector<double>& loc, int degree) {
    const int nlin = static_cast<int>(loc.size());
    std::vector<double> x(nlin);
    std::vector<double> x_scaled(nlin);

    for (int i = 0; i < nlin; ++i) x[i] = static_cast<double>(i);

    // 计算 mu
    double mu1_mean = mean(x);
    double mu2_stddev = stddev(x, mu1_mean);

    // 创建 x_scaled
    for (int i = 0; i < nlin; ++i) {
        x_scaled[i] = (x[i] - mu1_mean) / mu2_stddev;
    }

    // 对缩放后的数据进行拟合
    std::vector<double> p_scaled = sfr::polyfit(x_scaled, loc, degree);

    // 将系数转换回来
    // 注意：MATLAB的polyfit返回高次项在前，如果sfr::polyfit是低次项在前，转换时要注意
    // 假设 sfr::polyfit 返回 [c0, c1, ..., cn]
    // 假设 polyfit_convert_cpp 也返回 [c0, c1, ..., cn]
    std::vector<double> p_final = polyfit_convert_cpp(p_scaled, mu1_mean, mu2_stddev);

    return p_final;
}

// 等价于 MATLAB: b = deriv1(a, nlin, npix, fil)
// a  : CV_64F, 单通道, size = (nlin, npix)
// fil: CV_64F, 1xN 行向量 (例如 [0.5 -0.5] 或 [-0.5 0.5])
cv::Mat1d deriv1_like(const cv::Mat1d& a, const cv::Mat1d& fil)
{
    const int nlin = a.rows;
    const int npix = a.cols;

    // 1. 翻转滤波器
    // MATLAB 的 `conv` 执行卷积。OpenCV 的 `filter2D` 执行相关。
    // 为了用 `filter2D` 实现卷积，我们需要先手动翻转滤波器。
    cv::Mat1d flipped_fil;
    cv::flip(fil, flipped_fil, -1); // -1 表示在两个轴上翻转

    // 2. 定义锚点
    // 这是关键！将锚点设置为 (0,0) 意味着滤波器的左上角 (即第一个元素)
    // 将在计算时对准输入像素。这精确模拟了 MATLAB conv 的行为。
    cv::Point anchor(0, 0);

    // 3. 执行滤波
    // - 使用翻转后的滤波器。
    // - 使用自定义锚点。
    // - 使用 BORDER_CONSTANT 和标量 0 来模拟 MATLAB conv 在边界外的补零行为。
    cv::Mat1d b;
    cv::filter2D(a, b, -1, flipped_fil, anchor, 0, cv::BORDER_CONSTANT);

    // 4. 应用 MATLAB 代码中最后的边缘修正
    // 这一步和原始 MATLAB 代码完全相同。
    if (npix >= 2) {
        for (int i = 0; i < nlin; ++i) {
            b.at<double>(i, 0) = b.at<double>(i, 1);
            b.at<double>(i, npix - 1) = b.at<double>(i, npix - 2);
        }
    }

    return b;
}


// 对单通道 ROI 做多项式边缘拟合
// mat  : 单通道图像（灰度），可以是 uchar / ushort / double
// npol : 多项式阶数，1=直线，5=五次（对齐 sfrmat5 默认）
// loc_out : 若不为 nullptr，返回每一行的边缘位置 loc（单位：列索引）
std::vector<double> sfr::poly_edge_fit(const cv::Mat& mat, int npol, std::vector<double>* loc_out)
{
    CV_Assert(mat.channels() == 1);
    const int nlin = mat.rows;
    const int npix = mat.cols;

    cv::Mat1d a;
    mat.convertTo(a, CV_64F);

    // 估计左右亮度
    double tleft = cv::sum(a.colRange(0, 5))[0];
    double tright = cv::sum(a.colRange(npix - 5, npix))[0];

    // 基础核：[-0.5, 0, 0.5]
    cv::Mat1d fil1(1, 2);
    fil1(0, 0) = 0.5;
    fil1(0, 1) = -0.5;
    // 如果左边更亮，就把核反号，保证导数对应的是“暗→亮”正峰
    if (tleft > tright) {
        fil1 = -fil1;
    }

    cv::Mat1d deriv = deriv1_like(a, fil1);

    double alpha = 1.0; // 或你希望的值

    // 第一轮：对称 Hamming 窗，粗略 loc
    auto win1 = sfr::tukey2(npix, alpha, (npix + 1) / 2.0);
    for (double& v : win1) v = 0.95 * v + 0.05;   // 与 MATLAB inbox5 中对 Tukey 的处理一致

    std::vector<double> loc(nlin, 0.0);

    for (int i = 0; i < nlin; ++i) {
        cv::Mat1d row = deriv.row(i);
        loc[i] = row_centroid(row, win1);
    }

    // 粗略多项式拟合
    auto coeff = edge_polyfit(loc, npol);

    // 第二轮：用拟合位置生成非对称窗，重新估计 loc
    for (int i = 0; i < nlin; ++i) {
        // place = polyval(coeff, i)
        double place = sfr::polyval(std::vector<double>{static_cast<double>(i)}, coeff)[0];
        auto win2 = sfr::tukey2(npix, alpha,place);
        // MATLAB 中对 Tukey 窗有抬底：win2 = 0.95*win2 + 0.05;
        for (double& v : win2) v = 0.95 * v + 0.05;

        cv::Mat1d row = deriv.row(i);
        loc[i] = row_centroid(row, win2);
    }

    // 最终多项式拟合
    coeff = edge_polyfit(loc, npol);

    if (loc_out) *loc_out = loc;
    return coeff;   // 低次在前：coeff[0] + coeff[1]*x + ...
}


std::tuple<double, double> centroid_fit(const cv::InputArray img) {
    cv::Mat mat = img.getMat();
    std::vector<cv::Point2d> edge;
    for (int i = 0; i < mat.rows; ++i) {
        const double* ptr = mat.ptr<double>(i);
        double sum = 0, sum_y = 0;
        for (int j = 0; j < mat.cols; ++j) {
            sum += ptr[j]; sum_y += ptr[j] * j;
        }
        if (sum != 0) {
            edge.emplace_back(i, sum_y / sum - 0.5);
        }
    }
    cv::Vec4d line;
    cv::fitLine(edge, line, cv::DIST_L2, 0, 0.01, 0.01);
    double k = line[1] / line[0], offset = line[3] - k * line[2];
    return { k, offset };
}

std::tuple<double, double> sfr::line_fit(const cv::Mat& mat) {
    cv::Mat img = mat.clone();
    cv::Mat derivate(img.size(), CV_64F), hammed(img.size(), CV_64F);
    cv::filter2D(img, derivate, CV_64F, sfr::kernel);
    std::vector<double> w = sfr::hamming(mat.cols);
    for (int i = 0; i < mat.rows; ++i)
        hammed.row(i) = derivate.row(i).mul(w);

    auto [k0, b0] = centroid_fit(hammed);

    for (int i = 0; i < mat.rows; ++i) {
        double place = k0 * i + b0;
        w = sfr::tukey(mat.cols, place);
        hammed.row(i) = derivate.row(i).mul(w);
    }
    return centroid_fit(hammed);
}

std::vector<double> sfr::esf(const cv::Mat& mat, const std::vector<double>& fitme, int nbin)
{
    CV_Assert(mat.channels() == 1);
    const int nlin = mat.rows;
    const int npix = mat.cols;
    if (nbin <= 0) nbin = sfr::factor;

    // 1D 拟合：loc = polyval(fitme, y)
    // 构造 p2(m) = polyval(fitme,y) - fitme(0)（最低次项对应 MATLAB 的 int）
    std::vector<double> p2(nlin, 0.0);
    for (int m = 0; m < nlin; ++m) {
        double y = static_cast<double>(m);
        double val = sfr::polyval(std::vector<double>{y}, fitme)[0];
        // 减去常数项，类似 MATLAB p2(m) = polyval(fitme,y)-fitme(end)
        val -= fitme[0];
        p2[m] = val;
    }

    const int nn = static_cast<int>(std::floor(npix * nbin));
    // 估计 offset 和 bwidth：基本仿照 project2.m
    // slope = 1/slope_edge; 这里简单用线性近似：slope = fitme[1]
    const double slope = (fitme.size() > 1) ? fitme[1] : 0.0;
    int offset = 0;
    if (std::abs(slope) > 1e-8) {
        double invslope = 1.0 / slope;
        offset = static_cast<int>(std::round(nbin * (0.0 - (nlin - 1) / invslope)));
    }
    int del = std::abs(offset);
    if (offset > 0) offset = 0;

    const int bwidth = nn + del + 150;
    std::vector<double> cnt(bwidth, 0.0);
    std::vector<double> acc(bwidth, 0.0);

    // 投影与 binning
    cv::Mat1d a;
    mat.convertTo(a, CV_64F);
    for (int m = 0; m < nlin; ++m) {
        for (int n = 0; n < npix; ++n) {
            double x = n;
            int ling = static_cast<int>(std::ceil((x - p2[m]) * nbin)) + 1 - offset;
            ling = std::clamp(ling, 0, bwidth - 1);
            cnt[ling] += 1.0;
            acc[ling] += a(m, n);
        }
    }

    // 处理 zero-count（简化版：直接用邻近平均；可进一步精细化对齐 project2）
    int start = 1 + static_cast<int>(std::round(0.5 * del));
    for (int i = start; i < start + nn && i < bwidth; ++i) {
        if (cnt[i] == 0.0) {
            // 左右邻平均
            int i1 = std::max(start, i - 1);
            int i2 = std::min(start + nn - 1, i + 1);
            cnt[i] = 0.5 * (cnt[i1] + cnt[i2]);
            acc[i] = 0.5 * (acc[i1] + acc[i2]);
        }
    }

    // 求 ESF
    std::vector<double> esf(nn, 0.0);
    for (int i = 0; i < nn; ++i) {
        int idx = start + i;
        if (idx >= 0 && idx < bwidth && cnt[idx] > 0.0) {
            esf[i] = acc[idx] / cnt[idx];
        }
    }
    return esf;
}

std::vector<double> sfr::lsf(const std::vector<double>& esf) {
    const int n = static_cast<int>(esf.size());

    // 将 ESF 转成 cv::Mat1d 行向量
    cv::Mat1d esfMat(1, n);
    for (int i = 0; i < n; ++i) esfMat(0, i) = esf[i];

    // 一维差分滤波（kernel: -0.5, 0, 0.5）
    cv::Mat1d diffMat;
    cv::filter2D(esfMat, diffMat, CV_64F, sfr::kernel);

    // diffMat -> diff
    std::vector<double> diff(n);
    for (int i = 0; i < n; ++i) diff[i] = diffMat(0, i);

    // 用“绝对值最大”作为峰
    double maxVal = 0.0;
    int maxIdx = 0;
    for (int i = 0; i < n; ++i) {
        if (std::abs(diff[i]) > std::abs(maxVal)) {
            maxVal = diff[i];
            maxIdx = i;
        }
    }

    // 中心移到中点
    diff = sfr::center_shift(diff, maxIdx + 1);

    // 乘窗并用 maxVal 归一化
    auto win = sfr::hamming(n);
    std::vector<double> lsf(n);
    for (int i = 0; i < n; ++i) {
        lsf[i] = win[i] * diff[i] / maxVal;
    }

    return lsf;
}

std::vector<double> sfr::mtf(const std::vector<double>& lsf) {
    const int nn = static_cast<int>(lsf.size());

    cv::Mat1d lsfMat(1, nn);
    for (int i = 0; i < nn; ++i) lsfMat(0, i) = lsf[i];

    cv::Mat fftMat;
    cv::dft(lsfMat, fftMat, cv::DFT_COMPLEX_OUTPUT);

    std::vector<std::complex<double>> fft(nn);
    for (int i = 0; i < nn; ++i) {
        cv::Vec2d c = fftMat.at<cv::Vec2d>(0, i);
        fft[i] = std::complex<double>(c[0], c[1]);
    }

    const double DC = fft[0].real();
    const int nn2 = nn / 2 + 1;

    // 差分滤波器 MTF 修正
    auto corr = sfr::fir2fix(nn2, 3);

    const int nOut = nn / sfr::factor;
    const int nUse = std::min(nOut, nn2);

    std::vector<double> mtf(nOut, 0.0);
    for (int i = 0; i < nUse; ++i) {
        double raw = std::abs(fft[i]) / DC;
        mtf[i] = raw* corr[i];
    }
    return mtf;
}

double sfr::mtf10(const std::vector<double>& mtf) {
    auto it = std::adjacent_find(mtf.begin(), mtf.end(),
        [](double l, double r) { return l > 0.1 && r < 0.1; });
    if (it == mtf.end() || (it + 1) == mtf.end()) return 0.0;
    int i = static_cast<int>(std::distance(mtf.begin(), it));
    double y1 = *it;
    double y2 = *(it + 1);
    double x1 = static_cast<double>(i) / mtf.size();
    double x2 = static_cast<double>(i + 1) / mtf.size();
    return x1 + (0.1 - y1) * (x2 - x1) / (y2 - y1);
}

double sfr::mtf50(const std::vector<double>& mtf) {
    auto it = std::adjacent_find(mtf.begin(), mtf.end(),
        [](double l, double r) { return l > 0.5 && r < 0.5; });
    if (it == mtf.end() || (it + 1) == mtf.end()) return 0.0;
    int i = static_cast<int>(std::distance(mtf.begin(), it));
    double y1 = *it;
    double y2 = *(it + 1);
    double x1 = static_cast<double>(i) / mtf.size();
    double x2 = static_cast<double>(i + 1) / mtf.size();
    return x1 + (0.5 - y1) * (x2 - x1) / (y2 - y1);
}


double find_freq_at_threshold(
    const std::vector<double>& freq_axis,
    const std::vector<double>& sfr_data,
    double threshold)
{
    if (freq_axis.size() != sfr_data.size() || sfr_data.empty()) {
        return 0.0; // Input validation
    }

    // 1. Find the first pair of points that bracket the threshold
    // We need to find an iterator to the *first* element of the pair.
    auto it = std::adjacent_find(sfr_data.begin(), sfr_data.end(),
        [=](double y1, double y2) { return y1 >= threshold && y2 < threshold; });

    // If no such pair is found, or if it's the last element, we can't interpolate
    if (it == sfr_data.end()) {
        return 0.0;
    }

    // 2. Get the index and the bracketing values for y (SFR)
    int i = std::distance(sfr_data.begin(), it);
    double y1 = sfr_data[i];
    double y2 = sfr_data[i + 1];

    // 3. Get the corresponding bracketing values for x (frequency)
    // THIS IS THE CRITICAL CHANGE
    double x1 = freq_axis[i];
    double x2 = freq_axis[i + 1];

    // 4. Perform linear interpolation
    if (std::abs(y2 - y1) < 1e-9) { // Avoid division by zero
        return x1;
    }
    return x1 + (threshold - y1) * (x2 - x1) / (y2 - y1);
}

using namespace sfr;


SFRResult sfr::CalSFR(const cv::Mat& imgIn,
    double del,
    int    npol,
    int    nbin ,double vslope)
{
    SFRResult result;
    if (imgIn.empty()) return result;

    // 1. 保证单通道 & 自动旋转
    // 1. 保证单通道 & 自动旋转
    cv::Mat gray;
    gray = sfr::auto_rotate_vertical(imgIn.clone());

    // 2. 多项式边缘拟合
    std::vector<double> loc;
    auto fitme = poly_edge_fit(gray, npol, &loc);
    auto fitme1 = edge_polyfit(loc, 1); // [a0, a1]
    if (vslope == -1) {
        vslope = fitme1[1];           // 就是亮度边缘的一阶斜率
    }
    result.vslope = vslope;
    int nlin = gray.rows;
    double s = std::abs(vslope);
    int nlin1 = nlin;
    if (s > 1e-12) {
        nlin1 = static_cast<int>(std::round(std::floor(nlin * s) / s));
    }
    if (nlin1 > 0 && nlin1 < nlin) {
        gray = gray.rowRange(0, nlin1);
    }

    double delfac = std::cos(std::atan(vslope));
    double del_image = del;               // 保存原始 del（像 MATLAB 的 delimage）

    del *= delfac;                        // 沿法线方向的采样间距
    double del2 = del / nbin;             // 法线方向超采样间距


    // 3. ESF / LSF / MTF
    if (nbin <= 0) nbin = sfr::factor;
    auto esf_vec = esf(gray, fitme, nbin);
    if (esf_vec.empty()) return result;

    auto lsf_vec = lsf(esf_vec);
    if (lsf_vec.empty()) return result;

    auto mtf_vec = mtf(lsf_vec);
    if (mtf_vec.empty()) return result;

    // 5. 物理频率轴 freq(i) = i / (del2 * nn)
    int  nn = static_cast<int>(esf_vec.size());
    int  Nmtf = static_cast<int>(mtf_vec.size()); // 现在应该等于 nn2

    result.freq.resize(Nmtf);
    result.sfr.resize(Nmtf);
    for (int i = 0; i < Nmtf; ++i) {
        double f = static_cast<double>(i) / (del2 * nn); // 与 sfrmat5 一致
        result.freq[i] = f;
        result.sfr[i] = mtf_vec[i];
    }

    double mtf10_cypix = find_freq_at_threshold(result.freq, result.sfr, 0.1);
    double mtf50_cypix = find_freq_at_threshold(result.freq, result.sfr, 0.5);

    double fNyquist = 0.5 / del;

    // The function already gives the absolute value in cy/pixel.
    result.mtf10_cypix = mtf10_cypix;
    result.mtf50_cypix = mtf50_cypix;

    // The "normalized" value is the absolute value divided by the Nyquist frequency.
    result.mtf10_norm = (fNyquist > 0) ? mtf10_cypix / fNyquist : 0.0;
    result.mtf50_norm = (fNyquist > 0) ? mtf50_cypix / fNyquist : 0.0;

    return result;
}