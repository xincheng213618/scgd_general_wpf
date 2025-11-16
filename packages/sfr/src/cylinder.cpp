#include <sfr/cylinder.h>
#include <sfr/general.h>

sfr::circle sfr::circle_fit(const cv::Mat &mat, int thresh) {
    cv::Mat mask = cv::Mat::zeros(mat.size(), CV_8UC1);
    cv::threshold(mat, mask, thresh, 255, cv::THRESH_BINARY);
    std::vector<std::vector<cv::Point>> contours;
    cv::findContours(mask, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);
    auto circle_contour = *std::max_element(contours.begin(), contours.end(),[](auto& l, auto& r){ return l.size() < r.size();});
    auto circle = cv::fitEllipse(circle_contour);
    auto [cx, cy] = circle.center;
    float r = (circle.size.width + circle.size.height) / 4;
    return {cx, cy, r};
}

std::vector<cv::Point2d> sfr::esf(const cv::Mat &mat, const sfr::circle &cir, float roi, float binsize, int n_fit) {
    const auto [cx, cy, r] = cir;
    std::vector<cv::Point2d> esf_temp, esf;
    for (int i = 0; i < mat.rows; i++) {
        auto *ptr = mat.ptr<uchar>(i);
        for (int j = 0; j < mat.cols; j++) {
            const float dist = std::hypot(j - cx, i - cy) - r;
            if (std::abs(dist) < roi) {
                esf_temp.emplace_back(dist, ptr[j]);
            }
        }
    }
    std::sort(esf_temp.begin(), esf_temp.end(), [](cv::Point2d lp, cv::Point2d rp){ return lp.x < rp.x;});
    double next = esf_temp[0].x + binsize;
    double d_sum = 0, v_sum = 0;
    int cnt = 0;
    for (const auto [d, v]: esf_temp) {
        if (d < next) {
            d_sum += d; v_sum += v; cnt += 1;
        } else {
            esf.emplace_back(d_sum/cnt, v_sum/cnt);
            next += binsize;
            d_sum = d; v_sum = v; cnt = 1;
        }
    }

    for (int i = 0; i < esf.size(); i += n_fit) {
        int start = std::max(0, i), end = std::min((int)esf.size(), i + n_fit);
        if (end == esf.size()) break;
        std::vector<double> x(n_fit), y(n_fit), y_fit(n_fit), coeff(4);
        for (int j = start; j < end; j++) {
            x[j - start] = esf[j].x;
            y[j - start] = esf[j].y;
        }
        coeff = sfr::polyfit(x, y, 3); // a0 + a1*x + a2*x^2 + a3*x^3
        y_fit = sfr::polyval(x, coeff);
        esf[i + n_fit/2].y = y_fit[n_fit/2];
    }

    // 杯状伪影导致边缘亮度过高, 目前采取将边缘内测的亮度设为和最大值一样, 保证灰度值只在中间区域变化
    auto peak = std::max_element(esf.begin(), esf.end(), [](cv::Point2d lp, cv::Point2d rp){ return lp.y < rp.y;});
    double peak_val = peak->y;
    int peak_x = std::distance(esf.begin(), peak);
    for (int i = 0; i < peak_x; i++) esf[i].y = peak_val;
    return esf;
}

std::vector<cv::Point2d> sfr::lsf(const std::vector<cv::Point2d> &esf, int n_fit) {
    int size = esf.size();
    std::vector<cv::Point2d> lsf;
    for (int i = 0; i < esf.size(); i += n_fit) {
        int start = std::max(0, i), end = std::min(size, i + n_fit);
        if (end == size) break;
        std::vector<double> x(n_fit), y(n_fit), y_fit(n_fit), coeff(4);
        for (int j = start; j < end; j++) {
            x[j - start] = esf[j].x;
            y[j - start] = esf[j].y;
        }
        coeff = sfr::polyfit(x, y, 3); // a0 + a1*x + a2*x^2 + a3*x^3
        y_fit = sfr::polyval(x, { coeff[1], 2 * coeff[2], 3 * coeff[3]});
        lsf.emplace_back(esf[i + n_fit/2].x, y_fit[n_fit/2]);
    }
    double max = std::max_element(lsf.begin(), lsf.end(),
        [](cv::Point2d lp, cv::Point2d rp){ return std::abs(lp.y) < std::abs(rp.y);})->y;
    for (auto &val: lsf) val.y /= max;
    return lsf;
}

std::vector<cv::Point2d> sfr::mtf(const std::vector<cv::Point2d> &lsf, double ratio) {
    int n = lsf.size();
    std::vector<double> lsf_y(n);
    for (int i = 0; i < n; i++) lsf_y[i] = lsf[i].y;

    std::vector<std::complex<double>> fft;
    cv::dft(lsf_y, fft, cv::DFT_COMPLEX_OUTPUT);
    const double DC = fft[0].real();
    std::vector<cv::Point2d> mtf(lsf.size() / 2);
    for (size_t i = 0; i < mtf.size(); i++) mtf[i] = { 1 / ratio * i / n, std::abs(fft[i]) / DC};
    return mtf;
}

double sfr::mtf10(const std::vector<cv::Point2d> &mtf) {
    auto it = std::adjacent_find(mtf.begin(), mtf.end(),
        [](auto& lop, auto& rop){ return lop.y > 0.1 && rop.y < 0.1;});
    auto [x1, y1] = *it;
    auto [x2, y2] = *(it + 1);
    double mtf10 = (0.1 - y1) / (y2 - y1) * (x2 - x1) + x1;
    return mtf10;
}


