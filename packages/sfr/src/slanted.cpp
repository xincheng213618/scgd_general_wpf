#include <sfr/slanted.h>
#include <sfr/general.h>


cv::Mat sfr::auto_rotate_vertical(const cv::Mat& src) {
    CV_Assert(src.channels() == 1);
    cv::Mat a;
    src.convertTo(a, CV_64F);

    int nlin = a.rows;
    int npix = a.cols;
    if (nlin < 3 || npix < 3) {
        return src.clone(); // 太小不处理
    }

    // 对应 MATLAB 中 nn = 3; testv/testh
    int nn = std::min(3, std::min(nlin, npix) / 2);

    // 垂直方向（上下）灰度变化
    cv::Mat top = a.rowRange(0, nn);
    cv::Mat bottom = a.rowRange(nlin - nn, nlin);
    double v1 = cv::mean(top)[0];
    double v2 = cv::mean(bottom)[0];
    double testv = std::abs(v2 - v1);

    // 水平方向（左右）灰度变化
    cv::Mat left = a.colRange(0, nn);
    cv::Mat right = a.colRange(npix - nn, npix);
    double h1 = cv::mean(left)[0];
    double h2 = cv::mean(right)[0];
    double testh = std::abs(h2 - h1);

    cv::Mat out;
    // MATLAB: if testv > testh, rflag=1; a = rotate90(a);
    if (testv > testh) {
        // 边缘更像水平线 → 旋转 90° 让它竖直
        cv::rotate(src, out, cv::ROTATE_90_CLOCKWISE);
    }
    else {
        out = src.clone();
    }
    return out;
}

// 计算一行加窗导数的质心（列方向），对应 MATLAB 的 centroid(c.*win) - 0.5
double row_centroid(const cv::Mat1d& row, const std::vector<double>& win) {
    const int n = row.cols;
    double num = 0.0, den = 0.0;
    for (int j = 0; j < n; ++j) {
        double v = row(0, j) * win[j];
        num += v * j;
        den += v;
    }
    if (den == 0.0) return 0.0;
    return num / den - 0.5;
}

// 拟合 loc(line) 与行号的关系：loc = f(line)，多项式次数 degree
std::vector<double> edge_polyfit(const std::vector<double>& loc, int degree) {
    const int nlin = static_cast<int>(loc.size());
    std::vector<double> x(nlin), y(nlin);
    for (int i = 0; i < nlin; ++i) {
        x[i] = static_cast<double>(i);   // 对应 MATLAB index = 0:nlin-1
        y[i] = loc[i];
    }
    return sfr::polyfit(x, y, degree);
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

    // 转 double
    cv::Mat1d a;

     //cv::threshold(mat, mat,100,255,0);

    mat.convertTo(a, CV_64F);

    // 1D 导数（沿列方向），对应 deriv1(a(:,:,color), fil1/fil2)
    cv::Mat1d deriv;
    cv::filter2D(a, deriv, CV_64F, sfr::kernel);

    // 第一轮：对称 Hamming 窗，粗略 loc
    std::vector<double> win1 = sfr::hamming(npix);
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
        auto win2 = sfr::tukey(npix, place);
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

    // 找到最大响应位置（视为 LSF 中心）
    double maxVal;
    cv::Point maxLoc;
    cv::minMaxLoc(diffMat, nullptr, &maxVal, nullptr, &maxLoc);

    // 转回 std::vector
    std::vector<double> diff(n);
    for (int i = 0; i < n; ++i) diff[i] = diffMat(0, i);

    // 使用新的 center_shift 把峰值移到中间
    diff = sfr::center_shift(diff, maxLoc.x + 1);
    // +1：center_shift 中使用的是 MATLAB 风格的“索引”，maxLoc.x 是 0-based，
    // 对齐 MATLAB i=1..n，需要 +1

    // 乘 Hamming 窗并归一化
    std::vector<double> win = sfr::hamming(n);
    std::vector<double> lsf(n);
    for (int i = 0; i < n; ++i) {
        lsf[i] = win[i] * diff[i] / maxVal;
    }

    return lsf;
}

std::vector<double> sfr::mtf(const std::vector<double>& lsf) {
    const int nn = static_cast<int>(lsf.size());

    // FFT
    cv::Mat1d lsfMat(1, nn);
    for (int i = 0; i < nn; ++i) lsfMat(0, i) = lsf[i];

    cv::Mat fftMat;
    cv::dft(lsfMat, fftMat, cv::DFT_COMPLEX_OUTPUT);

    // 取复数数组
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


    // 剩余的保持 0 或延用最后一个值，看你需求；这里保留 0 即可。

    return mtf;
}

double sfr::mtf10(const std::vector<double> &mtf) {
    auto point = std::adjacent_find(mtf.begin(), mtf.end(), [](auto&& l, auto&& r) { return l > 0.1 && r < 0.1; });
    const int n = mtf.size(), i = std::distance(mtf.begin(), point);
    // 插值计算
    double k = n * (*(point+1) - *point);
    return (0.1 - *point) / k + 1.0 * i / n;
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
    // 线性插值
    double f50 = x1 + (0.5 - y1) * (x2 - x1) / (y2 - y1);
    return f50;
}

using namespace sfr;


SFRResult sfr::CalSFR(const cv::Mat& imgIn,
    double del,
    int    npol,
    int    nbin)
{
    SFRResult result;
    if (imgIn.empty()) return result;

    // 1. 保证单通道 & 自动旋转
    cv::Mat gray;
    if (imgIn.channels() == 3) {
        cv::cvtColor(imgIn, gray, cv::COLOR_BGR2GRAY);
    }
    else if (imgIn.channels() == 4) {
        cv::cvtColor(imgIn, gray, cv::COLOR_BGRA2GRAY);
    }
    else {
        gray = imgIn.clone();
    }
    gray = auto_rotate_vertical(gray);

    // 2. 多项式边缘拟合
    std::vector<double> loc;
    auto fitme = poly_edge_fit(gray, npol, &loc);

    // 3. ESF / LSF / MTF
    if (nbin <= 0) nbin = sfr::factor;
    auto esf_vec = esf(gray, fitme, nbin);
    if (esf_vec.empty()) return result;

    auto lsf_vec = lsf(esf_vec);
    if (lsf_vec.empty()) return result;

    auto mtf_vec = mtf(lsf_vec);
    if (mtf_vec.empty()) return result;

    // 4. 归一化 MTF10 / MTF50
    double mtf10_norm = mtf10(mtf_vec);
    double mtf50_norm = mtf50(mtf_vec);

    // 5. 物理频率轴 freq(i) = i / (del2 * nn)
    int    nn = static_cast<int>(esf_vec.size());
    double del2 = del / nbin;
    int    Nmtf = static_cast<int>(mtf_vec.size());

    result.freq.resize(Nmtf);
    result.sfr.resize(Nmtf);
    for (int i = 0; i < Nmtf; ++i) {
        double f = static_cast<double>(i) / (del2 * nn); // cy/pixel
        result.freq[i] = f;
        result.sfr[i] = mtf_vec[i];
    }

    // 6. 把归一化频率转成物理频率
    double fNyquist = 0.5 / del;   // cy/pixel
    result.mtf10_norm = mtf10_norm;
    result.mtf50_norm = mtf50_norm;
    result.mtf10_cypix = mtf10_norm * fNyquist;
    result.mtf50_cypix = mtf50_norm * fNyquist;

    return result;
}