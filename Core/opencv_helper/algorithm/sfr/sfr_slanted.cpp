#include "sfr_slanted.h"
#include "sfr_base.h"
#include <Eigen/Dense>
#include <numeric>

namespace cvcore {
namespace sfr {

// Strictly corresponds to MATLAB rotatev2 for single channel (mm = 1 case)
cv::Mat auto_rotate_vertical(const cv::Mat& src) {
    cv::Mat a;
    if (src.channels() == 1) {
        src.convertTo(a, CV_64F);
    }
    else {
        std::vector<cv::Mat> channels;
        cv::split(src, channels);
        channels[1].convertTo(a, CV_64F);
    }

    int nlin = a.rows;
    int npix = a.cols;

    int nn = 3;
    if (nlin < nn || npix < nn) {
        return src.clone();
    }

    // MATLAB: testv = abs(mean(a(end-nn,:,mm))-mean(a(nn,:,mm)));
    double v_top = cv::mean(a.row(nn - 1))[0];
    double v_bottom = cv::mean(a.row(nlin - nn - 1))[0];
    double testv = std::abs(v_bottom - v_top);

    // MATLAB: testh = abs(mean(a(:,end-nn,mm))-mean(a(:,nn,mm)));
    double h_left = cv::mean(a.col(nn - 1))[0];
    double h_right = cv::mean(a.col(npix - nn - 1))[0];
    double testh = std::abs(h_right - h_left);

    cv::Mat out;
    if (testv > testh) {
        cv::rotate(src, out, cv::ROTATE_90_COUNTERCLOCKWISE);
    }
    else {
        out = src.clone();
    }

    return out;
}

// Calculate centroid of windowed derivative (column direction)
// Corresponds to MATLAB: centroid(c.*win) - 0.5
double row_centroid(const cv::Mat1d& row, const std::vector<double>& win) {
    const int n = row.cols;
    if (win.size() != n) {
        throw std::runtime_error("Row and window sizes do not match.");
    }

    double num = 0.0;
    double den = 0.0;

    for (int j = 0; j < n; ++j) {
        double v = row.at<double>(0, j) * win[j];
        num += v * (j + 1);
        den += v;
    }

    if (den == 0.0) {
        return 0.0;
    }

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

// Binomial coefficient n choose k
long long nchoosek(int n, int k) {
    if (k < 0 || k > n) return 0;
    if (k == 0 || k == n) return 1;
    if (k > n / 2) k = n - k;

    long long res = 1;
    for (int i = 1; i <= k; ++i) {
        res = res * (n - i + 1) / i;
    }
    return res;
}

// Convert scaled polynomial coefficients to unscaled
std::vector<double> polyfit_convert_cpp(const std::vector<double>& p_scaled_asc, double m, double s) {
    if (s == 0.0) {
        throw std::runtime_error("Standard deviation cannot be zero.");
    }

    const int degree = static_cast<int>(p_scaled_asc.size()) - 1;
    if (degree < 0) return {};

    std::vector<double> p_unscaled(degree + 1, 0.0);

    for (int i = 0; i <= degree; ++i) {
        double p_i = p_scaled_asc[i];
        double s_pow_i = std::pow(s, i);

        for (int j = 0; j <= i; ++j) {
            double contribution = p_i / s_pow_i * nchoosek(i, j) * std::pow(-m, i - j);
            p_unscaled[j] += contribution;
        }
    }

    return p_unscaled;
}

// Fit loc(line) vs line number relationship
std::vector<double> edge_polyfit(const std::vector<double>& loc, int degree) {
    const int nlin = static_cast<int>(loc.size());
    std::vector<double> x(nlin);
    std::vector<double> x_scaled(nlin);

    for (int i = 0; i < nlin; ++i) x[i] = static_cast<double>(i);

    double mu1_mean = mean(x);
    double mu2_stddev = stddev(x, mu1_mean);

    for (int i = 0; i < nlin; ++i) {
        x_scaled[i] = (x[i] - mu1_mean) / mu2_stddev;
    }

    std::vector<double> p_scaled = polyfit(x_scaled, loc, degree);
    std::vector<double> p_final = polyfit_convert_cpp(p_scaled, mu1_mean, mu2_stddev);

    return p_final;
}

// Equivalent to MATLAB: b = deriv1(a, nlin, npix, fil)
cv::Mat1d deriv1_like(const cv::Mat1d& a, const cv::Mat1d& fil) {
    const int nlin = a.rows;
    const int npix = a.cols;

    cv::Mat1d flipped_fil;
    cv::flip(fil, flipped_fil, -1);

    cv::Point anchor(0, 0);
    cv::Mat1d b;
    cv::filter2D(a, b, -1, flipped_fil, anchor, 0, cv::BORDER_CONSTANT);

    if (npix >= 2) {
        for (int i = 0; i < nlin; ++i) {
            b.at<double>(i, 0) = b.at<double>(i, 1);
            b.at<double>(i, npix - 1) = b.at<double>(i, npix - 2);
        }
    }

    return b;
}

// Polynomial edge fitting for single channel ROI
std::vector<double> poly_edge_fit(const cv::Mat& mat, int npol, std::vector<double>* loc_out) {
    CV_Assert(mat.channels() == 1);
    const int nlin = mat.rows;
    const int npix = mat.cols;

    cv::Mat1d a;
    mat.convertTo(a, CV_64F);

    double tleft = cv::sum(a.colRange(0, 5))[0];
    double tright = cv::sum(a.colRange(npix - 5, npix))[0];

    cv::Mat1d fil1(1, 2);
    fil1(0, 0) = 0.5;
    fil1(0, 1) = -0.5;
    if (tleft > tright) {
        fil1 = -fil1;
    }

    cv::Mat1d deriv = deriv1_like(a, fil1);

    double alpha = 1.0;

    // First pass: symmetric Hamming window, rough loc
    auto win1 = tukey2(npix, alpha, (npix + 1) / 2.0);
    for (double& v : win1) v = 0.95 * v + 0.05;

    std::vector<double> loc(nlin, 0.0);

    for (int i = 0; i < nlin; ++i) {
        cv::Mat1d row = deriv.row(i);
        loc[i] = row_centroid(row, win1);
    }

    auto coeff = edge_polyfit(loc, npol);

    // Second pass: use fitted position for asymmetric window
    for (int i = 0; i < nlin; ++i) {
        double place = polyval(std::vector<double>{static_cast<double>(i)}, coeff)[0];
        auto win2 = tukey2(npix, alpha, place);
        for (double& v : win2) v = 0.95 * v + 0.05;

        cv::Mat1d row = deriv.row(i);
        loc[i] = row_centroid(row, win2);
    }

    coeff = edge_polyfit(loc, npol);

    if (loc_out) *loc_out = loc;
    return coeff;
}

std::tuple<double, double> centroid_fit(const cv::InputArray img) {
    cv::Mat mat = img.getMat();
    std::vector<cv::Point2d> edge;
    for (int i = 0; i < mat.rows; ++i) {
        const double* ptr = mat.ptr<double>(i);
        double sum = 0, sum_y = 0;
        for (int j = 0; j < mat.cols; ++j) {
            sum += ptr[j];
            sum_y += ptr[j] * j;
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

std::tuple<double, double> line_fit(const cv::Mat& mat) {
    cv::Mat img = mat.clone();
    cv::Mat derivate(img.size(), CV_64F), hammed(img.size(), CV_64F);
    cv::filter2D(img, derivate, CV_64F, DEFAULT_KERNEL);
    std::vector<double> w = hamming(mat.cols);
    for (int i = 0; i < mat.rows; ++i)
        hammed.row(i) = derivate.row(i).mul(w);

    auto [k0, b0] = centroid_fit(hammed);

    for (int i = 0; i < mat.rows; ++i) {
        double place = k0 * i + b0;
        w = tukey(mat.cols, place);
        hammed.row(i) = derivate.row(i).mul(w);
    }
    return centroid_fit(hammed);
}

std::vector<double> esf(const cv::Mat& mat, const std::vector<double>& fitme, int nbin) {
    CV_Assert(mat.channels() == 1);
    const int nlin = mat.rows;
    const int npix = mat.cols;
    if (nbin <= 0) nbin = DEFAULT_FACTOR;

    std::vector<double> p2(nlin, 0.0);
    for (int m = 0; m < nlin; ++m) {
        double y = static_cast<double>(m);
        double val = polyval(std::vector<double>{y}, fitme)[0];
        val -= fitme[0];
        p2[m] = val;
    }

    const int nn = static_cast<int>(std::floor(npix * nbin));
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

    int start = 1 + static_cast<int>(std::round(0.5 * del));
    for (int i = start; i < start + nn && i < bwidth; ++i) {
        if (cnt[i] == 0.0) {
            int i1 = std::max(start, i - 1);
            int i2 = std::min(start + nn - 1, i + 1);
            cnt[i] = 0.5 * (cnt[i1] + cnt[i2]);
            acc[i] = 0.5 * (acc[i1] + acc[i2]);
        }
    }

    std::vector<double> esf(nn, 0.0);
    for (int i = 0; i < nn; ++i) {
        int idx = start + i;
        if (idx >= 0 && idx < bwidth && cnt[idx] > 0.0) {
            esf[i] = acc[idx] / cnt[idx];
        }
    }
    return esf;
}

cv::Mat1d deriv2_like(const cv::Mat1d& a, const cv::Mat1d& fil) {
    const int nlin = a.rows;
    const int npix = a.cols;

    cv::Mat1d flipped_fil;
    cv::flip(fil, flipped_fil, 1);

    int anchor_x = flipped_fil.cols / 2;
    int anchor_y = flipped_fil.rows / 2;
    cv::Point anchor(anchor_x, anchor_y);

    cv::Mat1d b;
    cv::filter2D(a, b, -1, flipped_fil, anchor, 0, cv::BORDER_CONSTANT);

    if (npix >= 2) {
        for (int i = 0; i < nlin; ++i) {
            b.at<double>(i, 0) = b.at<double>(i, 1);
            b.at<double>(i, npix - 1) = b.at<double>(i, npix - 2);
        }
    }

    return b;
}

std::vector<double> lsf(const std::vector<double>& esf) {
    const int n = static_cast<int>(esf.size());

    cv::Mat1d esfMat(1, n);
    for (int i = 0; i < n; ++i) esfMat(0, i) = esf[i];

    cv::Mat1d kernel = (cv::Mat1d(1, 3) << -0.5, 0, 0.5);
    cv::Mat1d diffMat = deriv2_like(esfMat, kernel);

    std::vector<double> diff(n);
    for (int i = 0; i < n; ++i) diff[i] = diffMat(0, i);

    if (diff[0] == 0.0) diff[0] = diff[1];
    else if (diff[n - 1] == 0.0) diff[n - 1] = diff[n - 2];

    double maxVal = 0.0;
    int maxIdx = 0;
    for (int i = 0; i < n; ++i) {
        if (std::abs(diff[i]) > std::abs(maxVal)) {
            maxVal = diff[i];
            maxIdx = i;
        }
    }

    diff = center_shift(diff, maxIdx + 1);

    double newMm = n / 2.0;
    auto win = tukey2(n, 1.0, newMm);

    std::vector<double> lsf(n);
    for (int i = 0; i < n; ++i) {
        lsf[i] = win[i] * diff[i];
    }
    return lsf;
}

std::vector<double> mtf(const std::vector<double>& lsf) {
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

    const double DC = std::abs(fft[0]);
    const int nn2 = nn / 2 + 1;

    auto corr = fir2fix(nn2, 3);

    std::vector<double> mtf(nn2, 0.0);
    for (int i = 0; i < nn2; ++i) {
        double raw = std::abs(fft[i]) / DC;
        mtf[i] = raw * corr[i];
    }
    return mtf;
}

double mtf10(const std::vector<double>& mtf) {
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

double mtf50(const std::vector<double>& mtf) {
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
        return 0.0;
    }

    auto it = std::adjacent_find(sfr_data.begin(), sfr_data.end(),
        [=](double y1, double y2) { return y1 >= threshold && y2 < threshold; });

    if (it == sfr_data.end()) {
        return 0.0;
    }

    int i = std::distance(sfr_data.begin(), it);
    double y1 = sfr_data[i];
    double y2 = sfr_data[i + 1];
    double x1 = freq_axis[i];
    double x2 = freq_axis[i + 1];

    if (std::abs(y2 - y1) < 1e-9) {
        return x1;
    }
    return x1 + (threshold - y1) * (x2 - x1) / (y2 - y1);
}

SFRResult calculateSlantedEdgeSFR(const cv::Mat& imgIn,
                                   double del,
                                   int npol,
                                   int nbin,
                                   double vslope)
{
    SFRResult result;
    if (imgIn.empty()) return result;

    cv::Mat gray = auto_rotate_vertical(imgIn.clone());

    std::vector<double> loc;
    auto fitme = poly_edge_fit(gray, npol, &loc);
    auto fitme1 = edge_polyfit(loc, 1);
    if (vslope == -1) {
        vslope = fitme1[1];
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

    double delimage = del;
    double delfac = std::cos(std::atan(vslope));
    double del_image = del;
    del *= delfac;
    double del2 = del / nbin;

    if (nbin <= 0) nbin = DEFAULT_FACTOR;
    auto esf_vec = esf(gray, fitme, nbin);
    if (esf_vec.empty()) return result;

    auto lsf_vec = lsf(esf_vec);
    if (lsf_vec.empty()) return result;

    auto mtf_vec = mtf(lsf_vec);

    int nn = static_cast<int>(esf_vec.size());
    int nn2 = nn / 2 + 1;
    double freqlim = 1.0;
    int nn2out = static_cast<int>(std::round(nn2 * freqlim / 2.0));

    result.freq.resize(nn2out);
    result.sfr.resize(nn2out);
    for (int i = 0; i < nn2out; ++i) {
        double f = static_cast<double>(i) / (del2 * nn);
        result.freq[i] = f;
        result.sfr[i] = mtf_vec[i];
    }

    double mtf10_cypix = find_freq_at_threshold(result.freq, result.sfr, 0.1);
    double mtf50_cypix = find_freq_at_threshold(result.freq, result.sfr, 0.5);
    double hs = 0.495 / delimage;
    double fNyquist = 0.5 / delimage;

    result.mtf10_cypix = std::min(mtf10_cypix, hs);
    result.mtf50_cypix = std::min(mtf50_cypix, hs);
    result.mtf10_norm = (fNyquist > 0) ? result.mtf10_cypix / fNyquist : 0.0;
    result.mtf50_norm = (fNyquist > 0) ? result.mtf50_cypix / fNyquist : 0.0;

    return result;
}

} // namespace sfr
} // namespace cvcore