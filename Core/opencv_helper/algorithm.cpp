
#include "pch.h"
#include "algorithm.h"
#include <iostream>  
#include <opencv2/core/core.hpp>  
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/imgproc/imgproc.hpp>
#include "spdlog/spdlog.h"

#include <vector>
#include <algorithm>
#include <ctime>
using namespace cv;

cv::Mat removeMoire(const cv::Mat& image) {
    // Step 1: Apply Gaussian Blur
    cv::Mat blurred;
    cv::GaussianBlur(image, blurred, cv::Size(5, 5), 0);

    // Step 2: Downsample the image
    cv::Mat downsampled;
    cv::pyrDown(blurred, downsampled);

    // Step 3: Apply Gaussian Blur again
    cv::Mat blurredAgain;
    cv::GaussianBlur(downsampled, blurredAgain, cv::Size(5, 5), 0);

    // Step 4: Upsample the image
    cv::Mat upsampled;
    cv::pyrUp(blurredAgain, upsampled, image.size());

    // Step 5: Sharpen the image
    cv::Mat sharpened;
    cv::addWeighted(image, 1.5, upsampled, -0.5, 0, sharpened);

    return sharpened;
}


void LampBeadDetection(cv::Mat image, int rows, int cols)
{
    cv::Mat image8bit;
    image.convertTo(image8bit, CV_8UC3, 255.0 / 65535.0);

    cv::Mat gray;
    cv::cvtColor(image8bit, gray, cv::COLOR_BGR2GRAY);

    // 定义结构元素
    cv::Mat binary;
    cv::threshold(gray, binary, 20, 255, cv::THRESH_BINARY);


    // 腐蚀操作
    cv::erode(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(2, 2)));

    cv::dilate(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(4, 4)));
    cv::erode(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(2, 2)));

    // 检测轮廓
    std::vector<std::vector<cv::Point>> contours;
    cv::findContours(binary, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

    std::vector<cv::Point> centers;

    // 遍历轮廓
    for (const auto& contour : contours) {
        // 计算轮廓的边界框
        cv::Rect boundingBox = cv::boundingRect(contour);

        // 根据灯珠的已知大小过滤
        if (boundingBox.width > 2 && boundingBox.width < 20 &&
            boundingBox.height > 2 && boundingBox.height < 20) {

            // 计算中心点
            int cx = boundingBox.x + boundingBox.width / 2;
            int cy = boundingBox.y + boundingBox.height / 2;
            centers.push_back(cv::Point(cx, cy));
        }
        else {
            size_t coutns = centers.size();
        }
    }

    //总亮点
    size_t coutns = centers.size();

    // 计算中心点的凸包
    std::vector<cv::Point> hull;
    if (!centers.empty()) {
        cv::convexHull(centers, hull);
    }

    //绘制中心点
    for (const auto& center : centers) {
        cv::circle(image8bit, center, 4, cv::Scalar(255), -1);
    }

    // 绘制凸包
    if (!hull.empty()) {
        for (size_t i = 0; i < hull.size(); ++i) {
            cv::line(image8bit, hull[i], hull[(i + 1) % hull.size()], cv::Scalar(255), 2);
        }
    }

    // 计算凸包的面积
    double area = cv::contourArea(hull);
    std::cout << "Convex Hull Area: " << area << std::endl;

    // 计算凸包的边界矩形
    cv::Rect boundingRect = cv::boundingRect(hull);
    double width = boundingRect.width;
    double height = boundingRect.height;

    double singlewith = width / 850;
    double singleheight = height / 650;

    // 创建一个掩码，初始为全零
    cv::Mat mask = cv::Mat::zeros(image.size(), CV_8UC1);

    // 在掩码上绘制凸包区域
    std::vector<std::vector<cv::Point>> hulls = { hull };
    cv::fillPoly(mask, hulls, cv::Scalar(255));

    // 遍历图像的所有点
    for (int y = 0; y < binary.rows; ++y) {
        uchar* maskRow = mask.ptr<uchar>(y);
        uchar* imgRow = binary.ptr<uchar>(y);
        for (int x = 0; x < binary.cols; ++x) {
            // 如果掩码中该点不在凸包内，则设置为255
            if (maskRow[x] == 0) {
                imgRow[x] = 255;
            }
        }
    }

    //缺少的点
    size_t black = rows * cols - centers.size();
    std::cout << black << std::endl;


    std::vector<std::vector<cv::Point>> ledMatrix1;
    std::vector<cv::Point> currentRow;

    cv::dilate(binary, binary, cv::getStructuringElement(cv::MORPH_RECT, cv::Size(12, 12)));

    binary = 255 - binary;
    std::vector<cv::Point> blackcenters;

    std::vector<std::vector<cv::Point>> contourless;
    cv::findContours(binary, contourless, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

    // 遍历轮廓
    for (const auto& contour : contourless) {
        // 计算轮廓的边界框
        cv::Rect boundingBox = cv::boundingRect(contour);

        // 根据灯珠的已知大小过滤
        if (boundingBox.width > 2 && boundingBox.width < 20 &&
            boundingBox.height > 2 && boundingBox.height < 20) {

            // 计算中心点
            int cx = boundingBox.x + boundingBox.width / 2;
            int cy = boundingBox.y + boundingBox.height / 2;
            blackcenters.push_back(cv::Point(cx, cy));
        }
        else
        {
            // 计算凸包的边界矩形
            cv::Rect boundingRect = cv::boundingRect(contour);

            // 点的间隔和起始偏移
            int offset = 4;

            // 存储在凸包内的点
            std::vector<cv::Point> pointsInsideHull;

            // 在边界矩形内迭代
            for (double y = boundingRect.y + offset; y < boundingRect.y + boundingRect.height; y += singlewith) {
                for (double x = boundingRect.x + offset; x < boundingRect.x + boundingRect.width; x += singleheight) {
                    cv::Point p(x, y);
                    // 检查点是否在凸包内
                    if (cv::pointPolygonTest(contour, p, false) >= 0) {
                        blackcenters.push_back(p);
                    }
                }
            }

        }
    }

    //缺少的点
    std::cout << blackcenters.size() << std::endl;
    for (const auto& contour : blackcenters)
    {
        std::cout << contour << std::endl;
        cv::circle(image8bit, contour, 5, cv::Scalar(0, 0, 255), 1);
    }
    std::cout << blackcenters.size() << std::endl;

}

// 返回0正常，-1图像空，-2未找到发光区
// points 返回发光区四个角点（顺序为 minAreaRect 顺序）
int findLuminousAreaCorners(cv::Mat& src, std::vector<cv::Point2f>& points, int threshold)
{
    points.clear();
    if (src.empty()) return -1;

    Mat gray;
    if (src.channels() != 1)
        cvtColor(src, gray, COLOR_BGR2GRAY);
    else
        gray = src;
    if (gray.depth() == CV_16U)
        normalize(gray, gray, 0, 255, NORM_MINMAX, CV_8U);

    GaussianBlur(gray, gray, Size(5, 5), 0);
    Mat thresh;
    cv::threshold(gray, thresh, threshold, 255, THRESH_BINARY);

    Mat kernel = getStructuringElement(MORPH_RECT, Size(3, 3));
    dilate(thresh, thresh, kernel);

    std::vector<std::vector<Point>> contours;
    findContours(thresh, contours, RETR_EXTERNAL, CHAIN_APPROX_SIMPLE);

    // 去除贴边的
    contours.erase(remove_if(contours.begin(), contours.end(),
        [&](const std::vector<Point>& contour) {
            Rect rect = boundingRect(contour);
            return rect.x == 0 || rect.y == 0 ||
                rect.x + rect.width == src.cols ||
                rect.y + rect.height == src.rows;
        }), contours.end());

    if (contours.empty()) return -2;

    // 找最大面积轮廓
    size_t maxIdx = 0;
    double maxArea = 0;
    for (size_t i = 0; i < contours.size(); ++i) {
        double area = contourArea(contours[i]);
        if (area > maxArea) {
            maxArea = area;
            maxIdx = i;
        }
    }

    std::vector<cv::Point> approx;
    double peri = cv::arcLength(contours[maxIdx], true);
    // 0.02 * peri 可调整，通常 0.01~0.05 之间
    cv::approxPolyDP(contours[maxIdx], approx, 0.02 * peri, true);

    if (approx.size() == 4) {
        // 这就是你要的四边形4个角点
        for (int i = 0; i < 4; ++i)
            points.push_back(cv::Point2f(approx[i]));
        return 0;
    }
    else {

        // 最小外接矩形
        RotatedRect minRect = minAreaRect(contours[maxIdx]);
        Point2f verts[4];
        minRect.points(verts);
        points.assign(verts, verts + 4);
    }

    return 0;
}


int findLuminousArea(cv::Mat& src, cv::Rect& largestRect,int threshold)
{
    // 检查输入图像是否为空
    if (src.empty()) {
        return -1;
    }
    Mat gray;
    if (src.channels() != 1) {
        // 转换为灰度图
        cvtColor(src, gray, COLOR_BGR2GRAY);
    }
    else {
        gray = src;
    }
    if (gray.depth() == CV_16U) {
        cv::normalize(gray, gray, 0, 255, cv::NORM_MINMAX, CV_8U);
    }
    // 高斯模糊
    GaussianBlur(gray, gray, Size(5, 5), 0);

    // 阈值分割
    Mat thresh;
    cv::threshold(gray, thresh, threshold, 255, THRESH_BINARY);

    // 形态学操作：膨胀
    Mat kernel = getStructuringElement(MORPH_RECT, Size(3, 3));
    dilate(thresh, thresh, kernel);

    // 查找轮廓
    std::vector< std::vector<Point>> contours;
    findContours(thresh, contours, RETR_EXTERNAL, CHAIN_APPROX_SIMPLE);

    contours.erase(std::remove_if(contours.begin(), contours.end(),
        [&](const std::vector<cv::Point>& contour) {
            cv::Rect rect = cv::boundingRect(contour);
            return rect.x == 0 || rect.y == 0 ||
                rect.x + rect.width == src.cols ||
                rect.y + rect.height == src.rows;
        }), contours.end());


    if (contours.size()==0)
    {
        return -2;
    }

    double maxArea = 0;
    for (size_t i = 0; i < contours.size(); i++) {
        double area = contourArea(contours[i]);
        if (area > maxArea) {
            maxArea = area;
            largestRect = boundingRect(contours[i]);
        }
    }

    return 0;
}

int findLuminousAreaLocalContrast(cv::Mat& src, std::vector<cv::Point2f>& points, float contrastThreshold, int windowSize = 15)
{
    points.clear();
    if (src.empty()) return -1;

    cv::Mat gray;
    if (src.channels() != 1)
        cv::cvtColor(src, gray, cv::COLOR_BGR2GRAY);
    else
        gray = src;
    if (gray.depth() == CV_16U)
        cv::normalize(gray, gray, 0, 255, cv::NORM_MINMAX, CV_8U);

    cv::Mat localMean;
    cv::blur(gray, localMean, cv::Size(windowSize, windowSize));
    cv::Mat contrast;
    cv::divide(gray, localMean + 1, contrast, 1, CV_32F);

    cv::Mat mask;
    cv::threshold(contrast, mask, contrastThreshold, 1, cv::THRESH_BINARY);

    mask.convertTo(mask, CV_8U, 255);

    cv::Mat kernel = cv::getStructuringElement(cv::MORPH_RECT, cv::Size(3, 3));
    cv::morphologyEx(mask, mask, cv::MORPH_OPEN, kernel);

    std::vector<std::vector<cv::Point>> contours;
    cv::findContours(mask, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

    // 可选：针对加权区域筛选最大轮廓
    // ...

    if (contours.empty()) return -2;

    size_t maxIdx = 0;
    double maxArea = 0;
    for (size_t i = 0; i < contours.size(); ++i) {
        double area = cv::contourArea(contours[i]);
        if (area > maxArea) {
            maxArea = area;
            maxIdx = i;
        }
    }

    cv::RotatedRect minRect = cv::minAreaRect(contours[maxIdx]);
    cv::Point2f verts[4];
    minRect.points(verts);
    points.assign(verts, verts + 4);
    return 0;
}

int drawPoiImage(cv::Mat& src, cv::Mat& dst, int radius, int* points, int pointCount,int thickness)
{
    int depth = src.depth();
    int lutSize = (depth == CV_8U) ? 255 : 65535;
    // 创建输入图像的cv::Mat
    // 处理图像，绘制圆形
    for (int i = 0; i < pointCount/2; ++i)
    {
        int x = points[i * 2];
        int y = points[i * 2 + 1];
        cv::circle(src, cv::Point(x, y), radius, cv::Scalar(0, 0, lutSize), thickness);
    }
    dst = src;
    return 0; // 成功
}


int extractChannel(cv::Mat& input, cv::Mat& dst ,int channel)
{
    if (input.empty())
        return -1;
    if (channel < 0 || input.channels() <= channel)
        return -2;

    // 拆分通道

    std::vector<cv::Mat> channels;
    cv::split(input, channels);

    cv::Mat redChannel = channels[channel];

    // 创建单通道图像 (灰度图像)
    //cv::Mat grayImage;
    //std::vector<cv::Mat> grayChannels = { redChannel, redChannel, redChannel };
    //cv::merge(grayChannels, grayImage);
	dst = redChannel;
    return 0;
}

int pseudoColor(cv::Mat& image, uint min1, uint max1, cv::ColormapTypes types)
{
    if (image.empty())
        return -1;

    if (image.channels() != 1) {
        cv::cvtColor(image, image, cv::COLOR_BGR2GRAY);
    }

    if (image.depth() == CV_16U) {

        ///2025.02.07 16为图像做直方图时，不做直方图均衡化，这会导致图像变形，这个效果可以通过调节Gammma实现
        //// 应用自适应直方图均衡化
        //cv::Ptr<cv::CLAHE> clahe = cv::createCLAHE();
        ////这里设置高了，四个角会被扩散掉，
        //clahe->setClipLimit(1.0); // 设置对比度限制
        //clahe->apply(image, image);

        min1 = min1 / 256;
        max1 = max1 / 256;
        cv::normalize(image, image, 0, 255, cv::NORM_MINMAX, CV_8U);
	}

    if (image.depth() == CV_32F) {
        cv::normalize(image, image, 0, 255, cv::NORM_MINMAX, CV_8U);
    }
    cv::Mat maskGreater;
    cv::Mat maskLess;
    if (max1 < 255) {
        maskGreater = image > max1; // Change maxVal to your specific threshold
        image.setTo(cv::Scalar(255, 255, 255), maskGreater);
    }
    if (min1 > 0) {
        // Set values less than a threshold to black
        maskLess = image < min1; // Change minVal to your specific threshold
        image.setTo(cv::Scalar(0, 0, 0), maskLess);
    }

    cv::applyColorMap(image, image, types);

    if (max1 < 255) {
        image.setTo(cv::Scalar(255, 255, 255), maskGreater);
    }
    if (min1 > 0) {
        // Set values less than a threshold to black
        image.setTo(cv::Scalar(0, 0, 0), maskLess);
    }

    return 0;
}

void AdjustWhiteBalance(const cv::Mat& src, cv::Mat& dst, double redBalance, double greenBalance, double blueBalance) {
    // Split the source image into BGR channels
    std::vector<cv::Mat> channels(3);
    cv::split(src, channels);

    // Apply balance parameters to each channel
    channels[2] *= redBalance;    // Red channel
    channels[1] *= greenBalance;  // Green channel
    channels[0] *= blueBalance;   // Blue channel

    // Merge the channels back into the destination image
    cv::merge(channels, dst);

    // Clip values to the appropriate range
    double maxVal = (src.depth() == CV_8U) ? 255.0 : 65535.0;
    cv::threshold(dst, dst, maxVal, maxVal, cv::THRESH_TRUNC);
}



void autoLevelsAdjust(cv::Mat& src, cv::Mat& dst)
{
    CV_Assert(!src.empty() && src.channels() >= 3);
    spdlog::info("AutoLevelsAdjust");

    //统计灰度直方图
    int BHist[256] = { 0 };    //B分离
    int GHist[256] = { 0 };    //G分量
    int RHist[256] = { 0 };    //R分量
    cv::MatIterator_<Vec3b> its, ends;
    for (its = src.begin<Vec3b>(), ends = src.end<Vec3b>(); its != ends; its++)
    {
        BHist[(*its)[0]]++;
        GHist[(*its)[1]]++;
        RHist[(*its)[2]]++;
    }

    //设置LowCut和HighCut
    float LowCut = 0.4;
    float HighCut = 0.4;

    //根据LowCut和HighCut查找每个通道最大值最小值
    int BMax = 0, BMin = 0;
    int GMax = 0, GMin = 0;
    int RMax = 0, RMin = 0;

    int TotalPixels = src.cols * src.rows;
    float LowTh = LowCut * 0.01 * TotalPixels;
    float HighTh = HighCut * 0.01 * TotalPixels;

    //B通道查找最小最大值
    int sumTempB = 0;
    for (int i = 0; i < 256; i++)
    {
        sumTempB += BHist[i];
        if (sumTempB >= LowTh)
        {
            BMin = i;
            break;
        }
    }
    sumTempB = 0;
    for (int i = 255; i >= 0; i--)
    {
        sumTempB += BHist[i];
        if (sumTempB >= HighTh)
        {
            BMax = i;
            break;
        }
    }

    //G通道查找最小最大值
    int sumTempG = 0;
    for (int i = 0; i < 256; i++)
    {
        sumTempG += GHist[i];
        if (sumTempG >= LowTh)
        {
            GMin = i;
            break;
        }
    }
    sumTempG = 0;
    for (int i = 255; i >= 0; i--)
    {
        sumTempG += GHist[i];
        if (sumTempG >= HighTh)
        {
            GMax = i;
            break;
        }
    }

    //R通道查找最小最大值
    int sumTempR = 0;
    for (int i = 0; i < 256; i++)
    {
        sumTempR += RHist[i];
        if (sumTempR >= LowTh)
        {
            RMin = i;
            break;
        }
    }
    sumTempR = 0;
    for (int i = 255; i >= 0; i--)
    {
        sumTempR += RHist[i];
        if (sumTempR >= HighTh)
        {
            RMax = i;
            break;
        }
    }

    //对每个通道建立分段线性查找表
    //B分量查找表
    int BTable[256] = { 0 };
    for (int i = 0; i < 256; i++)
    {
        if (i <= BMin)
            BTable[i] = 0;
        else if (i > BMin && i < BMax)
            BTable[i] = cvRound((float)(i - BMin) / (BMax - BMin) * 255);
        else
            BTable[i] = 255;
    }

    //G分量查找表
    int GTable[256] = { 0 };
    for (int i = 0; i < 256; i++)
    {
        if (i <= GMin)
            GTable[i] = 0;
        else if (i > GMin && i < GMax)
            GTable[i] = cvRound((float)(i - GMin) / (GMax - GMin) * 255);
        else
            GTable[i] = 255;
    }

    //R分量查找表
    int RTable[256] = { 0 };
    for (int i = 0; i < 256; i++)
    {
        if (i <= RMin)
            RTable[i] = 0;
        else if (i > RMin && i < RMax)
            RTable[i] = cvRound((float)(i - RMin) / (RMax - RMin) * 255);
        else
            RTable[i] = 255;
    }

    //对每个通道用相应的查找表进行分段线性拉伸
    cv::Mat dst_ = src.clone();
    cv::MatIterator_<Vec3b> itd, endd;
    for (itd = dst_.begin<Vec3b>(), endd = dst_.end<Vec3b>(); itd != endd; itd++)
    {
        (*itd)[0] = BTable[(*itd)[0]];
        (*itd)[1] = GTable[(*itd)[1]];
        (*itd)[2] = RTable[(*itd)[2]];
    }
    dst = dst_;
}



void automaticColorAdjustment(cv::Mat& image) {
    cv::Mat lab_image;
    cv::cvtColor(image, lab_image, cv::COLOR_BGR2Lab);

    std::vector<cv::Mat> lab_planes(3);
    cv::split(lab_image, lab_planes);

    double avg_a = cv::mean(lab_planes[1])[0];
    double avg_b = cv::mean(lab_planes[2])[0];

    lab_planes[1] = lab_planes[1] - ((avg_a - 128) * (lab_planes[0] / 255.0) * 1.1);
    lab_planes[2] = lab_planes[2] - ((avg_b - 128) * (lab_planes[0] / 255.0) * 1.1);

    cv::merge(lab_planes, lab_image);
    cv::cvtColor(lab_image, image, cv::COLOR_Lab2BGR);
}


void automaticToneAdjustment(cv::Mat& image, double clip_hist_percent) {
    cv::Mat gray;
    cv::cvtColor(image, gray, cv::COLOR_BGR2GRAY);

    int hist_size = 256;
    float range[] = { 0, 256 };
    const float* hist_range = { range };
    cv::Mat hist;

    cv::calcHist(&gray, 1, 0, cv::Mat(), hist, 1, &hist_size, &hist_range);

    std::vector<float> accumulator(hist_size);
    accumulator[0] = hist.at<float>(0);
    for (int i = 1; i < hist_size; i++) {
        accumulator[i] = accumulator[i - 1] + hist.at<float>(i);
    }

    float max_value = accumulator.back();
    clip_hist_percent *= (max_value / 100.0);
    clip_hist_percent /= 2.0;

    int min_gray = 0;
    while (accumulator[min_gray] < clip_hist_percent) {
        min_gray++;
    }

    int max_gray = hist_size - 1;
    while (accumulator[max_gray] >= (max_value - clip_hist_percent)) {
        max_gray--;
    }

    double alpha = 255.0 / (max_gray - min_gray);
    double beta = -min_gray * alpha;

    image.convertTo(image, -1, alpha, beta);
}

void AdjustBrightnessContrast(const cv::Mat& src, cv::Mat& dst, double alpha, double beta)
{
    src.convertTo(dst, src.type(), alpha, beta);
}

void ApplyGammaCorrection(const cv::Mat& src, cv::Mat& dst, double gamma)
{
    CV_Assert(gamma >= 0);

    double adjustedGamma = 1.0 / gamma;

    int depth = src.depth();
    int lutSize = (depth == CV_8U) ? 256 : 65536;

    if (depth == CV_8U)
    {
        cv::Mat lut(1, lutSize, CV_8UC1);

        uchar* p = lut.ptr<uchar>();
        for (int i = 0; i < lutSize; i++)
        {
            p[i] = cv::saturate_cast<uchar>(pow(i / 255.0, adjustedGamma) * 255.0);
        }
        cv::LUT(src, lut, dst);
    }
    else if (depth == CV_16U)
    {
        cv::Mat lut(1, lutSize, CV_16UC1);
        ushort* p = lut.ptr<ushort>();
        for (int i = 0; i < lutSize; i++)
        {
            p[i] = cv::saturate_cast<ushort>(pow(i / 65535.0, adjustedGamma) * 65535.0);
        }
        dst.create(src.size(), src.type());

        int channels = src.channels();
        for (int y = 0; y < src.rows; y++)
        {
            const ushort* srcRow = src.ptr<ushort>(y);
            ushort* dstRow = dst.ptr<ushort>(y);
            for (int x = 0; x < src.cols * channels; x++)
            {
                dstRow[x] = p[srcRow[x]];
            }
        }
    }
    else
    {
        CV_Error(cv::Error::StsUnsupportedFormat, "Unsupported image depth");
    }
}

