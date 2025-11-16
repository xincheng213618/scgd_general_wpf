#ifndef MTF_CYLINDER_H
#define MTF_CYLINDER_H

#include <opencv2/opencv.hpp>

namespace sfr {
    struct circle { float cx, cy, r; };
    circle circle_fit(const cv::Mat& mat, int thresh=80);
    std::vector<cv::Point2d> esf(const cv::Mat& mat, const circle& cir, float roi = 15.0, float binsize=0.032, int n_fit = 25);
    std::vector<cv::Point2d> lsf(const std::vector<cv::Point2d>& esf, int n_fit = 25);
    std::vector<cv::Point2d> mtf(const std::vector<cv::Point2d>& lsf, double ratio);
    double mtf10(const std::vector<cv::Point2d>& mtf);
}

#endif //MTF_CYLINDER_H
