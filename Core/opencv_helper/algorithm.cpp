
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
#include <numeric>
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

    // ����ṹԪ��
    cv::Mat binary;
    cv::threshold(gray, binary, 20, 255, cv::THRESH_BINARY);


    // ��ʴ����
    cv::erode(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(2, 2)));

    cv::dilate(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(4, 4)));
    cv::erode(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(2, 2)));

    // �������
    std::vector<std::vector<cv::Point>> contours;
    cv::findContours(binary, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

    std::vector<cv::Point> centers;

    // ��������
    for (const auto& contour : contours) {
        // ���������ı߽��
        cv::Rect boundingBox = cv::boundingRect(contour);

        // ���ݵ������֪��С����
        if (boundingBox.width > 2 && boundingBox.width < 20 &&
            boundingBox.height > 2 && boundingBox.height < 20) {

            // �������ĵ�
            int cx = boundingBox.x + boundingBox.width / 2;
            int cy = boundingBox.y + boundingBox.height / 2;
            centers.push_back(cv::Point(cx, cy));
        }
        else {
            size_t coutns = centers.size();
        }
    }

    //������
    size_t coutns = centers.size();

    // �������ĵ��͹��
    std::vector<cv::Point> hull;
    if (!centers.empty()) {
        cv::convexHull(centers, hull);
    }

    //�������ĵ�
    for (const auto& center : centers) {
        cv::circle(image8bit, center, 4, cv::Scalar(255), -1);
    }

    // ����͹��
    if (!hull.empty()) {
        for (size_t i = 0; i < hull.size(); ++i) {
            cv::line(image8bit, hull[i], hull[(i + 1) % hull.size()], cv::Scalar(255), 2);
        }
    }

    // ����͹�������
    double area = cv::contourArea(hull);
    std::cout << "Convex Hull Area: " << area << std::endl;

    // ����͹���ı߽����
    cv::Rect boundingRect = cv::boundingRect(hull);
    double width = boundingRect.width;
    double height = boundingRect.height;

    double singlewith = width / 850;
    double singleheight = height / 650;

    // ����һ�����룬��ʼΪȫ��
    cv::Mat mask = cv::Mat::zeros(image.size(), CV_8UC1);

    // �������ϻ���͹������
    std::vector<std::vector<cv::Point>> hulls = { hull };
    cv::fillPoly(mask, hulls, cv::Scalar(255));

    // ����ͼ������е�
    for (int y = 0; y < binary.rows; ++y) {
        uchar* maskRow = mask.ptr<uchar>(y);
        uchar* imgRow = binary.ptr<uchar>(y);
        for (int x = 0; x < binary.cols; ++x) {
            // ��������иõ㲻��͹���ڣ�������Ϊ255
            if (maskRow[x] == 0) {
                imgRow[x] = 255;
            }
        }
    }

    //ȱ�ٵĵ�
    size_t black = rows * cols - centers.size();
    std::cout << black << std::endl;


    std::vector<std::vector<cv::Point>> ledMatrix1;
    std::vector<cv::Point> currentRow;

    cv::dilate(binary, binary, cv::getStructuringElement(cv::MORPH_RECT, cv::Size(12, 12)));

    binary = 255 - binary;
    std::vector<cv::Point> blackcenters;

    std::vector<std::vector<cv::Point>> contourless;
    cv::findContours(binary, contourless, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

    // ��������
    for (const auto& contour : contourless) {
        // ���������ı߽��
        cv::Rect boundingBox = cv::boundingRect(contour);

        // ���ݵ������֪��С����
        if (boundingBox.width > 2 && boundingBox.width < 20 &&
            boundingBox.height > 2 && boundingBox.height < 20) {

            // �������ĵ�
            int cx = boundingBox.x + boundingBox.width / 2;
            int cy = boundingBox.y + boundingBox.height / 2;
            blackcenters.push_back(cv::Point(cx, cy));
        }
        else
        {
            // ����͹���ı߽����
            cv::Rect boundingRect = cv::boundingRect(contour);

            // ��ļ������ʼƫ��
            int offset = 4;

            // �洢��͹���ڵĵ�
            std::vector<cv::Point> pointsInsideHull;

            // �ڱ߽�����ڵ���
            for (double y = boundingRect.y + offset; y < boundingRect.y + boundingRect.height; y += singlewith) {
                for (double x = boundingRect.x + offset; x < boundingRect.x + boundingRect.width; x += singleheight) {
                    cv::Point p(x, y);
                    // �����Ƿ���͹����
                    if (cv::pointPolygonTest(contour, p, false) >= 0) {
                        blackcenters.push_back(p);
                    }
                }
            }

        }
    }

    //ȱ�ٵĵ�
    std::cout << blackcenters.size() << std::endl;
    for (const auto& contour : blackcenters)
    {
        std::cout << contour << std::endl;
        cv::circle(image8bit, contour, 5, cv::Scalar(0, 0, 255), 1);
    }
    std::cout << blackcenters.size() << std::endl;

}

// ����0������-1ͼ��գ�-2δ�ҵ�������
// points ���ط������ĸ��ǵ㣨˳��Ϊ minAreaRect ˳��
int findLuminousAreaCorners(cv::Mat& src, std::vector<cv::Point2f>& points, int threshold)
{
    points.clear();
    if (src.empty()) return -1;

    Mat gray;
    if (src.channels() != 1)
        cvtColor(src, gray, COLOR_BGR2GRAY);
    else
        gray = src;

    // 处理各种位深度
    if (gray.depth() == CV_16U)
        normalize(gray, gray, 0, 255, NORM_MINMAX, CV_8U);
    else if (gray.depth() == CV_32F || gray.depth() == CV_64F)
        normalize(gray, gray, 0, 255, NORM_MINMAX, CV_8U);

    GaussianBlur(gray, gray, Size(5, 5), 0);

    // CLAHE增强对比度
    cv::Ptr<cv::CLAHE> clahe = cv::createCLAHE(2.0, cv::Size(8, 8));
    clahe->apply(gray, gray);

    Mat thresh;
    if (threshold < 0) {
        cv::threshold(gray, thresh, 0, 255, THRESH_BINARY | THRESH_OTSU);
    }
    else {
        cv::threshold(gray, thresh, threshold, 255, THRESH_BINARY);
    }

    // 改进形态学操作
    Mat kernelOpen = getStructuringElement(MORPH_RECT, Size(3, 3));
    morphologyEx(thresh, thresh, MORPH_OPEN, kernelOpen);

    Mat kernelDilate = getStructuringElement(MORPH_RECT, Size(5, 5));
    dilate(thresh, thresh, kernelDilate, Point(-1, -1), 2);

    std::vector<std::vector<Point>> contours;
    findContours(thresh, contours, RETR_EXTERNAL, CHAIN_APPROX_SIMPLE);

    // ȥ�����ߵ�
    contours.erase(remove_if(contours.begin(), contours.end(),
        [&](const std::vector<Point>& contour) {
            Rect rect = boundingRect(contour);
            return rect.x == 0 || rect.y == 0 ||
                rect.x + rect.width == src.cols ||
                rect.y + rect.height == src.rows;
        }), contours.end());

    if (contours.empty()) return -2;

    // ������������
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
    // 0.02 * peri �ɵ�����ͨ�� 0.01~0.05 ֮��
    cv::approxPolyDP(contours[maxIdx], approx, 0.02 * peri, true);

    if (approx.size() == 4) {
        // �������Ҫ���ı���4���ǵ�
        for (int i = 0; i < 4; ++i)
            points.push_back(cv::Point2f(approx[i]));
        return 0;
    }
    else {

        // ��С��Ӿ���
        RotatedRect minRect = minAreaRect(contours[maxIdx]);
        Point2f verts[4];
        minRect.points(verts);
        points.assign(verts, verts + 4);
    }

    return 0;
}


int findLuminousArea(cv::Mat& src, cv::Rect& largestRect,int threshold)
{
    if (src.empty()) {
        return -1;
    }
    Mat gray;
    if (src.channels() != 1) {
        cvtColor(src, gray, COLOR_BGR2GRAY);
    }
    else {
        gray = src;
    }

    // 处理各种位深度
    if (gray.depth() == CV_16U) {
        cv::normalize(gray, gray, 0, 255, cv::NORM_MINMAX, CV_8U);
    }
    else if (gray.depth() == CV_32F || gray.depth() == CV_64F) {
        cv::normalize(gray, gray, 0, 255, cv::NORM_MINMAX, CV_8U);
    }

    // 高斯模糊降噪
    GaussianBlur(gray, gray, Size(5, 5), 0);

    // 使用CLAHE增强对比度，改善低对比度图像的检测效果
    cv::Ptr<cv::CLAHE> clahe = cv::createCLAHE(2.0, cv::Size(8, 8));
    clahe->apply(gray, gray);

    // 阈值分割
    Mat thresh;
    if (threshold < 0) {
        cv::threshold(gray, thresh, 0, 255, THRESH_BINARY | THRESH_OTSU);
    }
    else {
        cv::threshold(gray, thresh, threshold, 255, THRESH_BINARY);
    }

    // 形态学操作：先开运算去噪，再膨胀合并相邻区域
    Mat kernelOpen = getStructuringElement(MORPH_RECT, Size(3, 3));
    morphologyEx(thresh, thresh, MORPH_OPEN, kernelOpen);

    Mat kernelDilate = getStructuringElement(MORPH_RECT, Size(5, 5));
    dilate(thresh, thresh, kernelDilate, Point(-1, -1), 2);

    // 轮廓检测
    std::vector<std::vector<Point>> contours;
    findContours(thresh, contours, RETR_EXTERNAL, CHAIN_APPROX_SIMPLE);

    // 过滤边界轮廓
    contours.erase(std::remove_if(contours.begin(), contours.end(),
        [&](const std::vector<cv::Point>& contour) {
            cv::Rect rect = cv::boundingRect(contour);
            return rect.x == 0 || rect.y == 0 ||
                rect.x + rect.width == src.cols ||
                rect.y + rect.height == src.rows;
        }), contours.end());

    if (contours.empty())
    {
        return -2;
    }

    // 如果有多个轮廓，尝试合并所有有效轮廓的外接矩形
    if (contours.size() > 1) {
        // 先找最大面积的轮廓
        double maxArea = 0;
        size_t maxIdx = 0;
        double totalArea = 0;
        for (size_t i = 0; i < contours.size(); i++) {
            double area = contourArea(contours[i]);
            totalArea += area;
            if (area > maxArea) {
                maxArea = area;
                maxIdx = i;
            }
        }

        // 如果最大轮廓面积占总面积的80%以上，直接用它
        if (maxArea > totalArea * 0.8) {
            largestRect = boundingRect(contours[maxIdx]);
        }
        else {
            // 否则合并所有轮廓求外接矩形
            std::vector<cv::Point> allPoints;
            for (const auto& contour : contours) {
                // 只合并面积大于最大面积1%的轮廓
                if (contourArea(contour) > maxArea * 0.01) {
                    allPoints.insert(allPoints.end(), contour.begin(), contour.end());
                }
            }
            largestRect = boundingRect(allPoints);
        }
    }
    else {
        largestRect = boundingRect(contours[0]);
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

    // ��ѡ����Լ�Ȩ����ɸѡ�������
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
    // ��������ͼ���cv::Mat
    // ����ͼ�񣬻���Բ��
    for (int i = 0; i < pointCount/2; ++i)
    {
        int x = points[i * 2];
        int y = points[i * 2 + 1];
        cv::circle(src, cv::Point(x, y), radius, cv::Scalar(0, 0, lutSize), thickness);
    }
    dst = src;
    return 0; // �ɹ�
}


int extractChannel(cv::Mat& input, cv::Mat& dst ,int channel)
{
    if (input.empty())
        return -1;
    if (channel < 0 || input.channels() <= channel)
        return -2;

    // ���ͨ��

    std::vector<cv::Mat> channels;
    cv::split(input, channels);

    cv::Mat redChannel = channels[channel];

    // ������ͨ��ͼ�� (�Ҷ�ͼ��)
    //cv::Mat grayImage;
    //std::vector<cv::Mat> grayChannels = { redChannel, redChannel, redChannel };
    //cv::merge(grayChannels, grayImage);
	dst = redChannel;
    return 0;
}

void GetOptimizedLUT(cv::ColormapTypes mapType, int minTh, int maxTh, cv::Mat& outLut)
{
    cv::Mat range(1, 256, CV_8U);
    std::iota(range.ptr<uint8_t>(), range.ptr<uint8_t>() + 256, 0);

    cv::applyColorMap(range, outLut, mapType);

    cv::Vec3b* ptr = outLut.ptr<cv::Vec3b>();

    if (minTh > 0) {
        std::memset(ptr, 0, std::min(minTh, 256) * 3);
    }

    for (int i = std::max(maxTh + 1, 0); i < 256; i++) {
        ptr[i] = cv::Vec3b(255, 255, 255);
    }
}

void GetStretchedLUT(cv::ColormapTypes mapType, int minTh, int maxTh, cv::Mat& outLut)
{
    cv::Mat range(1, 256, CV_8U);
    std::iota(range.ptr<uint8_t>(), range.ptr<uint8_t>() + 256, 0);

    cv::Mat fullColormap;
    cv::applyColorMap(range, fullColormap, mapType);
    const cv::Vec3b* cmPtr = fullColormap.ptr<cv::Vec3b>();

    outLut.create(1, 256, CV_8UC3);
    cv::Vec3b* ptr = outLut.ptr<cv::Vec3b>();

    int rangeSize = maxTh - minTh;

    for (int i = 0; i < 256; i++) {
        if (i < minTh) {
            ptr[i] = cv::Vec3b(0, 0, 0);
        }
        else if (i > maxTh) {
            ptr[i] = cv::Vec3b(255, 255, 255);
        }
        else {
            int cmIdx = (rangeSize > 0) ? (int)((double)(i - minTh) / rangeSize * 255.0) : 128;
            cmIdx = std::clamp(cmIdx, 0, 255);
            ptr[i] = cmPtr[cmIdx];
        }
    }
}

static void applyLUT(const cv::Mat& image, const cv::Mat& lut, cv::Mat& result)
{
    result.create(image.rows, image.cols, CV_8UC3);
    const cv::Vec3b* lutPtr = lut.ptr<cv::Vec3b>();

    cv::parallel_for_(cv::Range(0, image.rows), [&](const cv::Range& range) {
        for (int y = range.start; y < range.end; y++) {
            const uint8_t* srcRow = image.ptr<uint8_t>(y);
            cv::Vec3b* dstRow = result.ptr<cv::Vec3b>(y);
            for (int x = 0; x < image.cols; x++) {
                dstRow[x] = lutPtr[srcRow[x]];
            }
        }
    });
}

int pseudoColor(cv::Mat& image, uint min1, uint max1, cv::ColormapTypes types)
{
    if (image.empty()) return -1;

    if (image.channels() > 1) {
        cv::cvtColor(image, image, cv::COLOR_BGR2GRAY);
    }

    switch (image.depth()) {
    case CV_16U:
        min1 >>= 8;
        max1 >>= 8;
        image.convertTo(image, CV_8U, 1.0 / 257.0);
        break;
    case CV_32F:
    case CV_64F:
        cv::normalize(image, image, 0, 255, cv::NORM_MINMAX, CV_8U);
        break;
    }

    min1 = std::min(min1, 255u);
    max1 = std::min(max1, 255u);

    cv::Mat customLut;
    GetOptimizedLUT(types, (int)min1, (int)max1, customLut);

    cv::Mat result;
    applyLUT(image, customLut, result);
    image = std::move(result);
    return 0;
}

int pseudoColorAutoRange(cv::Mat& image, uint min1, uint max1, cv::ColormapTypes types, uint dataMin, uint dataMax)
{
    if (image.empty()) return -1;

    if (image.channels() > 1) {
        cv::cvtColor(image, image, cv::COLOR_BGR2GRAY);
    }

    switch (image.depth()) {
    case CV_16U:
    {
        double scale = (dataMax > dataMin) ? 255.0 / (dataMax - dataMin) : 1.0;
        double offset = -((double)dataMin) * scale;
        image.convertTo(image, CV_8U, scale, offset);
        // Remap slider min/max from [dataMin, dataMax] to [0, 255]
        if (dataMax > dataMin) {
            double range = (double)dataMax - (double)dataMin;
            min1 = (uint)std::clamp(((double)min1 - (double)dataMin) / range * 255.0, 0.0, 255.0);
            max1 = (uint)std::clamp(((double)max1 - (double)dataMin) / range * 255.0, 0.0, 255.0);
        }
        break;
    }
    case CV_32F:
    case CV_64F:
        cv::normalize(image, image, 0, 255, cv::NORM_MINMAX, CV_8U);
        min1 = 0;
        max1 = 255;
        break;
    default:
        // 8-bit: no conversion needed, slider values are already in [0, 255] range
        break;
    }

    min1 = std::min(min1, 255u);
    max1 = std::min(max1, 255u);

    cv::Mat customLut;
    GetStretchedLUT(types, (int)min1, (int)max1, customLut);

    cv::Mat result;
    applyLUT(image, customLut, result);
    image = std::move(result);
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

    //ͳ�ƻҶ�ֱ��ͼ
    int BHist[256] = { 0 };    //B����
    int GHist[256] = { 0 };    //G����
    int RHist[256] = { 0 };    //R����
    cv::MatIterator_<Vec3b> its, ends;
    for (its = src.begin<Vec3b>(), ends = src.end<Vec3b>(); its != ends; its++)
    {
        BHist[(*its)[0]]++;
        GHist[(*its)[1]]++;
        RHist[(*its)[2]]++;
    }

    //����LowCut��HighCut
    float LowCut = 0.4;
    float HighCut = 0.4;

    //����LowCut��HighCut����ÿ��ͨ�����ֵ��Сֵ
    int BMax = 0, BMin = 0;
    int GMax = 0, GMin = 0;
    int RMax = 0, RMin = 0;

    int TotalPixels = src.cols * src.rows;
    float LowTh = LowCut * 0.01 * TotalPixels;
    float HighTh = HighCut * 0.01 * TotalPixels;

    //Bͨ��������С���ֵ
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

    //Gͨ��������С���ֵ
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

    //Rͨ��������С���ֵ
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

    //��ÿ��ͨ�������ֶ����Բ��ұ�
    //B�������ұ�
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

    //G�������ұ�
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

    //R�������ұ�
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

    //��ÿ��ͨ������Ӧ�Ĳ��ұ����зֶ���������
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

// 高斯模糊
void ApplyGaussianBlur(const cv::Mat& src, cv::Mat& dst, int kernelSize, double sigma)
{
    CV_Assert(kernelSize > 0 && kernelSize % 2 == 1);
    cv::GaussianBlur(src, dst, cv::Size(kernelSize, kernelSize), sigma);
}

// 中值滤波
void ApplyMedianBlur(const cv::Mat& src, cv::Mat& dst, int kernelSize)
{
    CV_Assert(kernelSize > 0 && kernelSize % 2 == 1);
    cv::medianBlur(src, dst, kernelSize);
}

// 锐化
void ApplySharpen(const cv::Mat& src, cv::Mat& dst)
{
    cv::Mat kernel = (cv::Mat_<float>(3, 3) <<
        0, -1, 0,
        -1, 5, -1,
        0, -1, 0);
    cv::filter2D(src, dst, src.depth(), kernel);
}

// Canny边缘检测
void ApplyCannyEdgeDetection(const cv::Mat& src, cv::Mat& dst, double threshold1, double threshold2)
{
    cv::Mat gray;
    if (src.channels() == 3 || src.channels() == 4)
    {
        cv::cvtColor(src, gray, cv::COLOR_BGR2GRAY);
    }
    else
    {
        gray = src;
    }
    
    // Convert to 8-bit if needed
    if (gray.depth() != CV_8U)
    {
        gray.convertTo(gray, CV_8U, 255.0 / 65535.0);
    }
    
    cv::Canny(gray, dst, threshold1, threshold2);
}

// 直方图均衡化 (仅用于灰度图像)
void ApplyHistogramEqualization(const cv::Mat& src, cv::Mat& dst)
{
    cv::Mat gray;
    if (src.channels() == 3 || src.channels() == 4)
    {
        cv::cvtColor(src, gray, cv::COLOR_BGR2GRAY);
    }
    else
    {
        gray = src.clone();
    }
    
    // Convert to 8-bit if needed
    if (gray.depth() != CV_8U)
    {
        gray.convertTo(gray, CV_8U, 255.0 / 65535.0);
    }
    
    cv::equalizeHist(gray, dst);
}

// 寻找灯珠 (Find Light Beads)
int findLightBeads(
    cv::Mat& src,
    std::vector<cv::Point>& centers,
    std::vector<cv::Point>& blackCenters,
    int threshold,
    int minSize,
    int maxSize,
    int rows,
    int cols)
{
    // 用于网格遍历的偏移量，避免边界重复检测
    const int GRID_OFFSET = 4;

    centers.clear();
    blackCenters.clear();

    if (src.empty()) {
        return -1;
    }

    // 转换为8位图像，保持原始通道数
    cv::Mat image8bit;
    if (src.depth() == CV_16U) {
        // 16位转8位，保持通道数
        int targetType = (src.channels() == 1) ? CV_8UC1 : CV_8UC3;
        src.convertTo(image8bit, targetType, 255.0 / 65535.0);
    }
    else if (src.depth() != CV_8U) {
        // 其他深度转8位
        int targetType = (src.channels() == 1) ? CV_8UC1 : CV_8UC3;
        src.convertTo(image8bit, targetType);
    }
    else {
        image8bit = src;
    }

    // 转换为灰度图
    cv::Mat gray;
    if (image8bit.channels() == 3 || image8bit.channels() == 4) {
        cv::cvtColor(image8bit, gray, cv::COLOR_BGR2GRAY);
    }
    else {
        gray = image8bit;
    }

    // 二值化
    cv::Mat binary;
    cv::threshold(gray, binary, threshold, 255, cv::THRESH_BINARY);

    // 形态学操作
    cv::erode(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(2, 2)));
    cv::dilate(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(4, 4)));
    cv::erode(binary, binary, cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(2, 2)));

    // 检测轮廓
    std::vector<std::vector<cv::Point>> contours;
    cv::findContours(binary, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

    // 遍历轮廓，找到灯珠中心点
    for (const auto& contour : contours) {
        cv::Rect boundingBox = cv::boundingRect(contour);


        if ((boundingBox.width > minSize || minSize <= 0) && (maxSize <= 0 || boundingBox.width < maxSize) &&
            (boundingBox.height > minSize || minSize <= 0) && (maxSize <= 0 || boundingBox.height < maxSize)) {

            // 计算中心点
            int cx = boundingBox.x + boundingBox.width / 2;
            int cy = boundingBox.y + boundingBox.height / 2;
            centers.push_back(cv::Point(cx, cy));
        }
    }

    // 计算凸包
    std::vector<cv::Point> hull;
    if (!centers.empty()) {
        cv::convexHull(centers, hull);
    }

    // 创建掩码
    cv::Mat mask = cv::Mat::zeros(src.size(), CV_8UC1);

    // 在掩码上绘制凸包区域
    if (!hull.empty()) {
        std::vector<std::vector<cv::Point>> hulls = { hull };
        cv::fillPoly(mask, hulls, cv::Scalar(255));

        // 遍历图像的所有点，将凸包外的点设为255
        for (int y = 0; y < binary.rows; ++y) {
            uchar* maskRow = mask.ptr<uchar>(y);
            uchar* imgRow = binary.ptr<uchar>(y);
            for (int x = 0; x < binary.cols; ++x) {
                if (maskRow[x] == 0) {
                    imgRow[x] = 255;
                }
            }
        }
    }

    // 查找缺失的灯珠
    cv::dilate(binary, binary, cv::getStructuringElement(cv::MORPH_RECT, cv::Size(12, 12)));
    binary = 255 - binary;

    std::vector<std::vector<cv::Point>> contourless;
    cv::findContours(binary, contourless, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

    // 遍历轮廓，找到缺失的灯珠
    for (const auto& contour : contourless) {
        cv::Rect boundingBox = cv::boundingRect(contour);

        // 根据灯珠的已知大小过滤
        if (boundingBox.width > minSize && boundingBox.width < maxSize &&
            boundingBox.height > minSize && boundingBox.height < maxSize) {

            // 计算中心点
            int cx = boundingBox.x + boundingBox.width / 2;
            int cy = boundingBox.y + boundingBox.height / 2;
            blackCenters.push_back(cv::Point(cx, cy));
        }
        else if (!hull.empty()) {
            // 对于较大的区域，计算单个灯珠的尺寸并填充网格点
            cv::Rect hullBoundingRect = cv::boundingRect(hull);
            double width = hullBoundingRect.width;
            double height = hullBoundingRect.height;

            if (cols > 0 && rows > 0)
            {
                double singleWidth = width / cols;
                double singleHeight = height / rows;

                // 在边界框内遍历网格点
                for (double y = boundingBox.y + GRID_OFFSET; y < boundingBox.y + boundingBox.height; y += singleHeight) {
                    for (double x = boundingBox.x + GRID_OFFSET; x < boundingBox.x + boundingBox.width; x += singleWidth) {
                        cv::Point p(static_cast<int>(x), static_cast<int>(y));
                        // 检查是否在轮廓内
                        if (cv::pointPolygonTest(contour, p, false) >= 0) {
                            blackCenters.push_back(p);
                        }
                    }
                }
            }
        }

        return 0;
    }
}

int detectKeyRegions(
    cv::Mat& src,
    std::vector<cv::Rect>& keyRects,
    int threshold,
    int minArea,
    int maxArea,
    double marginRatio)
{
    keyRects.clear();
    if (src.empty()) return -1;

    // 转为8位灰度图
    cv::Mat gray;
    cv::Mat work;
    if (src.depth() == CV_16U) {
        cv::Mat temp;
        src.convertTo(temp, (src.channels() == 1) ? CV_8UC1 : CV_8UC3, 255.0 / 65535.0);
        work = temp;
    }
    else if (src.depth() != CV_8U) {
        cv::Mat temp;
        src.convertTo(temp, (src.channels() == 1) ? CV_8UC1 : CV_8UC3);
        work = temp;
    }
    else {
        work = src;
    }

    if (work.channels() >= 3) {
        cv::cvtColor(work, gray, cv::COLOR_BGR2GRAY);
    }
    else {
        gray = work;
    }

    // 高斯模糊降噪
    cv::GaussianBlur(gray, gray, cv::Size(5, 5), 0);

    // 阈值化
    cv::Mat binary;
    if (threshold < 0) {
        cv::threshold(gray, binary, 0, 255, cv::THRESH_BINARY | cv::THRESH_OTSU);
    }
    else {
        cv::threshold(gray, binary, threshold, 255, cv::THRESH_BINARY);
    }

    // 形态学操作：先开运算去除噪点，再闭运算填充按键内部间隙
    cv::Mat kernelOpen = cv::getStructuringElement(cv::MORPH_RECT, cv::Size(3, 3));
    cv::morphologyEx(binary, binary, cv::MORPH_OPEN, kernelOpen);

    cv::Mat kernelClose = cv::getStructuringElement(cv::MORPH_RECT, cv::Size(7, 7));
    cv::morphologyEx(binary, binary, cv::MORPH_CLOSE, kernelClose);

    // 轮廓检测
    std::vector<std::vector<cv::Point>> contours;
    cv::findContours(binary, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

    if (contours.empty()) return -2;

    // 计算图像总面积作为参考
    double imageArea = (double)src.rows * src.cols;
    if (maxArea <= 0) {
        maxArea = (int)(imageArea * 0.25); // 默认最大面积不超过图像面积的25%
    }

    // 收集有效轮廓的矩形
    std::vector<cv::Rect> candidateRects;
    for (const auto& contour : contours) {
        double area = cv::contourArea(contour);
        if (area < minArea || area > maxArea) continue;

        cv::Rect rect = cv::boundingRect(contour);

        // 过滤掉贴着图像边界的轮廓（通常是背景噪声）
        if (rect.x == 0 || rect.y == 0 ||
            rect.x + rect.width == src.cols ||
            rect.y + rect.height == src.rows) {
            continue;
        }

        // 过滤掉宽高比过于极端的区域（非按键形状）
        double aspect = (double)rect.width / rect.height;
        if (aspect < 0.15 || aspect > 8.0) continue;

        candidateRects.push_back(rect);
    }

    if (candidateRects.empty()) return -2;

    // 对矩形应用边距缩进（如果指定）
    double mr = std::max(0.0, std::min(0.45, marginRatio));
    for (auto& r : candidateRects) {
        int dx = (int)(r.width * mr);
        int dy = (int)(r.height * mr);
        r.x += dx;
        r.y += dy;
        r.width -= 2 * dx;
        r.height -= 2 * dy;
        if (r.width < 1) r.width = 1;
        if (r.height < 1) r.height = 1;
    }

    // 按位置排序：先按y分行，再按x排列
    // 先找到行的y坐标范围
    if (!candidateRects.empty()) {
        // 按y坐标排序
        std::sort(candidateRects.begin(), candidateRects.end(),
            [](const cv::Rect& a, const cv::Rect& b) {
                return a.y < b.y;
            });

        // 对同一行内的按x排序
        // 简单方法：按中心y分组,容差为平均高度的一半
        double avgHeight = 0;
        for (const auto& r : candidateRects) avgHeight += r.height;
        avgHeight /= candidateRects.size();
        double rowTolerance = avgHeight * 0.5;

        std::vector<std::vector<cv::Rect>> rows;
        std::vector<cv::Rect> currentRow;
        int lastY = candidateRects[0].y + candidateRects[0].height / 2;

        for (const auto& r : candidateRects) {
            int centerY = r.y + r.height / 2;
            if (std::abs(centerY - lastY) > rowTolerance && !currentRow.empty()) {
                // 新行
                std::sort(currentRow.begin(), currentRow.end(),
                    [](const cv::Rect& a, const cv::Rect& b) { return a.x < b.x; });
                rows.push_back(currentRow);
                currentRow.clear();
            }
            currentRow.push_back(r);
            lastY = centerY;
        }
        if (!currentRow.empty()) {
            std::sort(currentRow.begin(), currentRow.end(),
                [](const cv::Rect& a, const cv::Rect& b) { return a.x < b.x; });
            rows.push_back(currentRow);
        }

        // 展开为排序后的列表
        keyRects.clear();
        for (const auto& row : rows) {
            for (const auto& r : row) {
                keyRects.push_back(r);
            }
        }
    }

    return 0;
}

