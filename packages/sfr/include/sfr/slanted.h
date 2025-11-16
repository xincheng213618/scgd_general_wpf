#ifndef SLANTED_H
#define SLANTED_H
#include <tuple>
#include <opencv2/opencv.hpp>

namespace sfr {
    cv::Mat auto_rotate_vertical(const cv::Mat& src);
    std::tuple<double, double> line_fit(const cv::Mat& mat);
    // 新增：多项式边缘拟合
    std::vector<double> poly_edge_fit(const cv::Mat& mat,  int npol, std::vector<double>* loc_out = nullptr);
    std::vector<double> esf(const cv::Mat& mat, const std::vector<double>& fitme, int nbin);
    std::vector<double> lsf(const std::vector<double>& esf);
    std::vector<double> mtf(const std::vector<double>& lsf);
    std::vector<double> fir2fix(int n, int m);
    double mtf10(const std::vector<double>& mtf);
    double mtf50(const std::vector<double>& mtf);

    struct SFRResult {
        std::vector<double> freq;   // 物理频率 (cy/pixel)
        std::vector<double> sfr;    // SFR 值
        double mtf10_norm;          // 归一化频率下的 MTF10 (0~1)
        double mtf50_norm;          // 归一化频率下的 MTF50 (0~1)
        double mtf10_cypix;         // MTF10 物理频率 (cy/pixel)
        double mtf50_cypix;         // MTF50 物理频率 (cy/pixel)
    };

    SFRResult CalSFR(const cv::Mat& img,
        double del = 1.0,
        int    npol = 5,
        int    nbin = 4);


}

#endif //SLANTED_H
