#ifndef GENERAL_H
#define GENERAL_H
#include <vector>
#include <opencv2/opencv.hpp>

namespace sfr {
    const int factor = 4;
    const cv::Matx<double, 1, 3> kernel(-0.5, 0, 0.5);
    // 非对称 Hamming-type window，中心在 mid，行为对应 MATLAB ahamming
    std::vector<double> tukey(const int n0, const double mid);
    std::vector<double> tukey2(int n, double alpha, double mid);

    // 对称 Hamming 窗
    inline std::vector<double> hamming(const int n) { return tukey(n, (n + 1) / 2.0); }

    std::vector<double> center_shift(const std::vector<double>& x, const int center);
    std::vector<double> polyfit(const std::vector<double>& x, const std::vector<double>& y, int degree);
    std::vector<double> polyval(const std::vector<double>& x, const std::vector<double>& coeff);
    std::vector<double> fir2fix(int n, int m);

}
#endif //GENERAL_H
