// OpenCVHelper_test.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//
#include <chrono>
#include <iostream>
#include <opencv2/opencv.hpp>
#include <stack>

cv::Mat1d deriv1_like(const cv::Mat1d& a, const cv::Mat1d& fil)
{
    const int nlin = a.rows;
    const int npix = a.cols;

    // MATLAB conv 是真正的卷积，会翻转核
    // OpenCV filter2D 是相关，需要手动翻转
    cv::Mat1d flipped_fil;
    cv::flip(fil, flipped_fil, 1);  // 对于 1xN 的核，用 1（水平翻转）

    // 锚点应该在滤波器中心，这样才能匹配 MATLAB 的 'same' 行为
    // 对于 1x3 的核，中心是 (1, 0)，即 (col, row) 格式
    int anchor_x = flipped_fil.cols / 2;
    int anchor_y = flipped_fil.rows / 2;
    cv::Point anchor(anchor_x, anchor_y);

    // 使用 BORDER_CONSTANT 补零
    cv::Mat1d b;
    cv::filter2D(a, b, -1, flipped_fil, anchor, 0, cv::BORDER_CONSTANT);

    // 边缘修正（与原始 MATLAB 代码一致）
    if (npix >= 2) {
        for (int i = 0; i < nlin; ++i) {
            b.at<double>(i, 0) = b.at<double>(i, 1);
            b.at<double>(i, npix - 1) = b.at<double>(i, npix - 2);
        }
    }

    return b;
}

//int main()
//{
//    cv::Mat1d a = (cv::Mat1d(1, 5) << 1, 2, 3, 4, 5);
//    cv::Mat1d kernel = (cv::Mat1d(1, 3) << 0.5, 0, -0.5);
//    cv::Mat1d c;
//    c = deriv1_like(a, kernel);
//
//
//    std::chrono::steady_clock::time_point start, end;
//    std::chrono::microseconds duration;
//
//    cv::Mat image = cv::imread("C:\\Users\\Xin\\Desktop\\20250618184915_1_src.tif", cv::ImreadModes::IMREAD_UNCHANGED);
//
//    if (image.empty()) {
//        std::cerr << "无法读取图像文件！" << std::endl;
//        return -1;
//    }
//    start = std::chrono::high_resolution_clock::now();
//    cv::Mat image8bit;
//    image.convertTo(image8bit, CV_8UC3, 255.0 / 65535.0);
//
//    // Extract the blue channel
//    //std::vector<cv::Mat> channels;
//    //cv::split(image8bit, channels);
//    //cv::Mat gray = channels[0];
//
//    cv::Mat gray;
//    cv::cvtColor(image8bit, gray, cv::COLOR_BGR2GRAY);
//    //cv::extractChannel(image8bit, gray, 0); // 0 is the index for the blue channel
//
//   // 二值化
//
//   // 定义结构元素
//    cv::Mat binary;
//    cv::threshold(gray, binary, 20, 255, cv::THRESH_BINARY);
//
//    // 腐蚀操作
//    cv::erode(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(2, 2)));
//
//    cv::dilate(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(4, 4)));
//    cv::erode(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(2, 2)));
//
//
//    // 检测轮廓
//    std::vector<std::vector<cv::Point>> contours;
//    cv::findContours(binary, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);
//
//    std::vector<cv::Point> centers;
//
//    // 遍历轮廓
//    for (const auto& contour : contours) {
//        // 计算轮廓的边界框
//        cv::Rect boundingBox = cv::boundingRect(contour);
//
//        // 根据灯珠的已知大小过滤
//        if (boundingBox.width > 2 && boundingBox.height > 2 ) {
//
//            // 计算中心点
//            int cx = boundingBox.x + boundingBox.width / 2;
//            int cy = boundingBox.y + boundingBox.height / 2;
//            centers.push_back(cv::Point(cx, cy));
//        }
//        else {
//            int coutns = centers.size();
//        }
//    }
//
//    //总亮点
//    int coutns = centers.size();
//
//    // 计算中心点的凸包
//    std::vector<cv::Point> hull;
//    if (!centers.empty()) {
//        cv::convexHull(centers, hull);
//    }
//
//    //绘制中心点
//    for (const auto& center : centers) {
//        cv::circle(image8bit, center, 4, cv::Scalar(255), -1);
//    }
//
//    // 绘制凸包
//    if (!hull.empty()) {
//        for (size_t i = 0; i < hull.size(); ++i) {
//            cv::line(image8bit, hull[i], hull[(i + 1) % hull.size()], cv::Scalar(255), 2);
//        }
//    }
//
//    // 创建一个掩码，初始为全零
//    cv::Mat mask = cv::Mat::zeros(image.size(), CV_8UC1);
//
//    // 在掩码上绘制凸包区域
//    std::vector<std::vector<cv::Point>> hulls = { hull };
//    cv::fillPoly(mask, hulls, cv::Scalar(255));
//
//    // 遍历图像的所有点
//    for (int y = 0; y < binary.rows; ++y) {
//        uchar* maskRow = mask.ptr<uchar>(y);
//        uchar* imgRow = binary.ptr<uchar>(y);
//        for (int x = 0; x < binary.cols; ++x) {
//            // 如果掩码中该点不在凸包内，则设置为255
//            if (maskRow[x] == 0) {
//                imgRow[x] = 255;
//            }
//        }
//    }
//
//    int rows = 650;
//    int cols = 850;
//
//    //缺少的点
//    int black = rows * cols - centers.size();
//    std::cout << black << std::endl;
//
//
//    std::vector<std::vector<cv::Point>> ledMatrix1;
//    std::vector<cv::Point> currentRow;
//
//    cv::dilate(binary, binary, cv::getStructuringElement(cv::MORPH_RECT, cv::Size(12, 12)));
//
//    binary = 255 - binary;
//    std::vector<cv::Point> blackcenters;
//
//    std::vector<std::vector<cv::Point>> contourless;
//    cv::findContours(binary, contourless, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);
//
//    // 遍历轮廓
//    for (const auto& contour : contourless) {
//        // 计算轮廓的边界框
//        cv::Rect boundingBox = cv::boundingRect(contour);
//
//        // 根据灯珠的已知大小过滤
//        if (boundingBox.width > 2 && boundingBox.width < 20 &&
//            boundingBox.height > 2 && boundingBox.height < 20) {
//
//            // 计算中心点
//            int cx = boundingBox.x + boundingBox.width / 2;
//            int cy = boundingBox.y + boundingBox.height / 2;
//            blackcenters.push_back(cv::Point(cx, cy));
//        }
//    }
//    //缺少的点
//    std::cout << blackcenters.size() << std::endl;
//    for (const auto& contour : blackcenters)
//    {
//        //std::cout << contour << std::endl;
//        cv::circle(image8bit, contour, 5, cv::Scalar(0, 0, 255), 1);
//    }
//
//    end = std::chrono::high_resolution_clock::now();
//    duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
//    std::cout << ": " << duration.count() / 1000.0 << " 毫秒" << std::endl;
//
//    cv::imwrite("reee.tif", image8bit);
//
//
//    //cv::imshow("tif读", image);
//    cv::waitKey(0);
//}

