#include "sfr_cylinder.h"
#include "sfr_base.h"
#include <algorithm>

namespace cvcore {
namespace sfr {

Circle fitCircle(const cv::Mat& mat, int thresh) {
    cv::Mat mask = cv::Mat::zeros(mat.size(), CV_8UC1);
    cv::threshold(mat, mask, thresh, 255, cv::THRESH_BINARY);

    std::vector<std::vector<cv::Point>> contours;
    cv::findContours(mask, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

    auto circle_contour = *std::max_element(contours.begin(), contours.end(),
        [](auto& l, auto& r) { return l.size() < r.size(); });

    auto circle = cv::fitEllipse(circle_contour);
    auto [cx, cy] = circle.center;
    float r = (circle.size.width + circle.size.height) / 4.0f;

    return { static_cast<float>(cx), static_cast<float>(cy), r };
}

std::vector<cv::Point2d> cylinderESF(const cv::Mat& mat, const Circle& cir,
                                      float roi, float binsize, int n_fit) {
    const auto [cx, cy, r] = cir;
    std::vector<cv::Point2d> esf_temp, esf;

    for (int i = 0; i < mat.rows; i++) {
        auto* ptr = mat.ptr<uchar>(i);
        for (int j = 0; j < mat.cols; j++) {
            const float dist = std::hypot(j - cx, i - cy) - r;
            if (std::abs(dist) < roi) {
                esf_temp.emplace_back(dist, ptr[j]);
            }
        }
    }

    std::sort(esf_temp.begin(), esf_temp.end(),
        [](cv::Point2d lp, cv::Point2d rp) { return lp.x < rp.x; });

    double next = esf_temp[0].x + binsize;
    double d_sum = 0, v_sum = 0;
    int cnt = 0;

    for (const auto [d, v] : esf_temp) {
        if (d < next) {
            d_sum += d;
            v_sum += v;
            cnt += 1;
        } else {
            esf.emplace_back(d_sum / cnt, v_sum / cnt);
            next += binsize;
            d_sum = d;
            v_sum = v;
            cnt = 1;
        }
    }

    // Polynomial fitting for smoothing
    for (int i = 0; i < static_cast<int>(esf.size()); i += n_fit) {
        int start = std::max(0, i);
        int end = std::min(static_cast<int>(esf.size()), i + n_fit);
        if (end == static_cast<int>(esf.size())) break;

        std::vector<double> x(n_fit), y(n_fit), y_fit(n_fit), coeff(4);
        for (int j = start; j < end; j++) {
            x[j - start] = esf[j].x;
            y[j - start] = esf[j].y;
        }
        coeff = polyfit(x, y, 3);
        y_fit = polyval(x, coeff);
        esf[i + n_fit / 2].y = y_fit[n_fit / 2];
    }

    // Handle cup artifact: set inner edge brightness to maximum
    auto peak = std::max_element(esf.begin(), esf.end(),
        [](cv::Point2d lp, cv::Point2d rp) { return lp.y < rp.y; });
    double peak_val = peak->y;
    int peak_x = static_cast<int>(std::distance(esf.begin(), peak));
    for (int i = 0; i < peak_x; i++) esf[i].y = peak_val;

    return esf;
}

std::vector<cv::Point2d> cylinderLSF(const std::vector<cv::Point2d>& esf, int n_fit) {
    int size = static_cast<int>(esf.size());
    std::vector<cv::Point2d> lsf;

    for (int i = 0; i < size; i += n_fit) {
        int start = std::max(0, i);
        int end = std::min(size, i + n_fit);
        if (end == size) break;

        std::vector<double> x(n_fit), y(n_fit), y_fit(n_fit), coeff(4);
        for (int j = start; j < end; j++) {
            x[j - start] = esf[j].x;
            y[j - start] = esf[j].y;
        }
        coeff = polyfit(x, y, 3);
        y_fit = polyval(x, { coeff[1], 2 * coeff[2], 3 * coeff[3] });
        lsf.emplace_back(esf[i + n_fit / 2].x, y_fit[n_fit / 2]);
    }

    double max_val = std::max_element(lsf.begin(), lsf.end(),
        [](cv::Point2d lp, cv::Point2d rp) {
            return std::abs(lp.y) < std::abs(rp.y);
        })->y;

    for (auto& val : lsf) val.y /= max_val;
    return lsf;
}

std::vector<cv::Point2d> cylinderMTF(const std::vector<cv::Point2d>& lsf, double ratio) {
    int n = static_cast<int>(lsf.size());
    std::vector<double> lsf_y(n);
    for (int i = 0; i < n; i++) lsf_y[i] = lsf[i].y;

    std::vector<std::complex<double>> fft;
    cv::dft(lsf_y, fft, cv::DFT_COMPLEX_OUTPUT);
    const double DC = fft[0].real();

    std::vector<cv::Point2d> mtf(lsf.size() / 2);
    for (size_t i = 0; i < mtf.size(); i++) {
        mtf[i] = { 1 / ratio * static_cast<double>(i) / n, std::abs(fft[i]) / DC };
    }
    return mtf;
}

double cylinderMTF10(const std::vector<cv::Point2d>& mtf) {
    auto it = std::adjacent_find(mtf.begin(), mtf.end(),
        [](auto& lop, auto& rop) { return lop.y > 0.1 && rop.y < 0.1; });

    if (it == mtf.end() || (it + 1) == mtf.end()) return 0.0;

    auto [x1, y1] = *it;
    auto [x2, y2] = *(it + 1);
    double mtf10 = (0.1 - y1) / (y2 - y1) * (x2 - x1) + x1;
    return mtf10;
}

CylinderSFRResult calculateCylinderSFR(const cv::Mat& mat,
                                        int thresh,
                                        float roi,
                                        float binsize,
                                        int n_fit) {
    CylinderSFRResult result;

    if (mat.empty()) return result;

    result.circle = fitCircle(mat, thresh);
    result.esf = cylinderESF(mat, result.circle, roi, binsize, n_fit);
    result.lsf = cylinderLSF(result.esf, n_fit);
    result.mtf = cylinderMTF(result.lsf, binsize);
    result.mtf10 = cylinderMTF10(result.mtf);

    // Calculate MTF50 if possible
    auto it = std::adjacent_find(result.mtf.begin(), result.mtf.end(),
        [](auto& lop, auto& rop) { return lop.y > 0.5 && rop.y < 0.5; });
    if (it != result.mtf.end() && (it + 1) != result.mtf.end()) {
        auto [x1, y1] = *it;
        auto [x2, y2] = *(it + 1);
        result.mtf50 = (0.5 - y1) / (y2 - y1) * (x2 - x1) + x1;
    }

    return result;
}

} // namespace sfr
} // namespace cvcore