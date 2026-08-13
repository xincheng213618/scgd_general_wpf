// Test for M_FindLuminousArea with self-adaptive thresholding
// Test M_FindLuminousArea adaptive threshold function

#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <iostream>
#include <opencv2/opencv.hpp>
#include <nlohmann/json.hpp>
#include "../../Native/include/opencv_media_export.h"
#include "../../Native/include/video_export.h"
#include <atomic>
#include <array>
#include <chrono>
#include <cmath>
#include <combaseapi.h>
#include <cstddef>
#include <cstdio>
#include <cstdint>
#include <cstdlib>
#include <filesystem>
#include <iomanip>
#include <limits>
#include <sstream>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

using json = nlohmann::json;

#include "../../Native/opencv_helper/algorithm/surface_defect/surface_defect.h"

bool RunCalibrationApiSmokeTests();
bool RunCalibrationCacheSmallBudgetTests();
bool RunCalibrationRealDataTests(const std::filesystem::path& testRoot);
bool RunCalibrationLegacyColorComparison(
    const std::filesystem::path& rawPath,
    const std::filesystem::path& colorFile,
    const std::filesystem::path& legacyDll);

bool RunP2AlgorithmTests();
bool RunNativeLoggingTests();

static std::atomic<int> g_videoCallbackFrames{ 0 };
static std::atomic<int> g_videoStatusPlaying{ 0 };

std::filesystem::path findTestDataFile(const std::filesystem::path& relativePath)
{
    namespace fs = std::filesystem;
    std::vector<fs::path> roots;

    fs::path cursor = fs::current_path();
    for (int i = 0; i < 8 && !cursor.empty(); ++i) {
        roots.push_back(cursor);
        roots.push_back(cursor / "Test" / "opencv_helper_test");

        fs::path parent = cursor.parent_path();
        if (parent == cursor) {
            break;
        }
        cursor = parent;
    }

    for (const fs::path& root : roots) {
        fs::path candidate = root / relativePath;
        if (fs::exists(candidate)) {
            return candidate;
        }
    }

    return {};
}

bool nearlyEqual(double actual, double expected, double tolerance)
{
    return std::isfinite(actual)
        && std::isfinite(expected)
        && std::abs(actual - expected) <= tolerance;
}

static void __stdcall smokeVideoFrameCallback(int, HImage* frame, int, int, void*)
{
    if (frame != nullptr && frame->pData != nullptr && frame->rows > 0 && frame->cols > 0) {
        g_videoCallbackFrames.fetch_add(1);
        CoTaskMemFree(frame->pData);
        frame->pData = nullptr;
    }
}

static void __stdcall smokeVideoStatusCallback(int, int status, void*)
{
    if (status == 1) {
        g_videoStatusPlaying.fetch_add(1);
    }
}

static int __cdecl smokeInitialFrameCallback(void* data, int rows, int cols, int channels)
{
    return data != nullptr && rows > 0 && cols > 0 && channels > 0 ? 0 : -1;
}

bool smokeHImageHelpersValidateLayoutAndOwnership()
{
    std::cout << "HImage helper validation smoke..." << std::endl;

    const int width = 3;
    const int height = 2;
    const int channels = 3;
    const int stride = 16;
    std::vector<unsigned char> padded(static_cast<size_t>(height) * stride, 0);
    for (int y = 0; y < height; ++y) {
        unsigned char* row = padded.data() + static_cast<size_t>(y) * stride;
        for (int x = 0; x < width; ++x) {
            row[x * channels + 0] = static_cast<unsigned char>(10 + x);
            row[x * channels + 1] = static_cast<unsigned char>(20 + y);
            row[x * channels + 2] = static_cast<unsigned char>(30 + x + y);
        }
    }

    HImage valid{};
    valid.rows = height;
    valid.cols = width;
    valid.channels = channels;
    valid.depth = 8;
    valid.stride = stride;
    valid.pData = padded.data();

    cv::Mat view = HImageToMatView(valid);
    const bool validView = !view.empty()
        && view.rows == height
        && view.cols == width
        && view.step == stride
        && view.at<cv::Vec3b>(1, 2)[2] == padded[static_cast<size_t>(1) * stride + 2 * channels + 2];

    HImage invalidChannels = valid;
    invalidChannels.channels = CV_CN_MAX + 1;
    HImage invalidStride = valid;
    invalidStride.stride = width * channels - 1;
    HImage negativeStride = valid;
    negativeStride.stride = -1;

    cv::Mat backing(5, 6, CV_8UC3);
    cv::randu(backing, cv::Scalar::all(0), cv::Scalar::all(255));
    cv::Mat roi = backing(cv::Rect(1, 1, 3, 2));
    HImage owned{};
    const int roiRet = MatToHImage(roi, &owned);

    bool roiCopied = roiRet == 0
        && owned.pData != nullptr
        && owned.rows == roi.rows
        && owned.cols == roi.cols
        && owned.channels == roi.channels()
        && owned.depth == 8
        && owned.stride == owned.cols * owned.channels;
    if (roiCopied) {
        for (int y = 0; y < roi.rows && roiCopied; ++y) {
            for (int x = 0; x < roi.cols; ++x) {
                const cv::Vec3b expected = roi.at<cv::Vec3b>(y, x);
                const unsigned char* pixel = owned.pData + static_cast<size_t>(y) * owned.stride + x * owned.channels;
                if (pixel[0] != expected[0] || pixel[1] != expected[1] || pixel[2] != expected[2]) {
                    roiCopied = false;
                    break;
                }
            }
        }
    }
    CoTaskMemFree(owned.pData);

    HImage unsupported{};
    unsupported.rows = 123;
    unsupported.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    cv::Mat signedMat(2, 2, CV_8SC1);
    const int unsupportedRet = MatToHImage(signedMat, &unsupported);

    return validView
        && HImageToMatView(invalidChannels).empty()
        && HImageToMatView(invalidStride).empty()
        && HImageToMatView(negativeStride).empty()
        && valid.elemSize() == 3
        && invalidChannels.elemSize() == 0
        && roiCopied
        && unsupportedRet == -4
        && unsupported.rows == 0
        && unsupported.pData == nullptr;
}

bool smokeCommonExportsFailSafely()
{
    SetInitialFrame(nullptr);
    SetUpdateFrame(nullptr);

    const bool nullVideoPathFails = ReadVideoTest(nullptr) == -1;

    SetInitialFrame(smokeInitialFrameCallback);
    const bool emptyVideoPathFails = ReadVideoTest("") == -1;

    SetInitialFrame(nullptr);
    SetUpdateFrame(nullptr);

    return nullVideoPathFails
        && emptyVideoPathFails;
}

// Helper function: create test image (bright area in center)
cv::Mat createTestImage(int width, int height, int brightWidth, int brightHeight)
{
    cv::Mat image = cv::Mat::zeros(height, width, CV_8UC1);

    // Create bright area in center
    int startX = (width - brightWidth) / 2;
    int startY = (height - brightHeight) / 2;
    cv::Rect brightArea(startX, startY, brightWidth, brightHeight);
    image(brightArea) = 200; // Set bright area pixel value

    // Add some noise to make it more realistic
    cv::Mat noise(height, width, CV_8UC1);
    cv::randn(noise, 0, 10);
    image += noise;

    return image;
}

cv::Mat createSlantedEdgeImage(int width, int height)
{
    cv::Mat image(height, width, CV_8UC1);
    const double center = width * 0.5;
    for (int y = 0; y < height; ++y) {
        const double edgeX = center + 0.18 * (y - height * 0.5);
        for (int x = 0; x < width; ++x) {
            image.at<unsigned char>(y, x) = x >= edgeX ? 230 : 20;
        }
    }

    cv::GaussianBlur(image, image, cv::Size(3, 3), 0.8);
    return image;
}

// Helper function: create HImage from cv::Mat
HImage createHImageFromMat(const cv::Mat& mat)
{
    HImage himg;
    himg.rows = mat.rows;
    himg.cols = mat.cols;
    himg.channels = mat.channels();
    himg.depth = static_cast<int>(mat.elemSize1() * 8);
    himg.stride = static_cast<int>(mat.step);
    himg.isDispose = true;
    himg.pData = const_cast<unsigned char*>(mat.data);
    return himg;
}

bool smokeFindLuminousArea()
{
    cv::Mat image = createTestImage(320, 240, 80, 60);
    HImage himg = createHImageFromMat(image);
    RoiRect roi = {0, 0, 0, 0};

    json config;
    config["Threshold"] = 100;
    config["UseRotatedRect"] = false;
    std::string configStr = config.dump();

    char* result = nullptr;
    const int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);
    if (ret <= 0 || result == nullptr) {
        return false;
    }

    json resultJson = json::parse(result, nullptr, false);
    FreeResult(result);
    return !resultJson.is_discarded()
        && resultJson.contains("X")
        && resultJson.contains("Y")
        && resultJson.contains("Width")
        && resultJson.contains("Height");
}

bool smokeInvalidJsonDoesNotThrow()
{
    cv::Mat image = createTestImage(320, 240, 80, 60);
    HImage himg = createHImageFromMat(image);
    RoiRect roi = {0, 0, 0, 0};

    char* result = nullptr;
    const int ret = M_FindLuminousArea(himg, roi, "{", &result);
    if (result != nullptr) {
        FreeResult(result);
        return false;
    }

    return ret < 0;
}

bool smokeFreeResultAcceptsNull()
{
    return FreeResult(nullptr) == 0;
}

bool smokeCalArtculationInvalidImageDoesNotThrow()
{
    HImage invalid{};
    RoiRect roi = { 0, 0, 0, 0 };
    return M_CalArtculation(invalid, Variance, roi) == -1.0;
}

bool smokeCalArtculationUsesRawPixelScale()
{
    cv::Mat image8(2, 2, CV_8UC1);
    image8.at<unsigned char>(0, 0) = 0;
    image8.at<unsigned char>(0, 1) = 255;
    image8.at<unsigned char>(1, 0) = 255;
    image8.at<unsigned char>(1, 1) = 0;

    cv::Mat image16(2, 2, CV_16UC1);
    image16.at<unsigned short>(0, 0) = 0;
    image16.at<unsigned short>(0, 1) = 65535;
    image16.at<unsigned short>(1, 0) = 65535;
    image16.at<unsigned short>(1, 1) = 0;

    RoiRect roi = { 0, 0, 0, 0 };
    HImage hImage8 = createHImageFromMat(image8);
    HImage hImage16 = createHImageFromMat(image16);

    const double variance8 = M_CalArtculation(hImage8, Variance, roi);
    const double stddev8 = M_CalArtculation(hImage8, StandardDeviation, roi);
    const double variance16 = M_CalArtculation(hImage16, Variance, roi);
    const double stddev16 = M_CalArtculation(hImage16, StandardDeviation, roi);

    return nearlyEqual(variance8, 16256.25, 1e-9)
        && nearlyEqual(stddev8, 127.5, 1e-9)
        && nearlyEqual(variance16, 1073709056.25, 1e-3)
        && nearlyEqual(stddev16, 32767.5, 1e-9);
}

bool smokeCalArtculationGray32FloatDoesNotMutateSource()
{
    cv::Mat image(2, 3, CV_32FC1);
    image.at<float>(0, 0) = 1.0f;
    image.at<float>(0, 1) = std::numeric_limits<float>::quiet_NaN();
    image.at<float>(0, 2) = 3.0f;
    image.at<float>(1, 0) = 5.0f;
    image.at<float>(1, 1) = 7.0f;
    image.at<float>(1, 2) = 9.0f;

    HImage hImage = createHImageFromMat(image);
    RoiRect roi = { 0, 0, 0, 0 };
    const double result = M_CalArtculation(hImage, Variance, roi);

    return result >= 0.0
        && std::isnan(image.at<float>(0, 1))
        && image.at<float>(0, 0) == 1.0f
        && image.at<float>(1, 2) == 9.0f;
}

bool smokeGetMinMaxClearsOutputsOnFailure()
{
    uint minValue = 123;
    uint maxValue = 456;

    const int retMissingMax = M_GetMinMax(HImage{}, &minValue, nullptr, -1);
    const bool minCleared = retMissingMax < 0 && minValue == 0;

    minValue = 789;
    maxValue = 321;
    const int retMissingMin = M_GetMinMax(HImage{}, nullptr, &maxValue, -1);
    const bool maxCleared = retMissingMin < 0 && maxValue == 0;

    return minCleared && maxCleared;
}

bool smokeSfrOutputsClearOnFailure()
{
    constexpr int maxLen = 8;
    RoiRect roi = { 0, 0, 0, 0 };

    double freq[maxLen]{};
    int outLen = 123;
    double mtf10 = 1.0;
    double mtf50 = 2.0;
    double mtf10Cy = 3.0;
    double mtf50Cy = 4.0;

    const int singleRet = M_CalSFR(
        HImage{},
        1.0,
        roi,
        freq,
        nullptr,
        maxLen,
        &outLen,
        &mtf10,
        &mtf50,
        &mtf10Cy,
        &mtf50Cy);

    const bool singleCleared = singleRet == -1
        && outLen == 0
        && mtf10 == 0.0
        && mtf50 == 0.0
        && mtf10Cy == 0.0
        && mtf50Cy == 0.0;

    cv::Mat rgb(16, 16, CV_8UC3, cv::Scalar(32, 64, 128));
    HImage rgbImage = createHImageFromMat(rgb);
    double sfrG[maxLen]{};
    double sfrB[maxLen]{};
    double sfrL[maxLen]{};
    int multiOutLen = 456;
    int channelCount = 789;
    double mtf10R = 1.0, mtf50R = 2.0, mtf10CyR = 3.0, mtf50CyR = 4.0;
    double mtf10G = 5.0, mtf50G = 6.0, mtf10CyG = 7.0, mtf50CyG = 8.0;
    double mtf10B = 9.0, mtf50B = 10.0, mtf10CyB = 11.0, mtf50CyB = 12.0;
    double mtf10L = 13.0, mtf50L = 14.0, mtf10CyL = 15.0, mtf50CyL = 16.0;

    const int multiRet = M_CalSFRMultiChannel(
        rgbImage,
        1.0,
        roi,
        freq,
        nullptr,
        sfrG,
        sfrB,
        sfrL,
        maxLen,
        &multiOutLen,
        &channelCount,
        &mtf10R, &mtf50R, &mtf10CyR, &mtf50CyR,
        &mtf10G, &mtf50G, &mtf10CyG, &mtf50CyG,
        &mtf10B, &mtf50B, &mtf10CyB, &mtf50CyB,
        &mtf10L, &mtf50L, &mtf10CyL, &mtf50CyL);

    const bool multiCleared = multiRet == -1
        && multiOutLen == 0
        && channelCount == 0
        && mtf10R == 0.0 && mtf50R == 0.0 && mtf10CyR == 0.0 && mtf50CyR == 0.0
        && mtf10G == 0.0 && mtf50G == 0.0 && mtf10CyG == 0.0 && mtf50CyG == 0.0
        && mtf10B == 0.0 && mtf50B == 0.0 && mtf10CyB == 0.0 && mtf50CyB == 0.0
        && mtf10L == 0.0 && mtf50L == 0.0 && mtf10CyL == 0.0 && mtf50CyL == 0.0;

    return singleCleared && multiCleared;
}

bool hasFiniteSfrCurve(const double* freq, const double* sfr, int outLen)
{
    if (freq == nullptr || sfr == nullptr || outLen <= 1) {
        return false;
    }

    for (int i = 0; i < outLen; ++i) {
        if (!std::isfinite(freq[i]) || !std::isfinite(sfr[i])) {
            return false;
        }
    }
    return freq[0] == 0.0 && sfr[0] >= 0.0;
}

bool smokeSfrCalculatesSyntheticSlantedEdge()
{
    constexpr int maxLen = 512;
    RoiRect roi = { 0, 0, 0, 0 };

    cv::Mat gray = createSlantedEdgeImage(96, 80);
    HImage grayImage = createHImageFromMat(gray);

    double freq[maxLen]{};
    double sfr[maxLen]{};
    int outLen = 0;
    double mtf10 = 0.0, mtf50 = 0.0, mtf10Cy = 0.0, mtf50Cy = 0.0;

    const int singleRet = M_CalSFR(
        grayImage,
        1.0,
        roi,
        freq,
        sfr,
        maxLen,
        &outLen,
        &mtf10,
        &mtf50,
        &mtf10Cy,
        &mtf50Cy);

    const bool singleOk = singleRet == 0
        && hasFiniteSfrCurve(freq, sfr, outLen)
        && std::isfinite(mtf10)
        && std::isfinite(mtf50)
        && std::isfinite(mtf10Cy)
        && std::isfinite(mtf50Cy);

    std::vector<cv::Mat> bgr = { gray, gray, gray };
    cv::Mat color;
    cv::merge(bgr, color);
    HImage colorImage = createHImageFromMat(color);

    double freqMulti[maxLen]{};
    double sfrR[maxLen]{};
    double sfrG[maxLen]{};
    double sfrB[maxLen]{};
    double sfrL[maxLen]{};
    int multiOutLen = 0;
    int channelCount = 0;
    double mtf10R = 0.0, mtf50R = 0.0, mtf10CyR = 0.0, mtf50CyR = 0.0;
    double mtf10G = 0.0, mtf50G = 0.0, mtf10CyG = 0.0, mtf50CyG = 0.0;
    double mtf10B = 0.0, mtf50B = 0.0, mtf10CyB = 0.0, mtf50CyB = 0.0;
    double mtf10L = 0.0, mtf50L = 0.0, mtf10CyL = 0.0, mtf50CyL = 0.0;

    const int multiRet = M_CalSFRMultiChannel(
        colorImage,
        1.0,
        roi,
        freqMulti,
        sfrR,
        sfrG,
        sfrB,
        sfrL,
        maxLen,
        &multiOutLen,
        &channelCount,
        &mtf10R, &mtf50R, &mtf10CyR, &mtf50CyR,
        &mtf10G, &mtf50G, &mtf10CyG, &mtf50CyG,
        &mtf10B, &mtf50B, &mtf10CyB, &mtf50CyB,
        &mtf10L, &mtf50L, &mtf10CyL, &mtf50CyL);

    const bool multiOk = multiRet == 0
        && channelCount == 4
        && hasFiniteSfrCurve(freqMulti, sfrR, multiOutLen)
        && hasFiniteSfrCurve(freqMulti, sfrG, multiOutLen)
        && hasFiniteSfrCurve(freqMulti, sfrB, multiOutLen)
        && hasFiniteSfrCurve(freqMulti, sfrL, multiOutLen)
        && std::isfinite(mtf50R)
        && std::isfinite(mtf50G)
        && std::isfinite(mtf50B)
        && std::isfinite(mtf50L);

    return singleOk && multiOk;
}

bool smokeSfrMatchesSfrmat5MonoFixture()
{
    namespace fs = std::filesystem;
    const fs::path imagePath = findTestDataFile(fs::path("data") / "sfrmat5" / "Test_edge1_mono.tif");
    if (imagePath.empty()) {
        std::cerr << "Missing sfrmat5 fixture image" << std::endl;
        return false;
    }

    cv::Mat image = cv::imread(imagePath.string(), cv::IMREAD_GRAYSCALE);
    if (image.empty()) {
        std::cerr << "Unable to read sfrmat5 fixture image: " << imagePath.string() << std::endl;
        return false;
    }

    constexpr int maxLen = 512;
    double freq[maxLen]{};
    double sfr[maxLen]{};
    int outLen = 0;
    double mtf10 = 0.0, mtf50 = 0.0, mtf10Cy = 0.0, mtf50Cy = 0.0;

    HImage hImage = createHImageFromMat(image);
    RoiRect roi = { 0, 0, 0, 0 };
    const int ret = M_CalSFR(
        hImage,
        1.0,
        roi,
        freq,
        sfr,
        maxLen,
        &outLen,
        &mtf10,
        &mtf50,
        &mtf10Cy,
        &mtf50Cy);

    if (ret != 0 || outLen < 8) {
        return false;
    }

    const double expectedFreqHead[] = {
        0.0,
        0.00810146190394791,
        0.0162029238078958,
        0.0243043857118437
    };
    const double expectedSfrHead[] = {
        1.0,
        0.994476749384265,
        0.981007096897886,
        0.964310831297654
    };

    bool headOk = true;
    for (int i = 0; i < 4; ++i) {
        headOk = headOk
            && nearlyEqual(freq[i], expectedFreqHead[i], 1e-9)
            && nearlyEqual(sfr[i], expectedSfrHead[i], 5e-4);
    }

    return outLen == 125
        && headOk
        && nearlyEqual(mtf50Cy, 0.275311298814052, 5e-4)
        && nearlyEqual(mtf50, mtf50Cy / 0.5, 1e-9)
        && mtf10Cy > mtf50Cy
        && mtf10Cy <= 0.495;
}

bool checkHead(const double* actual, const double* expected, int count, double tolerance)
{
    for (int i = 0; i < count; ++i) {
        if (!nearlyEqual(actual[i], expected[i], tolerance)) {
            return false;
        }
    }
    return true;
}

bool smokeSfrMatchesSfrmat5ColorFixture()
{
    namespace fs = std::filesystem;
    const fs::path imagePath = findTestDataFile(fs::path("data") / "sfrmat5" / "Test_edge1.tif");
    if (imagePath.empty()) {
        std::cerr << "Missing sfrmat5 color fixture image" << std::endl;
        return false;
    }

    cv::Mat image = cv::imread(imagePath.string(), cv::IMREAD_COLOR);
    if (image.empty() || image.channels() != 3) {
        std::cerr << "Unable to read sfrmat5 color fixture image: " << imagePath.string() << std::endl;
        return false;
    }

    constexpr int maxLen = 512;
    double freq[maxLen]{};
    double sfrR[maxLen]{};
    double sfrG[maxLen]{};
    double sfrB[maxLen]{};
    double sfrL[maxLen]{};
    int outLen = 0;
    int channelCount = 0;
    double mtf10R = 0.0, mtf50R = 0.0, mtf10CyR = 0.0, mtf50CyR = 0.0;
    double mtf10G = 0.0, mtf50G = 0.0, mtf10CyG = 0.0, mtf50CyG = 0.0;
    double mtf10B = 0.0, mtf50B = 0.0, mtf10CyB = 0.0, mtf50CyB = 0.0;
    double mtf10L = 0.0, mtf50L = 0.0, mtf10CyL = 0.0, mtf50CyL = 0.0;

    HImage hImage = createHImageFromMat(image);
    RoiRect roi = { 0, 0, 0, 0 };
    const int ret = M_CalSFRMultiChannel(
        hImage,
        1.0,
        roi,
        freq,
        sfrR,
        sfrG,
        sfrB,
        sfrL,
        maxLen,
        &outLen,
        &channelCount,
        &mtf10R, &mtf50R, &mtf10CyR, &mtf50CyR,
        &mtf10G, &mtf50G, &mtf10CyG, &mtf50CyG,
        &mtf10B, &mtf50B, &mtf10CyB, &mtf50CyB,
        &mtf10L, &mtf50L, &mtf10CyL, &mtf50CyL);

    if (ret != 0 || channelCount != 4 || outLen != 125) {
        return false;
    }

    const double expectedFreqHead[] = {
        0.0,
        0.00810162007171458,
        0.0162032401434292,
        0.0243048602151437
    };
    const double expectedRHead[] = { 1.0, 0.994170788105408, 0.979802607375787, 0.961734032776102 };
    const double expectedGHead[] = { 1.0, 0.994336792329949, 0.980432969921805, 0.963128342257561 };
    const double expectedBHead[] = { 1.0, 0.994452465494764, 0.980877926545115, 0.963960313609583 };
    const double expectedLHead[] = { 1.0, 0.994307358099263, 0.980331198399311, 0.962885281074278 };

    const bool headsOk = checkHead(freq, expectedFreqHead, 4, 1e-9)
        && checkHead(sfrR, expectedRHead, 4, 5e-4)
        && checkHead(sfrG, expectedGHead, 4, 5e-4)
        && checkHead(sfrB, expectedBHead, 4, 5e-4)
        && checkHead(sfrL, expectedLHead, 4, 5e-4);

    return headsOk
        && nearlyEqual(mtf10CyR, 0.418457380085611, 5e-4)
        && nearlyEqual(mtf10CyG, 0.42082300769394, 5e-4)
        && nearlyEqual(mtf10CyB, 0.424989047410745, 5e-4)
        && nearlyEqual(mtf10CyL, 0.420342418069725, 5e-4)
        && nearlyEqual(mtf50CyR, 0.26980517721886, 5e-4)
        && nearlyEqual(mtf50CyG, 0.272567771934206, 5e-4)
        && nearlyEqual(mtf50CyB, 0.275718530671458, 5e-4)
        && nearlyEqual(mtf50CyL, 0.27195747068128, 5e-4)
        && nearlyEqual(mtf50R, mtf50CyR / 0.5, 1e-9)
        && nearlyEqual(mtf50G, mtf50CyG / 0.5, 1e-9)
        && nearlyEqual(mtf50B, mtf50CyB / 0.5, 1e-9)
        && nearlyEqual(mtf50L, mtf50CyL / 0.5, 1e-9);
}

cv::Mat makeSyntheticBmwTargetImage()
{
    const int size = 720;
    const cv::Scalar background(95, 145, 82);
    cv::Mat image(size, size, CV_8UC3, background);

    cv::Point center(size / 2, size / 2);
    cv::Size radius(165, 165);
    cv::Scalar black(8, 8, 8);

    cv::ellipse(image, center, radius, 0.0, 270.0, 360.0, black, cv::FILLED, cv::LINE_AA);
    cv::ellipse(image, center, radius, 0.0, 90.0, 180.0, black, cv::FILLED, cv::LINE_AA);
    cv::GaussianBlur(image, image, cv::Size(5, 5), 0.9);

    cv::Mat rotated;
    cv::Mat transform = cv::getRotationMatrix2D(center, 4.0, 1.0);
    cv::warpAffine(image, rotated, transform, image.size(), cv::INTER_LINEAR, cv::BORDER_CONSTANT, background);
    return rotated;
}

bool smokeSfrBmw4In1SyntheticTarget()
{
    cv::Mat image = makeSyntheticBmwTargetImage();
    HImage hImage = createHImageFromMat(image);

    json config;
    config["MinArea"] = 2000;
    config["MaxTargets"] = 1;
    config["RoiWidth"] = 72;
    config["RoiHeight"] = 72;
    config["CloseKernel"] = 17;
    config["EdgeOffsetRatio"] = 0.42;
    config["MaxCurveLength"] = 128;

    char* result = nullptr;
    const int ret = M_CalSFRBmw4In1(hImage, { 0, 0, 0, 0 }, config.dump().c_str(), &result);

    bool ok = false;
    if (ret > 0 && result != nullptr) {
        json output = json::parse(result, nullptr, false);
        ok = !output.is_discarded()
            && output.contains("result")
            && output["result"].is_array()
            && !output["result"].empty();

        if (ok) {
            const json& point = output["result"][0];
            ok = point.contains("data")
                && point["data"].is_array()
                && point["data"].size() == 4;
        }

        if (ok) {
            for (const auto& curve : output["result"][0]["data"]) {
                ok = curve.contains("id")
                    && curve.contains("frequency")
                    && curve.contains("domainSamplingData")
                    && curve["frequency"].is_array()
                    && curve["domainSamplingData"].is_array()
                    && curve["frequency"].size() >= 8
                    && curve["domainSamplingData"].size() == curve["frequency"].size();
                if (!ok) {
                    break;
                }
            }
        }
    }

    FreeResult(result);
    return ok;
}

cv::Mat makeSyntheticDistortionP9Image()
{
    cv::Mat image(560, 560, CV_8UC1, cv::Scalar(12));
    const std::array<cv::Point, 9> points = {
        cv::Point(120, 98), cv::Point(280, 116), cv::Point(440, 96),
        cv::Point(136, 280), cv::Point(280, 280), cv::Point(424, 280),
        cv::Point(118, 444), cv::Point(280, 424), cv::Point(442, 446)
    };

    for (const cv::Point& point : points) {
        cv::circle(image, point, 24, cv::Scalar(230), cv::FILLED, cv::LINE_AA);
    }

    cv::GaussianBlur(image, image, cv::Size(3, 3), 0.6);
    return image;
}

bool validateDistortionP9Json(const json& output)
{
    if (output.is_discarded()
        || !output.value("success", false)
        || !output.contains("points")
        || !output["points"].is_array()
        || output["points"].size() != 9
        || !output.contains("metrics")
        || !output["metrics"].is_object()) {
        return false;
    }

    const json& metrics = output["metrics"];
    const double h = metrics.value("horizontalTvPercent", std::numeric_limits<double>::quiet_NaN());
    const double v = metrics.value("verticalTvPercent", std::numeric_limits<double>::quiet_NaN());
    return std::isfinite(h) && std::isfinite(v);
}

bool smokeDistortionP9SyntheticTarget()
{
    cv::Mat image = makeSyntheticDistortionP9Image();
    HImage hImage = createHImageFromMat(image);

    json config;
    config["threshold"] = 100;
    config["minRectSize"] = 30;
    config["maxRectSize"] = 80;

    char* result = nullptr;
    const int ret = M_CalDistortionP9(hImage, { 0, 0, 0, 0 }, config.dump().c_str(), &result);

    bool ok = false;
    if (ret > 0 && result != nullptr) {
        json output = json::parse(result, nullptr, false);
        ok = validateDistortionP9Json(output);
        if (ok) {
            ok = output["points"][0].value("name", "") == "TL"
                && output["points"][4].value("name", "") == "C"
                && output["points"][8].value("name", "") == "BR"
                && output["metrics"].value("horizontalTvPercent", 0.0) > 10.0
                && output["metrics"].value("verticalTvPercent", 0.0) > 10.0;
        }
    }

    FreeResult(result);
    return ok;
}

bool smokeDistortionP9ReportsMissingPoint()
{
    cv::Mat image = makeSyntheticDistortionP9Image();
    cv::circle(image, cv::Point(442, 446), 36, cv::Scalar(12), cv::FILLED, cv::LINE_AA);
    HImage hImage = createHImageFromMat(image);

    json config;
    config["threshold"] = 100;
    config["minRectSize"] = 30;
    config["maxRectSize"] = 80;

    char* result = nullptr;
    const int ret = M_CalDistortionP9(hImage, { 0, 0, 0, 0 }, config.dump().c_str(), &result);

    bool ok = false;
    if (ret > 0 && result != nullptr) {
        json output = json::parse(result, nullptr, false);
        ok = !output.is_discarded()
            && output.value("success", true) == false
            && output.value("statusCode", "") == "too_few_candidates"
            && output.value("candidateCount", 0) == 8
            && output.contains("candidatePoints")
            && output["candidatePoints"].is_array()
            && output["candidatePoints"].size() == 8
            && output.contains("diagnostics")
            && output["diagnostics"].value("missingCount", 0) == 1;
    }

    FreeResult(result);
    return ok;
}

bool smokeDistortionP9ReportsExtraCandidateWarning()
{
    cv::Mat image = makeSyntheticDistortionP9Image();
    cv::circle(image, cv::Point(52, 52), 20, cv::Scalar(230), cv::FILLED, cv::LINE_AA);
    HImage hImage = createHImageFromMat(image);

    json config;
    config["threshold"] = 100;
    config["minRectSize"] = 30;
    config["maxRectSize"] = 80;

    char* result = nullptr;
    const int ret = M_CalDistortionP9(hImage, { 0, 0, 0, 0 }, config.dump().c_str(), &result);

    bool ok = false;
    if (ret > 0 && result != nullptr) {
        json output = json::parse(result, nullptr, false);
        ok = validateDistortionP9Json(output)
            && output.value("statusCode", "") == "ok_with_warnings"
            && output.value("candidateCount", 0) == 10
            && output.contains("warnings")
            && output["warnings"].is_array()
            && !output["warnings"].empty()
            && output.contains("candidatePoints")
            && output["candidatePoints"].is_array()
            && output["candidatePoints"].size() == 10
            && output["diagnostics"].value("extraCount", 0) == 1;
    }

    FreeResult(result);
    return ok;
}

std::filesystem::path findDesktopDistortionP9Fixture()
{
    char* userProfile = nullptr;
    size_t userProfileLength = 0;
    if (_dupenv_s(&userProfile, &userProfileLength, "USERPROFILE") != 0 || userProfile == nullptr || userProfile[0] == '\0') {
        std::free(userProfile);
        return {};
    }

    std::filesystem::path path = std::filesystem::path(userProfile) / "Desktop" / "DistortionP9" / "DistortionP9.tiff";
    std::free(userProfile);
    return std::filesystem::exists(path) ? path : std::filesystem::path();
}

bool smokeDistortionP9DesktopFixtureIfPresent()
{
    namespace fs = std::filesystem;
    fs::path imagePath = findDesktopDistortionP9Fixture();
    if (imagePath.empty()) {
        std::cout << "DistortionP9 desktop fixture not present; skipped" << std::endl;
        return true;
    }

    cv::Mat image = cv::imread(imagePath.string(), cv::IMREAD_UNCHANGED);
    if (image.empty()) {
        std::cerr << "Unable to read DistortionP9 fixture: " << imagePath.string() << std::endl;
        return false;
    }

    HImage hImage = createHImageFromMat(image);

    json config;
    config["CommonParams"] = { { "brightNumX", 3 }, { "brightNumY", 3 } };
    config["Point9Params"] = {
        { "threshold", 20000 },
        { "outRectSizeMin", 40 },
        { "outRectSizeMax", 400 },
        { "erodeKernel", 3 },
        { "erodeTime", 0 }
    };

    char* result = nullptr;
    const int ret = M_CalDistortionP9(hImage, { 0, 0, 0, 0 }, config.dump().c_str(), &result);

    bool ok = false;
    if (ret > 0 && result != nullptr) {
        json output = json::parse(result, nullptr, false);
        ok = validateDistortionP9Json(output);
        if (ok) {
            const json& center = output["points"][4];
            const json& metrics = output["metrics"];
            ok = nearlyEqual(center.value("x", 0.0), 4798.57, 5.0)
                && nearlyEqual(center.value("y", 0.0), 3217.30, 5.0)
                && nearlyEqual(metrics.value("horizontalTvPercent", 0.0), 9.58, 0.5)
                && nearlyEqual(metrics.value("verticalTvPercent", 0.0), 9.58, 0.5);
        }
    }

    FreeResult(result);
    return ok;
}

bool smokeSurfaceDefectDetectsSyntheticBrightAndDark()
{
    cv::Mat image(160, 220, CV_8UC1, cv::Scalar(128));
    cv::rectangle(image, cv::Rect(42, 46, 24, 18), cv::Scalar(190), cv::FILLED);
    cv::rectangle(image, cv::Rect(142, 92, 28, 20), cv::Scalar(70), cv::FILLED);

    HImage hImage = createHImageFromMat(image);
    RoiRect roi = { 0, 0, 0, 0 };
    json config = {
        { "scales", { 31 } },
        { "brightThreshold", 0.05 },
        { "darkThreshold", 0.05 },
        { "minArea", 20 },
        { "muraMinArea", 2000 },
        { "openKernel", 1 },
        { "closeKernel", 3 },
        { "mergeDistance", 5 }
    };

    char* result = nullptr;
    int ret = M_DetectSurfaceDefects(hImage, roi, config.dump().c_str(), &result);
    if (ret <= 0 || result == nullptr) {
        return false;
    }

    json output = json::parse(result, nullptr, false);
    FreeResult(result);
    if (output.is_discarded() || !output.value("success", false)) {
        return false;
    }

    const auto& summary = output.at("summary");
    if (summary.value("brightCount", 0) < 1 || summary.value("darkCount", 0) < 1) {
        return false;
    }

    bool hasBright = false;
    bool hasDark = false;
    for (const auto& defect : output.at("defects")) {
        const std::string polarity = defect.value("polarity", "");
        hasBright = hasBright || polarity == "bright";
        hasDark = hasDark || polarity == "dark";
    }

    return hasBright && hasDark;
}

namespace {

using SurfaceDefectConfig = cvcore::surface_defect::SurfaceDefectConfig;
using SurfaceDefectItem = cvcore::surface_defect::SurfaceDefectItem;
using SurfaceDefectResult = cvcore::surface_defect::SurfaceDefectResult;

constexpr double kSurfaceDefectComparisonTolerance = 1e-9;

struct SurfaceDefectTestCase
{
    std::string name;
    cv::Mat image;
    RoiRect roi{ 0, 0, 0, 0 };
    SurfaceDefectConfig config;
};

struct SurfaceDefectDiffReport
{
    bool exactMatch = true;
    double maxNumericDiff = 0.0;
    int mismatchCount = 0;
    std::vector<std::string> samples;
};

struct SurfaceDefectBenchmarkRow
{
    std::string name;
    int requestedComponents = 0;
    int detectedComponents = 0;
    double prepareMs = 0.0;
    double coldMs = 0.0;
    double warmMs = 0.0;
};

int normalizedOddKernel(int value)
{
    if (value <= 1) {
        return 0;
    }

    return (value % 2 == 0) ? value + 1 : value;
}

std::vector<int> normalizedScales(const std::vector<int>& scales)
{
    std::vector<int> output;
    output.reserve(scales.size());
    for (int scale : scales) {
        int normalized = normalizedOddKernel(scale);
        if (normalized > 1) {
            output.push_back(normalized);
        }
    }

    if (output.empty()) {
        output = { 31, 61, 121 };
    }

    std::sort(output.begin(), output.end());
    output.erase(std::unique(output.begin(), output.end()), output.end());
    return output;
}

cv::Mat selectAnalysisChannel(const cv::Mat& image, int channel)
{
    if (image.empty()) {
        return {};
    }

    cv::Mat gray;
    if (image.channels() == 1) {
        gray = image;
    }
    else if (channel >= 0 && channel < image.channels()) {
        cv::extractChannel(image, gray, channel);
    }
    else if (image.channels() == 3) {
        cv::cvtColor(image, gray, cv::COLOR_BGR2GRAY);
    }
    else if (image.channels() == 4) {
        cv::cvtColor(image, gray, cv::COLOR_BGRA2GRAY);
    }

    return gray;
}

bool convertToAnalysisFloat(const cv::Mat& image, int channel, cv::Mat& gray32)
{
    cv::Mat gray = selectAnalysisChannel(image, channel);
    if (gray.empty()) {
        return false;
    }

    double scale = 1.0;
    switch (gray.depth())
    {
    case CV_8U:
        scale = 1.0 / 255.0;
        break;
    case CV_16U:
        scale = 1.0 / 65535.0;
        break;
    case CV_32F:
    case CV_64F:
        scale = 1.0;
        break;
    default:
        return false;
    }

    gray.convertTo(gray32, CV_32F, scale);
    cv::patchNaNs(gray32, 0.0);
    return !gray32.empty();
}

void buildSignedRelativeDelta(const cv::Mat& source32, int scale, cv::Mat& delta)
{
    cv::Mat background;
    cv::GaussianBlur(source32, background, cv::Size(scale, scale), 0.0, 0.0, cv::BORDER_REPLICATE);

    cv::Mat denominator;
    cv::absdiff(background, cv::Scalar::all(0.0), denominator);
    cv::Mat epsilon(denominator.size(), denominator.type(), cv::Scalar::all(1e-6));
    cv::max(denominator, epsilon, denominator);

    cv::subtract(source32, background, delta);
    cv::divide(delta, denominator, delta);
}

void thresholdResidual(const cv::Mat& residual, double threshold, int openKernel, int closeKernel, cv::Mat& mask)
{
    cv::threshold(residual, mask, threshold, 255.0, cv::THRESH_BINARY);
    mask.convertTo(mask, CV_8U);

    int openSize = normalizedOddKernel(openKernel);
    if (openSize > 1) {
        cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(openSize, openSize));
        cv::morphologyEx(mask, mask, cv::MORPH_OPEN, kernel);
    }

    int closeSize = normalizedOddKernel(closeKernel);
    if (closeSize > 1) {
        cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(closeSize, closeSize));
        cv::morphologyEx(mask, mask, cv::MORPH_CLOSE, kernel);
    }
}

double aspectRatio(const cv::Rect& rect)
{
    const int minSide = (std::max)(1, (std::min)(rect.width, rect.height));
    const int maxSide = (std::max)(rect.width, rect.height);
    return static_cast<double>(maxSide) / static_cast<double>(minSide);
}

std::string gradeForSeverity(double severity, const SurfaceDefectConfig& config)
{
    if (severity >= config.criticalSeverity) {
        return "critical";
    }
    if (severity >= config.majorSeverity) {
        return "major";
    }
    if (severity >= config.minorSeverity) {
        return "minor";
    }
    return severity > 0.0 ? "trace" : "ok";
}

std::string classifyDefect(const std::string& polarity, int area, double aspect, int scale, const SurfaceDefectConfig& config)
{
    if (config.enableLineDetect && aspect >= config.lineAspectRatio) {
        return polarity == "bright" ? "brightLine" : "darkLine";
    }

    if (area >= config.muraMinArea || scale >= 61) {
        return polarity == "bright" ? "brightMura" : "darkMura";
    }

    return polarity == "bright" ? "brightSpot" : "darkSpot";
}

bool rectsTouchOrOverlap(const cv::Rect& a, const cv::Rect& b, int distance)
{
    cv::Rect expanded(
        a.x - distance,
        a.y - distance,
        a.width + distance * 2,
        a.height + distance * 2);
    return (expanded & b).area() > 0;
}

json surfaceDefectConfigToJson(const SurfaceDefectConfig& config)
{
    return json{
        { "channel", config.channel },
        { "scales", config.scales },
        { "darkThreshold", config.darkThreshold },
        { "brightThreshold", config.brightThreshold },
        { "minArea", config.minArea },
        { "maxArea", config.maxArea },
        { "muraMinArea", config.muraMinArea },
        { "openKernel", config.openKernel },
        { "closeKernel", config.closeKernel },
        { "mergeDistance", config.mergeDistance },
        { "maxDefects", config.maxDefects },
        { "enableDark", config.enableDark },
        { "enableBright", config.enableBright },
        { "enableLineDetect", config.enableLineDetect },
        { "lineAspectRatio", config.lineAspectRatio },
        { "minSeverity", config.minSeverity },
        { "minorSeverity", config.minorSeverity },
        { "majorSeverity", config.majorSeverity },
        { "criticalSeverity", config.criticalSeverity }
    };
}

void appendComponentsReference(
    const cv::Mat& signedDelta,
    const cv::Mat& mask,
    const std::string& polarity,
    int scale,
    const SurfaceDefectConfig& config,
    std::vector<SurfaceDefectItem>& defects)
{
    cv::Mat labels;
    cv::Mat stats;
    cv::Mat centroids;
    const int labelCount = cv::connectedComponentsWithStats(mask, labels, stats, centroids, 8, CV_32S);

    for (int label = 1; label < labelCount; ++label) {
        const int area = stats.at<int>(label, cv::CC_STAT_AREA);
        if (area < config.minArea || (config.maxArea > 0 && area > config.maxArea)) {
            continue;
        }

        const cv::Rect rect(
            stats.at<int>(label, cv::CC_STAT_LEFT),
            stats.at<int>(label, cv::CC_STAT_TOP),
            stats.at<int>(label, cv::CC_STAT_WIDTH),
            stats.at<int>(label, cv::CC_STAT_HEIGHT));
        if (rect.width <= 0 || rect.height <= 0) {
            continue;
        }

        cv::Mat componentMask;
        cv::compare(labels, label, componentMask, cv::CMP_EQ);

        cv::Scalar mean = cv::mean(signedDelta, componentMask);
        double minDelta = 0.0;
        double maxDelta = 0.0;
        cv::minMaxLoc(signedDelta, &minDelta, &maxDelta, nullptr, nullptr, componentMask);

        const double maxDeltaAbs = (std::max)(std::abs(minDelta), std::abs(maxDelta));
        const double severity = maxDeltaAbs * std::sqrt(static_cast<double>(area));
        if (severity < config.minSeverity) {
            continue;
        }

        const double aspect = aspectRatio(rect);
        SurfaceDefectItem item;
        item.scale = scale;
        item.polarity = polarity;
        item.boundingRect = rect;
        item.center = cv::Point2d(centroids.at<double>(label, 0), centroids.at<double>(label, 1));
        item.area = area;
        item.meanDelta = mean[0];
        item.minDelta = minDelta;
        item.maxDelta = maxDelta;
        item.maxDeltaAbs = maxDeltaAbs;
        item.severity = severity;
        item.aspectRatio = aspect;
        item.fillRatio = static_cast<double>(area) / static_cast<double>((std::max)(1, rect.area()));
        item.type = classifyDefect(polarity, area, aspect, scale, config);
        defects.push_back(std::move(item));
    }
}

std::vector<SurfaceDefectItem> mergeDefectsReference(std::vector<SurfaceDefectItem> defects, const SurfaceDefectConfig& config)
{
    std::sort(defects.begin(), defects.end(), [](const auto& a, const auto& b) {
        return a.severity > b.severity;
    });

    std::vector<SurfaceDefectItem> selected;
    selected.reserve(defects.size());
    for (const SurfaceDefectItem& defect : defects) {
        bool duplicate = false;
        for (const SurfaceDefectItem& existing : selected) {
            if (defect.polarity == existing.polarity &&
                rectsTouchOrOverlap(defect.boundingRect, existing.boundingRect, config.mergeDistance)) {
                duplicate = true;
                break;
            }
        }

        if (duplicate) {
            continue;
        }

        selected.push_back(defect);
        if (config.maxDefects > 0 && static_cast<int>(selected.size()) >= config.maxDefects) {
            break;
        }
    }

    std::sort(selected.begin(), selected.end(), [](const auto& a, const auto& b) {
        if (a.boundingRect.y != b.boundingRect.y) {
            return a.boundingRect.y < b.boundingRect.y;
        }
        return a.boundingRect.x < b.boundingRect.x;
    });

    for (int i = 0; i < static_cast<int>(selected.size()); ++i) {
        selected[static_cast<size_t>(i)].id = i + 1;
    }

    return selected;
}

cvcore::surface_defect::SurfaceDefectSummary summarizeReference(const std::vector<SurfaceDefectItem>& defects, const SurfaceDefectConfig& config)
{
    cvcore::surface_defect::SurfaceDefectSummary summary;
    summary.defectCount = static_cast<int>(defects.size());
    double totalSeverity = 0.0;
    for (const SurfaceDefectItem& defect : defects) {
        if (defect.polarity == "dark") {
            summary.darkCount++;
        }
        else if (defect.polarity == "bright") {
            summary.brightCount++;
        }

        summary.maxSeverity = (std::max)(summary.maxSeverity, defect.severity);
        totalSeverity += defect.severity;
    }

    summary.meanSeverity = defects.empty() ? 0.0 : totalSeverity / static_cast<double>(defects.size());
    summary.grade = gradeForSeverity(summary.maxSeverity, config);
    return summary;
}

SurfaceDefectResult detectSurfaceDefectsReference(const cv::Mat& image, const SurfaceDefectConfig& config)
{
    SurfaceDefectResult result;
    result.imageSize = image.empty() ? cv::Size() : cv::Size(image.cols, image.rows);

    cv::Mat source32;
    if (!convertToAnalysisFloat(image, config.channel, source32)) {
        result.statusCode = "invalid_image";
        result.message = "Invalid image or unsupported channel count/depth.";
        return result;
    }

    std::vector<SurfaceDefectItem> defects;
    const std::vector<int> scales = normalizedScales(config.scales);
    for (int scale : scales) {
        cv::Mat delta;
        buildSignedRelativeDelta(source32, scale, delta);

        if (config.enableBright && config.brightThreshold > 0.0) {
            cv::Mat brightMask;
            thresholdResidual(delta, config.brightThreshold, config.openKernel, config.closeKernel, brightMask);
            appendComponentsReference(delta, brightMask, "bright", scale, config, defects);
        }

        if (config.enableDark && config.darkThreshold > 0.0) {
            cv::Mat darkResidual;
            cv::multiply(delta, -1.0, darkResidual);
            cv::Mat darkMask;
            thresholdResidual(darkResidual, config.darkThreshold, config.openKernel, config.closeKernel, darkMask);
            appendComponentsReference(delta, darkMask, "dark", scale, config, defects);
        }
    }

    result.defects = mergeDefectsReference(std::move(defects), config);
    result.summary = summarizeReference(result.defects, config);
    result.success = true;
    result.statusCode = "ok";
    result.message = result.defects.empty() ? "No surface defects detected." : "ok";
    return result;
}

double durationToMs(const std::chrono::steady_clock::duration& duration)
{
    return std::chrono::duration_cast<std::chrono::duration<double, std::milli>>(duration).count();
}

bool compareSurfaceDefectOutputs(
    const json& actual,
    const SurfaceDefectResult& expected,
    const SurfaceDefectConfig& config,
    const cv::Mat& originalImage,
    const RoiRect& roi,
    SurfaceDefectDiffReport& report)
{
    auto fail = [&](const std::string& path, const std::string& message) {
        report.exactMatch = false;
        report.mismatchCount++;
        if (report.samples.size() < 8) {
            report.samples.push_back(path + ": " + message);
        }
    };

    auto compareDouble = [&](const std::string& path, double actualValue, double expectedValue) {
        const double diff = std::abs(actualValue - expectedValue);
        report.maxNumericDiff = (std::max)(report.maxNumericDiff, diff);
        if (diff > kSurfaceDefectComparisonTolerance) {
            fail(path, "numeric mismatch actual=" + std::to_string(actualValue) + " expected=" + std::to_string(expectedValue));
        }
    };

    if (!actual.is_object()) {
        fail("$", "output is not an object");
        return false;
    }

    const cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
    const cv::Rect imageRect(0, 0, originalImage.cols, originalImage.rows);
    const bool useRoi = (mroi.width > 0 && mroi.height > 0 && (mroi & imageRect) == mroi);
    const cv::Rect expectedRoi = useRoi ? mroi : cv::Rect(0, 0, originalImage.cols, originalImage.rows);
    const cv::Point origin = useRoi ? cv::Point(mroi.x, mroi.y) : cv::Point(0, 0);
    const json expectedConfig = surfaceDefectConfigToJson(config);

    if (actual.value("algorithm", "") != "SurfaceDefect") {
        fail("$.algorithm", "unexpected algorithm");
    }
    if (actual.value("version", "") != "0.1") {
        fail("$.version", "unexpected version");
    }
    if (!actual.value("success", false) || !expected.success) {
        fail("$.success", "unexpected success flag");
    }
    if (actual.value("statusCode", "") != expected.statusCode) {
        fail("$.statusCode", "unexpected status code");
    }
    if (actual.value("message", "") != expected.message) {
        fail("$.message", "unexpected message");
    }
    if (actual.value("count", -1) != static_cast<int>(expected.defects.size())) {
        fail("$.count", "unexpected defect count");
    }

    if (!actual.contains("image") || !actual.at("image").is_object()) {
        fail("$.image", "missing image object");
        return false;
    }

    const json& image = actual.at("image");
    if (image.value("width", -1) != originalImage.cols) {
        fail("$.image.width", "unexpected width");
    }
    if (image.value("height", -1) != originalImage.rows) {
        fail("$.image.height", "unexpected height");
    }
    if (!image.contains("roi") || !image.at("roi").is_object()) {
        fail("$.image.roi", "missing roi");
    }
    else {
        const json& actualRoi = image.at("roi");
        if (actualRoi.value("x", -1) != expectedRoi.x) {
            fail("$.image.roi.x", "unexpected roi x");
        }
        if (actualRoi.value("y", -1) != expectedRoi.y) {
            fail("$.image.roi.y", "unexpected roi y");
        }
        if (actualRoi.value("w", -1) != expectedRoi.width) {
            fail("$.image.roi.w", "unexpected roi width");
        }
        if (actualRoi.value("h", -1) != expectedRoi.height) {
            fail("$.image.roi.h", "unexpected roi height");
        }
    }

    if (actual.value("configUsed", json::object()) != expectedConfig) {
        fail("$.configUsed", "unexpected config");
    }

    if (!actual.contains("summary") || !actual.at("summary").is_object()) {
        fail("$.summary", "missing summary");
        return false;
    }

    const json& summary = actual.at("summary");
    if (summary.value("defectCount", -1) != expected.summary.defectCount) {
        fail("$.summary.defectCount", "unexpected summary count");
    }
    if (summary.value("darkCount", -1) != expected.summary.darkCount) {
        fail("$.summary.darkCount", "unexpected dark count");
    }
    if (summary.value("brightCount", -1) != expected.summary.brightCount) {
        fail("$.summary.brightCount", "unexpected bright count");
    }
    compareDouble("$.summary.maxSeverity", summary.value("maxSeverity", 0.0), expected.summary.maxSeverity);
    compareDouble("$.summary.meanSeverity", summary.value("meanSeverity", 0.0), expected.summary.meanSeverity);
    if (summary.value("grade", "") != expected.summary.grade) {
        fail("$.summary.grade", "unexpected grade");
    }

    if (!actual.contains("diagnostics") || !actual.at("diagnostics").is_object()) {
        fail("$.diagnostics", "missing diagnostics");
        return false;
    }

    const json& diagnostics = actual.at("diagnostics");
    if (diagnostics.value("roiUsed", !useRoi) != useRoi) {
        fail("$.diagnostics.roiUsed", "unexpected roi flag");
    }
    if (diagnostics.value("relativeResidual", false) != true) {
        fail("$.diagnostics.relativeResidual", "unexpected residual flag");
    }
    if (diagnostics.value("background", "") != "gaussian") {
        fail("$.diagnostics.background", "unexpected background");
    }

    if (!actual.contains("defects") || !actual.at("defects").is_array()) {
        fail("$.defects", "missing defects array");
        return false;
    }

    const json& defects = actual.at("defects");
    if (defects.size() != expected.defects.size()) {
        fail("$.defects", "unexpected defect array size");
    }

    const size_t count = (std::min)(defects.size(), expected.defects.size());
    for (size_t i = 0; i < count; ++i) {
        const json& actualDefect = defects.at(i);
        const SurfaceDefectItem& expectedDefect = expected.defects[i];
        const std::string basePath = "$.defects[" + std::to_string(i) + "]";
        const cv::Rect expectedRect(
            expectedDefect.boundingRect.x + origin.x,
            expectedDefect.boundingRect.y + origin.y,
            expectedDefect.boundingRect.width,
            expectedDefect.boundingRect.height);
        const cv::Point2d expectedCenter(expectedDefect.center.x + origin.x, expectedDefect.center.y + origin.y);

        if (actualDefect.value("id", -1) != expectedDefect.id) {
            fail(basePath + ".id", "unexpected id");
        }
        if (actualDefect.value("type", "") != expectedDefect.type) {
            fail(basePath + ".type", "unexpected type");
        }
        if (actualDefect.value("polarity", "") != expectedDefect.polarity) {
            fail(basePath + ".polarity", "unexpected polarity");
        }
        if (actualDefect.value("grade", "") != gradeForSeverity(expectedDefect.severity, config)) {
            fail(basePath + ".grade", "unexpected grade");
        }
        if (actualDefect.value("scale", -1) != expectedDefect.scale) {
            fail(basePath + ".scale", "unexpected scale");
        }
        if (actualDefect.value("x", (std::numeric_limits<int>::lowest)()) != expectedRect.x) {
            fail(basePath + ".x", "unexpected x");
        }
        if (actualDefect.value("y", (std::numeric_limits<int>::lowest)()) != expectedRect.y) {
            fail(basePath + ".y", "unexpected y");
        }
        if (actualDefect.value("w", (std::numeric_limits<int>::lowest)()) != expectedRect.width) {
            fail(basePath + ".w", "unexpected width");
        }
        if (actualDefect.value("h", (std::numeric_limits<int>::lowest)()) != expectedRect.height) {
            fail(basePath + ".h", "unexpected height");
        }
        compareDouble(basePath + ".centerX", actualDefect.value("centerX", 0.0), expectedCenter.x);
        compareDouble(basePath + ".centerY", actualDefect.value("centerY", 0.0), expectedCenter.y);
        if (actualDefect.value("area", -1) != expectedDefect.area) {
            fail(basePath + ".area", "unexpected area");
        }
        compareDouble(basePath + ".meanDelta", actualDefect.value("meanDelta", 0.0), expectedDefect.meanDelta);
        compareDouble(basePath + ".minDelta", actualDefect.value("minDelta", 0.0), expectedDefect.minDelta);
        compareDouble(basePath + ".maxDelta", actualDefect.value("maxDelta", 0.0), expectedDefect.maxDelta);
        compareDouble(basePath + ".maxDeltaAbs", actualDefect.value("maxDeltaAbs", 0.0), expectedDefect.maxDeltaAbs);
        compareDouble(basePath + ".severity", actualDefect.value("severity", 0.0), expectedDefect.severity);
        compareDouble(basePath + ".aspectRatio", actualDefect.value("aspectRatio", 0.0), expectedDefect.aspectRatio);
        compareDouble(basePath + ".fillRatio", actualDefect.value("fillRatio", 0.0), expectedDefect.fillRatio);

        if (!actualDefect.contains("boundingRect") || !actualDefect.at("boundingRect").is_object()) {
            fail(basePath + ".boundingRect", "missing boundingRect");
        }
        else {
            const json& boundingRect = actualDefect.at("boundingRect");
            if (boundingRect.value("x", (std::numeric_limits<int>::lowest)()) != expectedRect.x
                || boundingRect.value("y", (std::numeric_limits<int>::lowest)()) != expectedRect.y
                || boundingRect.value("w", (std::numeric_limits<int>::lowest)()) != expectedRect.width
                || boundingRect.value("h", (std::numeric_limits<int>::lowest)()) != expectedRect.height) {
                fail(basePath + ".boundingRect", "unexpected bounding rect");
            }
        }
    }

    return report.exactMatch;
}

cv::Mat makeSurfaceDefectFlatImage8U(int width, int height)
{
    return cv::Mat(height, width, CV_8UC1, cv::Scalar(128)).clone();
}

cv::Mat makeSurfaceDefectBgrImage8U()
{
    cv::Mat image(240, 320, CV_8UC3, cv::Scalar(112, 118, 126));
    cv::rectangle(image, cv::Rect(52, 46, 30, 20), cv::Scalar(210, 210, 210), cv::FILLED);
    cv::rectangle(image, cv::Rect(184, 138, 28, 22), cv::Scalar(36, 36, 36), cv::FILLED);
    cv::rectangle(image, cv::Rect(68, 170, 116, 8), cv::Scalar(202, 202, 202), cv::FILLED);
    cv::rectangle(image, cv::Rect(236, 58, 14, 72), cv::Scalar(44, 44, 44), cv::FILLED);
    return image;
}

cv::Mat makeSurfaceDefectRoi16UImage()
{
    cv::Mat image(320, 420, CV_16UC1, cv::Scalar(32768));
    cv::rectangle(image, cv::Rect(90, 58, 18, 96), cv::Scalar(56320), cv::FILLED);
    cv::rectangle(image, cv::Rect(228, 174, 30, 20), cv::Scalar(12000), cv::FILLED);
    cv::rectangle(image, cv::Rect(256, 48, 56, 10), cv::Scalar(54800), cv::FILLED);
    cv::rectangle(image, cv::Rect(116, 206, 12, 54), cv::Scalar(14500), cv::FILLED);
    return image;
}

cv::Mat makeSurfaceDefectNonContiguousView()
{
    cv::Mat backing(280, 280, CV_8UC1, cv::Scalar(128));
    for (int y = 0; y < 8; ++y) {
        for (int x = 0; x < 8; ++x) {
            const int left = 18 + x * 30;
            const int top = 16 + y * 30;
            const cv::Rect rect(left, top, 12, 12);
            if (((x + y) % 2) == 0) {
                cv::rectangle(backing, rect, cv::Scalar(214 - (x + y)), cv::FILLED);
            }
            else {
                cv::rectangle(backing, rect, cv::Scalar(42 + (x + y)), cv::FILLED);
            }
        }
    }

    return backing(cv::Rect(10, 12, 252, 240));
}

cv::Mat makeSurfaceDefectSelectionImage16U()
{
    cv::Mat image(360, 360, CV_16UC1, cv::Scalar(32000));
    for (int y = 0; y < 6; ++y) {
        for (int x = 0; x < 6; ++x) {
            const int left = 30 + x * 52;
            const int top = 28 + y * 52;
            const cv::Rect rect(left, top, 14, 14);
            const unsigned short value = ((x + y) % 2 == 0)
                ? static_cast<unsigned short>(51000 - (x + y) * 120)
                : static_cast<unsigned short>(11800 + (x + y) * 120);
            cv::rectangle(image, rect, cv::Scalar(value), cv::FILLED);
        }
    }

    return image;
}

SurfaceDefectTestCase makeSurfaceDefectFlatCase()
{
    SurfaceDefectTestCase testCase;
    testCase.name = "flat-no-defect";
    testCase.image = makeSurfaceDefectFlatImage8U(240, 180);
    testCase.config.scales = { 31 };
    testCase.config.brightThreshold = 0.5;
    testCase.config.darkThreshold = 0.5;
    testCase.config.minArea = 20;
    testCase.config.maxArea = 0;
    testCase.config.minSeverity = 0.05;
    return testCase;
}

SurfaceDefectTestCase makeSurfaceDefectBgrCase()
{
    SurfaceDefectTestCase testCase;
    testCase.name = "bgr-line-and-blobs";
    testCase.image = makeSurfaceDefectBgrImage8U();
    testCase.config.channel = -1;
    testCase.config.scales = { 31, 61, 121 };
    testCase.config.brightThreshold = 0.03;
    testCase.config.darkThreshold = 0.03;
    testCase.config.minArea = 18;
    testCase.config.maxArea = 0;
    testCase.config.muraMinArea = 1200;
    testCase.config.openKernel = 1;
    testCase.config.closeKernel = 3;
    testCase.config.mergeDistance = 5;
    testCase.config.minSeverity = 0.0;
    testCase.config.lineAspectRatio = 6.0;
    return testCase;
}

SurfaceDefectTestCase makeSurfaceDefectRoi16BitCase()
{
    SurfaceDefectTestCase testCase;
    testCase.name = "roi-16bit";
    testCase.image = makeSurfaceDefectRoi16UImage();
    testCase.roi = { 38, 42, 260, 192 };
    testCase.config.channel = -1;
    testCase.config.scales = { 31, 61 };
    testCase.config.brightThreshold = 0.025;
    testCase.config.darkThreshold = 0.025;
    testCase.config.minArea = 16;
    testCase.config.maxArea = 0;
    testCase.config.muraMinArea = 1200;
    testCase.config.openKernel = 1;
    testCase.config.closeKernel = 5;
    testCase.config.mergeDistance = 4;
    testCase.config.minSeverity = 0.0;
    testCase.config.lineAspectRatio = 5.0;
    return testCase;
}

SurfaceDefectTestCase makeSurfaceDefectNonContiguousSelectionCase()
{
    SurfaceDefectTestCase testCase;
    testCase.name = "non-contiguous-selection";
    testCase.image = makeSurfaceDefectNonContiguousView();
    testCase.config.scales = { 31 };
    testCase.config.brightThreshold = 0.04;
    testCase.config.darkThreshold = 0.04;
    testCase.config.minArea = 14;
    testCase.config.maxArea = 0;
    testCase.config.muraMinArea = 2000;
    testCase.config.openKernel = 1;
    testCase.config.closeKernel = 3;
    testCase.config.mergeDistance = 2;
    testCase.config.maxDefects = 8;
    testCase.config.minSeverity = 0.0;
    return testCase;
}

SurfaceDefectBenchmarkRow runSurfaceDefectBenchmarkRow(
    const std::string& name,
    const cv::Mat& image,
    const RoiRect& roi,
    const SurfaceDefectConfig& config,
    int requestedComponents,
    int warmIterations)
{
    SurfaceDefectBenchmarkRow row;
    row.name = name;
    row.requestedComponents = requestedComponents;

    const auto prepareStart = std::chrono::steady_clock::now();
    const std::string configJson = surfaceDefectConfigToJson(config).dump();
    HImage hImage = createHImageFromMat(image);
    const auto prepareEnd = std::chrono::steady_clock::now();
    row.prepareMs = durationToMs(prepareEnd - prepareStart);

    char* result = nullptr;
    const auto coldStart = std::chrono::steady_clock::now();
    const int coldRet = M_DetectSurfaceDefects(hImage, roi, configJson.c_str(), &result);
    const auto coldEnd = std::chrono::steady_clock::now();
    row.coldMs = durationToMs(coldEnd - coldStart);

    if (coldRet <= 0 || result == nullptr) {
        std::ostringstream message;
        message << "SurfaceDefect benchmark cold run failed for " << name << " ret=" << coldRet;
        throw std::runtime_error(message.str());
    }

    json coldJson = json::parse(result, nullptr, false);
    FreeResult(result);
    if (coldJson.is_discarded()) {
        throw std::runtime_error("SurfaceDefect benchmark cold JSON parse failed for " + name);
    }

    row.detectedComponents = coldJson.value("count", 0);

    double warmTotal = 0.0;
    for (int i = 0; i < warmIterations; ++i) {
        char* warmResult = nullptr;
        const auto warmStart = std::chrono::steady_clock::now();
        const int warmRet = M_DetectSurfaceDefects(hImage, roi, configJson.c_str(), &warmResult);
        const auto warmEnd = std::chrono::steady_clock::now();
        warmTotal += durationToMs(warmEnd - warmStart);

        if (warmRet <= 0 || warmResult == nullptr) {
            throw std::runtime_error("SurfaceDefect benchmark warm run failed for " + name);
        }
        FreeResult(warmResult);
    }

    row.warmMs = warmTotal / static_cast<double>(warmIterations);
    return row;
}

bool runSurfaceDefectEquivalenceCase(const SurfaceDefectTestCase& testCase, SurfaceDefectDiffReport& aggregate)
{
    HImage hImage = createHImageFromMat(testCase.image);
    const std::string configJson = surfaceDefectConfigToJson(testCase.config).dump();

    char* result = nullptr;
    const int ret = M_DetectSurfaceDefects(hImage, testCase.roi, configJson.c_str(), &result);
    if (ret <= 0 || result == nullptr) {
        std::cerr << "SurfaceDefect case " << testCase.name << " returned " << ret << std::endl;
        return false;
    }

    json actual = json::parse(result, nullptr, false);
    FreeResult(result);
    if (actual.is_discarded()) {
        std::cerr << "SurfaceDefect case " << testCase.name << " produced invalid JSON" << std::endl;
        return false;
    }

    const cv::Rect referenceRoi(testCase.roi.x, testCase.roi.y, testCase.roi.width, testCase.roi.height);
    const cv::Rect imageRect(0, 0, testCase.image.cols, testCase.image.rows);
    const bool useRoi = (referenceRoi.width > 0 && referenceRoi.height > 0 && (referenceRoi & imageRect) == referenceRoi);
    const cv::Mat referenceImage = useRoi ? testCase.image(referenceRoi) : testCase.image;
    SurfaceDefectResult reference = detectSurfaceDefectsReference(referenceImage, testCase.config);
    SurfaceDefectDiffReport report;
    compareSurfaceDefectOutputs(actual, reference, testCase.config, testCase.image, testCase.roi, report);
    aggregate.exactMatch = aggregate.exactMatch && report.exactMatch;
    aggregate.maxNumericDiff = std::max(aggregate.maxNumericDiff, report.maxNumericDiff);
    aggregate.mismatchCount += report.mismatchCount;
    aggregate.samples.insert(aggregate.samples.end(), report.samples.begin(), report.samples.end());

    if (!report.exactMatch) {
        std::cerr << "SurfaceDefect case " << testCase.name << " mismatch count=" << report.mismatchCount
                  << " maxNumericDiff=" << report.maxNumericDiff << std::endl;
        for (const std::string& sample : report.samples) {
            std::cerr << "  " << sample << std::endl;
        }
        return false;
    }

    return true;
}

std::vector<SurfaceDefectTestCase> buildSurfaceDefectEquivalenceCases()
{
    std::vector<SurfaceDefectTestCase> cases;
    cases.push_back(makeSurfaceDefectFlatCase());
    cases.push_back(makeSurfaceDefectBgrCase());
    cases.push_back(makeSurfaceDefectRoi16BitCase());
    cases.push_back(makeSurfaceDefectNonContiguousSelectionCase());
    return cases;
}

cv::Mat makeSurfaceDefectBenchmarkImage(int gridSide, int cellSize, int spotSize, int activeSide)
{
    const int imageSize = gridSide * cellSize;
    cv::Mat image(imageSize, imageSize, CV_8UC1, cv::Scalar(128));
    const int startCell = (gridSide - activeSide) / 2;
    const int gap = (cellSize - spotSize) / 2;

    for (int gy = 0; gy < activeSide; ++gy) {
        for (int gx = 0; gx < activeSide; ++gx) {
            const int cellX = startCell + gx;
            const int cellY = startCell + gy;
            const int left = cellX * cellSize + gap;
            const int top = cellY * cellSize + gap;
            const cv::Rect rect(left, top, spotSize, spotSize);
            const bool bright = ((cellX + cellY) % 2) == 0;
            cv::rectangle(image, rect, cv::Scalar(bright ? 224 : 32), cv::FILLED);
        }
    }

    return image;
}

std::vector<SurfaceDefectBenchmarkRow> runSurfaceDefectBenchmarks()
{
    std::vector<SurfaceDefectBenchmarkRow> rows;
    const int gridSide = 32;
    const int cellSize = 48;
    const int spotSize = 18;
    const std::array<int, 6> activeSides = { 1, 2, 4, 8, 16, 32 };
    SurfaceDefectConfig config;
    config.scales = { 31 };
    config.brightThreshold = 0.03;
    config.darkThreshold = 0.03;
    config.minArea = 12;
    config.maxArea = 0;
    config.muraMinArea = 2000;
    config.openKernel = 1;
    config.closeKernel = 3;
    config.mergeDistance = 2;
    config.maxDefects = 0;
    config.minSeverity = 0.0;
    config.lineAspectRatio = 8.0;

    const int warmIterations = 5;
    for (int activeSide : activeSides) {
        const auto prepareStart = std::chrono::steady_clock::now();
        cv::Mat image = makeSurfaceDefectBenchmarkImage(gridSide, cellSize, spotSize, activeSide);
        const auto prepareEnd = std::chrono::steady_clock::now();
        SurfaceDefectBenchmarkRow row = runSurfaceDefectBenchmarkRow(
            "grid-" + std::to_string(activeSide * activeSide),
            image,
            { 0, 0, 0, 0 },
            config,
            activeSide * activeSide,
            warmIterations);
        row.prepareMs += durationToMs(prepareEnd - prepareStart);
        rows.push_back(std::move(row));
    }

    return rows;
}

bool runSurfaceDefectEquivalenceTests()
{
    const std::vector<SurfaceDefectTestCase> cases = buildSurfaceDefectEquivalenceCases();
    SurfaceDefectDiffReport aggregate;
    for (const SurfaceDefectTestCase& testCase : cases) {
        if (!runSurfaceDefectEquivalenceCase(testCase, aggregate)) {
            return false;
        }
    }

    std::cout << "SurfaceDefect equivalence cases passed: " << cases.size()
              << ", maxNumericDiff=" << std::setprecision(15) << aggregate.maxNumericDiff
              << ", mismatchCount=" << aggregate.mismatchCount << std::endl;
    return aggregate.mismatchCount == 0 && aggregate.maxNumericDiff <= kSurfaceDefectComparisonTolerance;
}

bool runSurfaceDefectBenchmarkMode()
{
    std::vector<SurfaceDefectBenchmarkRow> rows;
    try {
        rows = runSurfaceDefectBenchmarks();
    }
    catch (const std::exception& ex) {
        std::cerr << ex.what() << std::endl;
        return false;
    }

    std::cout << "SurfaceDefect benchmark (Release|x64)" << std::endl;
    std::cout << std::left
              << std::setw(14) << "case"
              << std::setw(14) << "components"
              << std::setw(14) << "detected"
              << std::setw(14) << "prepare_ms"
              << std::setw(14) << "cold_ms"
              << std::setw(14) << "warm_ms"
              << std::endl;

    for (const SurfaceDefectBenchmarkRow& row : rows) {
        std::cout << std::left
                  << std::setw(14) << row.name
                  << std::setw(14) << row.requestedComponents
                  << std::setw(14) << row.detectedComponents
                  << std::setw(14) << std::fixed << std::setprecision(3) << row.prepareMs
                  << std::setw(14) << row.coldMs
                  << std::setw(14) << row.warmMs
                  << std::endl;
    }

    return true;
}

} // namespace

bool smokeConvertImageHandlesStrideAndOwnedBuffer()
{
    const int width = 7;
    const int height = 5;
    const int channels = 3;
    const int stride = 32;
    std::vector<unsigned char> padded(static_cast<size_t>(height) * stride, 0);

    for (int y = 0; y < height; ++y) {
        unsigned char* row = padded.data() + static_cast<size_t>(y) * stride;
        for (int x = 0; x < width; ++x) {
            row[x * channels + 0] = static_cast<unsigned char>(10 + x);
            row[x * channels + 1] = static_cast<unsigned char>(20 + y);
            row[x * channels + 2] = static_cast<unsigned char>(80 + x + y);
        }
    }

    HImage himg{};
    himg.rows = height;
    himg.cols = width;
    himg.channels = channels;
    himg.depth = 8;
    himg.stride = stride;
    himg.isDispose = false;
    himg.pData = padded.data();

    unsigned char* output = nullptr;
    int length = 0;
    int scaleFactor = 0;
    const int ret = M_ConvertImage(himg, &output, &length, &scaleFactor, width, height);

    bool ok = false;
    if (ret == 0 && output != nullptr && length == width * height && scaleFactor == 1) {
        cv::Mat source(height, width, CV_8UC3, padded.data(), stride);
        cv::Mat expected;
        cv::cvtColor(source, expected, cv::COLOR_BGR2GRAY);

        ok = true;
        for (int y = 0; y < height && ok; ++y) {
            for (int x = 0; x < width; ++x) {
                if (output[y * width + x] != expected.at<unsigned char>(y, x)) {
                    ok = false;
                    break;
                }
            }
        }
    }

    M_FreeHImageData(output);
    return ok;
}

bool smokeConvertImageClearsOutputsOnFailure()
{
    unsigned char* output = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    int length = 123;
    int scaleFactor = 456;

    const int ret = M_ConvertImage(HImage{}, &output, &length, &scaleFactor, 0, 512);

    return ret < 0
        && output == nullptr
        && length == 0
        && scaleFactor == 0;
}

bool smokeDrawPoiImageInvalidArgsClearOutput()
{
    cv::Mat image(8, 8, CV_8UC3, cv::Scalar(20, 40, 60));
    HImage himg = createHImageFromMat(image);

    HImage out{};
    out.rows = 123;
    out.cols = 456;
    out.stride = 789;
    out.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));

    const int nullPointsRet = M_DrawPoiImage(himg, &out, 2, nullptr, 2, 1);
    const bool nullPointsCleared = nullPointsRet < 0
        && out.rows == 0
        && out.cols == 0
        && out.stride == 0
        && out.pData == nullptr;

    int points[] = { 1, 1, 3 };
    out.rows = 123;
    out.cols = 456;
    out.stride = 789;
    out.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    const int oddCountRet = M_DrawPoiImage(himg, &out, 2, points, 3, 1);
    const bool oddCountCleared = oddCountRet < 0
        && out.rows == 0
        && out.cols == 0
        && out.stride == 0
        && out.pData == nullptr;

    out.rows = 123;
    out.cols = 456;
    out.stride = 789;
    out.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    const int badRadiusRet = M_DrawPoiImage(himg, &out, 0, points, 2, 1);
    const bool badRadiusCleared = badRadiusRet < 0
        && out.rows == 0
        && out.cols == 0
        && out.stride == 0
        && out.pData == nullptr;

    return nullPointsCleared && oddCountCleared && badRadiusCleared;
}

bool smokeChannelExportsInvalidArgsClearOutput()
{
    cv::Mat gray(8, 8, CV_8UC1, cv::Scalar(80));
    HImage grayImage = createHImageFromMat(gray);

    HImage whiteBalanceOut{};
    whiteBalanceOut.rows = 123;
    whiteBalanceOut.cols = 456;
    whiteBalanceOut.stride = 789;
    whiteBalanceOut.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    const int whiteRet = M_GetWhiteBalance(grayImage, &whiteBalanceOut, 1.0, 1.0, 1.0);
    const bool whiteBalanceCleared = whiteRet < 0
        && whiteBalanceOut.rows == 0
        && whiteBalanceOut.cols == 0
        && whiteBalanceOut.stride == 0
        && whiteBalanceOut.pData == nullptr;

    cv::Mat color(8, 8, CV_8UC3, cv::Scalar(10, 20, 30));
    HImage colorImage = createHImageFromMat(color);
    HImage extractOut{};
    extractOut.rows = 123;
    extractOut.cols = 456;
    extractOut.stride = 789;
    extractOut.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    const int extractRet = M_ExtractChannel(colorImage, &extractOut, 3);
    const bool extractCleared = extractRet < 0
        && extractOut.rows == 0
        && extractOut.cols == 0
        && extractOut.stride == 0
        && extractOut.pData == nullptr;

    HImage finiteOut{};
    finiteOut.rows = 123;
    finiteOut.cols = 456;
    finiteOut.stride = 789;
    finiteOut.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    const int finiteRet = M_GetWhiteBalance(colorImage, &finiteOut, std::numeric_limits<double>::quiet_NaN(), 1.0, 1.0);
    const bool finiteCleared = finiteRet < 0
        && finiteOut.rows == 0
        && finiteOut.cols == 0
        && finiteOut.stride == 0
        && finiteOut.pData == nullptr;

    return whiteBalanceCleared && extractCleared && finiteCleared;
}

bool smokeGammaInvalidArgsClearOutput()
{
    cv::Mat color(8, 8, CV_8UC3, cv::Scalar(10, 20, 30));
    HImage colorImage = createHImageFromMat(color);

    HImage zeroGammaOut{};
    zeroGammaOut.rows = 123;
    zeroGammaOut.cols = 456;
    zeroGammaOut.stride = 789;
    zeroGammaOut.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    const int zeroRet = M_ApplyGammaCorrection(colorImage, &zeroGammaOut, 0.0);
    const bool zeroCleared = zeroRet < 0
        && zeroGammaOut.rows == 0
        && zeroGammaOut.cols == 0
        && zeroGammaOut.stride == 0
        && zeroGammaOut.pData == nullptr;

    HImage nonFiniteGammaOut{};
    nonFiniteGammaOut.rows = 123;
    nonFiniteGammaOut.cols = 456;
    nonFiniteGammaOut.stride = 789;
    nonFiniteGammaOut.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    const int nonFiniteRet = M_ApplyGammaCorrection(
        colorImage,
        &nonFiniteGammaOut,
        std::numeric_limits<double>::infinity());
    const bool nonFiniteCleared = nonFiniteRet < 0
        && nonFiniteGammaOut.rows == 0
        && nonFiniteGammaOut.cols == 0
        && nonFiniteGammaOut.stride == 0
        && nonFiniteGammaOut.pData == nullptr;

    return zeroCleared && nonFiniteCleared;
}

bool IsOwnedBgr8Image(const HImage& image, int rows, int cols)
{
    return image.pData != nullptr
        && image.rows == rows
        && image.cols == cols
        && image.channels == 3
        && image.depth == 8
        && image.stride >= cols * 3;
}

bool smokeAutoAdjustExportsNormalizeInputFormats()
{
    cv::Mat floatColor(6, 7, CV_32FC3);
    cv::randu(floatColor, cv::Scalar::all(0.0), cv::Scalar::all(1.0));
    HImage floatImage = createHImageFromMat(floatColor);

    HImage autoLevelsOut{};
    const int autoLevelsRet = M_AutoLevelsAdjust(floatImage, &autoLevelsOut);
    const bool autoLevelsOk = autoLevelsRet == 0
        && IsOwnedBgr8Image(autoLevelsOut, floatColor.rows, floatColor.cols);
    M_FreeHImageData(autoLevelsOut.pData);

    cv::Mat constantBgr(5, 5, CV_8UC3, cv::Scalar(80, 80, 80));
    HImage constantImage = createHImageFromMat(constantBgr);

    HImage constantAutoLevelsOut{};
    const int constantAutoLevelsRet = M_AutoLevelsAdjust(constantImage, &constantAutoLevelsOut);
    bool constantAutoLevelsOk = constantAutoLevelsRet == 0
        && IsOwnedBgr8Image(constantAutoLevelsOut, constantBgr.rows, constantBgr.cols);
    if (constantAutoLevelsOk) {
        cv::Mat outView(
            constantAutoLevelsOut.rows,
            constantAutoLevelsOut.cols,
            CV_8UC3,
            constantAutoLevelsOut.pData,
            static_cast<size_t>(constantAutoLevelsOut.stride));
        cv::Mat diff;
        cv::absdiff(outView, constantBgr, diff);
        constantAutoLevelsOk = cv::countNonZero(diff.reshape(1)) == 0;
    }
    M_FreeHImageData(constantAutoLevelsOut.pData);

    HImage constantToneOut{};
    const int constantToneRet = M_AutomaticToneAdjustment(constantImage, &constantToneOut);
    bool constantToneOk = constantToneRet == 0
        && IsOwnedBgr8Image(constantToneOut, constantBgr.rows, constantBgr.cols);
    if (constantToneOk) {
        cv::Mat outView(
            constantToneOut.rows,
            constantToneOut.cols,
            CV_8UC3,
            constantToneOut.pData,
            static_cast<size_t>(constantToneOut.stride));
        cv::Mat diff;
        cv::absdiff(outView, constantBgr, diff);
        constantToneOk = cv::countNonZero(diff.reshape(1)) == 0;
    }
    M_FreeHImageData(constantToneOut.pData);

    cv::Mat bgra(5, 6, CV_8UC4, cv::Scalar(10, 40, 90, 255));
    HImage bgraImage = createHImageFromMat(bgra);

    HImage colorOut{};
    const int colorRet = M_AutomaticColorAdjustment(bgraImage, &colorOut);
    const bool colorOk = colorRet == 0
        && IsOwnedBgr8Image(colorOut, bgra.rows, bgra.cols);
    M_FreeHImageData(colorOut.pData);

    HImage toneOut{};
    const int toneRet = M_AutomaticToneAdjustment(bgraImage, &toneOut);
    const bool toneOk = toneRet == 0
        && IsOwnedBgr8Image(toneOut, bgra.rows, bgra.cols);
    M_FreeHImageData(toneOut.pData);

    cv::Mat gray(4, 4, CV_8UC1, cv::Scalar(120));
    HImage grayImage = createHImageFromMat(gray);
    HImage grayOut{};
    grayOut.rows = 123;
    grayOut.cols = 456;
    grayOut.stride = 789;
    grayOut.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    const int grayRet = M_AutoLevelsAdjust(grayImage, &grayOut);
    const bool grayCleared = grayRet < 0
        && grayOut.rows == 0
        && grayOut.cols == 0
        && grayOut.stride == 0
        && grayOut.pData == nullptr;

    return autoLevelsOk
        && constantAutoLevelsOk
        && constantToneOk
        && colorOk
        && toneOk
        && grayCleared;
}

bool smokeFusionReturnsOwnedHImage()
{
    namespace fs = std::filesystem;

    fs::path imageDir = fs::temp_directory_path() / "colorvision_native_fusion_smoke";
    fs::create_directories(imageDir);
    fs::path imagePath = imageDir / "fusion_input.png";

    cv::Mat image(16, 24, CV_8UC3, cv::Scalar(12, 80, 160));
    if (!cv::imwrite(imagePath.string(), image)) {
        fs::remove_all(imageDir);
        return false;
    }

    json files = json::array({ imagePath.string() });
    HImage outImage{};
    const int ret = M_Fusion(files.dump().c_str(), &outImage);
    const bool ok = ret == 0
        && outImage.pData != nullptr
        && outImage.rows == image.rows
        && outImage.cols == image.cols
        && outImage.channels == image.channels()
        && outImage.depth == 8
        && outImage.stride >= image.cols * image.channels();

    M_FreeHImageData(outImage.pData);
    fs::remove_all(imageDir);
    return ok;
}

bool smokeStitchImagesReturnsOwnedHImage()
{
    namespace fs = std::filesystem;

    fs::path imageDir = fs::temp_directory_path() / "colorvision_native_stitch_smoke";
    fs::create_directories(imageDir);
    fs::path imagePath1 = imageDir / "stitch_input_1.png";
    fs::path imagePath2 = imageDir / "stitch_input_2.png";

    cv::Mat image1(8, 8, CV_8UC3, cv::Scalar(10, 20, 30));
    cv::Mat image2(8, 8, CV_8UC3, cv::Scalar(90, 100, 110));
    if (!cv::imwrite(imagePath1.string(), image1) || !cv::imwrite(imagePath2.string(), image2)) {
        fs::remove_all(imageDir);
        return false;
    }

    json config;
    config["ImageFiles"] = json::array({ imagePath1.string(), imagePath2.string() });

    HImage outImage{};
    const int ret = M_StitchImages(config.dump().c_str(), &outImage);
    const bool ok = ret == 0
        && outImage.pData != nullptr
        && outImage.rows == image2.rows
        && outImage.cols == image2.cols
        && outImage.channels == image2.channels()
        && outImage.depth == 8
        && outImage.stride >= image2.cols * image2.channels();

    M_FreeHImageData(outImage.pData);
    fs::remove_all(imageDir);
    return ok;
}

bool smokeHImageExportsClearOutputOnFailure()
{
    HImage fusionOut{};
    fusionOut.rows = 123;
    fusionOut.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    const int fusionRet = M_Fusion("{", &fusionOut);

    HImage stitchOut{};
    stitchOut.rows = 456;
    stitchOut.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    const int stitchRet = M_StitchImages("{", &stitchOut);

    return fusionRet < 0
        && fusionOut.pData == nullptr
        && fusionOut.rows == 0
        && stitchRet < 0
        && stitchOut.pData == nullptr
        && stitchOut.rows == 0;
}

bool smokeVideoInvalidCalls()
{
    std::cout << "Video API invalid-call smoke..." << std::endl;

    VideoInfo info{};
    info.totalFrames = 123;
    info.width = 456;
    const bool openNullClearsInfo = M_VideoOpen(nullptr, &info) == -1
        && info.totalFrames == 0
        && info.width == 0;

    HImage out{};
    out.rows = 123;
    out.cols = 456;
    out.channels = 3;
    out.depth = 8;
    out.stride = 789;
    out.isDispose = true;
    out.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));

    const bool readInvalidClearsOutput = M_VideoReadFrame(-999, &out) == -1
        && out.pData == nullptr
        && out.rows == 0
        && out.cols == 0
        && out.stride == 0;

    return openNullClearsInfo
        && readInvalidClearsOutput
        && M_VideoReadFrame(-999, nullptr) == -1
        && M_VideoSeek(-999, 0) == -1
        && M_VideoGetCurrentFrame(-999) == -1
        && M_VideoSetPlaybackSpeed(-999, 1.0) == -1
        && M_VideoSetResizeScale(-999, 1.0) == -1
        && M_VideoPlay(-999, smokeVideoFrameCallback, smokeVideoStatusCallback, nullptr) == -1
        && M_VideoPause(-999) == -1
        && M_VideoClose(-999) == -1;
}

bool smokeVideoApiLifecycle()
{
    namespace fs = std::filesystem;
    std::cout << "Video API lifecycle smoke..." << std::endl;

    fs::path videoDir = fs::temp_directory_path() / "colorvision_native_video_smoke";
    fs::create_directories(videoDir);

    for (int i = 0; i < 10; ++i) {
        cv::Mat frame(48, 64, CV_8UC3, cv::Scalar(20 + i * 10, 80, 180));
        cv::putText(frame, std::to_string(i), cv::Point(8, 32), cv::FONT_HERSHEY_SIMPLEX, 0.7, cv::Scalar(255, 255, 255), 1);
        char name[32];
        std::snprintf(name, sizeof(name), "frame_%02d.png", i);
        if (!cv::imwrite((videoDir / name).string(), frame)) {
            fs::remove_all(videoDir);
            return false;
        }
    }

    fs::path videoPath = videoDir / "frame_%02d.png";
    VideoInfo info{};
    std::cout << "  open" << std::endl;
    int handle = M_VideoOpen(videoPath.wstring().c_str(), &info);
    if (handle <= 0) {
        fs::remove_all(videoDir);
        return false;
    }

    HImage frame{};
    std::cout << "  read" << std::endl;
    int readRet = M_VideoReadFrame(handle, &frame);
    bool readOk = readRet == 0 && frame.pData != nullptr && frame.rows > 0 && frame.cols > 0 && frame.stride > 0;
    if (frame.pData != nullptr) {
        CoTaskMemFree(frame.pData);
        frame.pData = nullptr;
    }

    g_videoCallbackFrames.store(0);
    g_videoStatusPlaying.store(0);
    std::cout << "  seek" << std::endl;
    M_VideoSeek(handle, 0);
    std::cout << "  play" << std::endl;
    int playRet = M_VideoPlay(handle, smokeVideoFrameCallback, smokeVideoStatusCallback, nullptr);

    for (int i = 0; i < 20 && g_videoCallbackFrames.load() == 0; ++i) {
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
    }

    std::cout << "  pause" << std::endl;
    int pauseRet = M_VideoPause(handle);
    std::cout << "  close" << std::endl;
    int closeRet = M_VideoClose(handle);
    std::cout << "  closed" << std::endl;
    fs::remove_all(videoDir);

    return readOk
        && playRet == 0
        && pauseRet == 0
        && closeRet == 0
        && g_videoStatusPlaying.load() > 0
        && g_videoCallbackFrames.load() > 0;
}

using CudaFusionBatchFn = int(__cdecl*)(const char*, HImage*, int, int*);

bool smokeCudaFusionBatchClearsOutputsOnFailure()
{
    std::cout << "CUDA fusion batch failure-clear smoke..." << std::endl;

    HMODULE module = LoadLibraryW(L"opencv_cuda.dll");
    if (module == nullptr) {
        std::cout << "  skipped: opencv_cuda.dll is not available" << std::endl;
        return true;
    }

    auto fn = reinterpret_cast<CudaFusionBatchFn>(GetProcAddress(module, "CM_Fusion_Batch"));
    if (fn == nullptr) {
        FreeLibrary(module);
        return false;
    }

    HImage outImages[2]{};
    outImages[0].rows = 123;
    outImages[0].cols = 456;
    outImages[0].stride = 789;
    outImages[0].pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
    outImages[1].rows = 321;
    outImages[1].cols = 654;
    outImages[1].stride = 987;
    outImages[1].pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(2));
    int outCount = 42;

    const int ret = fn(nullptr, outImages, 2, &outCount);
    FreeLibrary(module);

    return ret == -1
        && outCount == 0
        && outImages[0].pData == nullptr
        && outImages[0].rows == 0
        && outImages[0].cols == 0
        && outImages[0].stride == 0
        && outImages[1].pData == nullptr
        && outImages[1].rows == 0
        && outImages[1].cols == 0
        && outImages[1].stride == 0;
}

// Test 1: Use fixed threshold
void testFixedThreshold(cv::Mat testImg)
{
    std::cout << "\n=== Test 1: Fixed Threshold (Threshold=100) ===" << std::endl;

    // Create test image
//    cv::Mat testImg = createTestImage(640, 480, 200, 150);
    HImage himg = createHImageFromMat(testImg);

    // Set ROI to empty (use entire image)
    RoiRect roi = {0, 0, 0, 0};

    // Create config JSON (fixed threshold=100)
    json config;
    config["Threshold"] = 100;
    config["UseRotatedRect"] = false;
    std::string configStr = config.dump();

    // Call function
    char* result = nullptr;
    int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);

    if (ret > 0 && result != nullptr) {
        std::cout << "Success! Return value: " << ret << std::endl;
        std::cout << "Result JSON: " << result << std::endl;

        // Parse result
        json resultJson = json::parse(result);
        if (resultJson.contains("X") && resultJson.contains("Y") && 
            resultJson.contains("Width") && resultJson.contains("Height")) {
            std::cout << "Luminous area: X=" << resultJson["X"] 
                      << ", Y=" << resultJson["Y"]
                      << ", Width=" << resultJson["Width"]
                      << ", Height=" << resultJson["Height"] << std::endl;
        }

        FreeResult(result);
    } else {
        std::cout << "Failed! Error code: " << ret << std::endl;
    }
}

// Test 2: Use auto threshold (OTSU method, Threshold=-1)
void testAutoThresholdExplicit(cv::Mat testImg)
{
    std::cout << "\n=== Test 2: Auto Threshold (Threshold=-1, OTSU) ===" << std::endl;

    // Create test image
//    cv::Mat testImg = createTestImage(640, 480, 200, 150);
    HImage himg = createHImageFromMat(testImg);

    // Set ROI to empty (use entire image)
    RoiRect roi = {0, 0, 0, 0};

    // Create config JSON (set Threshold=-1 to enable auto threshold)
    json config;
    config["Threshold"] = -1;
    config["UseRotatedRect"] = false;
    std::string configStr = config.dump();

    // Call function
    char* result = nullptr;
    int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);

    if (ret > 0 && result != nullptr) {
        std::cout << "Success! Return value: " << ret << std::endl;
        std::cout << "Result JSON: " << result << std::endl;

        // Parse result
        json resultJson = json::parse(result);
        if (resultJson.contains("X") && resultJson.contains("Y") && 
            resultJson.contains("Width") && resultJson.contains("Height")) {
            std::cout << "Luminous area: X=" << resultJson["X"] 
                      << ", Y=" << resultJson["Y"]
                      << ", Width=" << resultJson["Width"]
                      << ", Height=" << resultJson["Height"] << std::endl;
        }

        FreeResult(result);
    } else {
        std::cout << "Failed! Error code: " << ret << std::endl;
    }
}

// Test 3: Omit Threshold parameter (default use auto threshold)
void testAutoThresholdOmitted()
{
    std::cout << "\n=== Test 3: Auto Threshold (Omit Threshold parameter) ===" << std::endl;

    // Create test image
    cv::Mat testImg = createTestImage(640, 480, 200, 150);
    HImage himg = createHImageFromMat(testImg);

    // Set ROI to empty (use entire image)
    RoiRect roi = {0, 0, 0, 0};

    // Create config JSON (does not contain Threshold parameter)
    json config;
    config["UseRotatedRect"] = false;
    std::string configStr = config.dump();

    // Call function
    char* result = nullptr;
    int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);

    if (ret > 0 && result != nullptr) {
        std::cout << "Success! Return value: " << ret << std::endl;
        std::cout << "Result JSON: " << result << std::endl;

        // Parse result
        json resultJson = json::parse(result);
        if (resultJson.contains("X") && resultJson.contains("Y") && 
            resultJson.contains("Width") && resultJson.contains("Height")) {
            std::cout << "Luminous area: X=" << resultJson["X"] 
                      << ", Y=" << resultJson["Y"]
                      << ", Width=" << resultJson["Width"]
                      << ", Height=" << resultJson["Height"] << std::endl;
        }

        FreeResult(result);
    } else {
        std::cout << "Failed! Error code: " << ret << std::endl;
    }
}

// Test 4: Use rotated rect mode + auto threshold
void testRotatedRectWithAutoThreshold()
{
    std::cout << "\n=== Test 4: Rotated Rect + Auto Threshold ===" << std::endl;

    // Create test image
    cv::Mat testImg = createTestImage(640, 480, 200, 150);
    HImage himg = createHImageFromMat(testImg);

    // Set ROI to empty (use entire image)
    RoiRect roi = {0, 0, 0, 0};

    // Create config JSON
    json config;
    config["Threshold"] = -1;
    config["UseRotatedRect"] = true;
    std::string configStr = config.dump();

    // Call function
    char* result = nullptr;
    int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);

    if (ret > 0 && result != nullptr) {
        std::cout << "Success! Return value: " << ret << std::endl;
        std::cout << "Result JSON: " << result << std::endl;

        // Parse result
        json resultJson = json::parse(result);
        if (resultJson.contains("Corners")) {
            std::cout << "Rotated rect corners: " << resultJson["Corners"] << std::endl;
        }

        FreeResult(result);
    } else {
        std::cout << "Failed! Error code: " << ret << std::endl;
    }
}

// Test 5: Use ROI + auto threshold
void testWithROIAndAutoThreshold()
{
    std::cout << "\n=== Test 5: ROI + Auto Threshold ===" << std::endl;

    // Create test image
    cv::Mat testImg = createTestImage(640, 480, 200, 150);
    HImage himg = createHImageFromMat(testImg);

    // Set ROI (only process center area)
    RoiRect roi = {100, 100, 440, 280};

    // Create config JSON (omit Threshold)
    json config;
    config["UseRotatedRect"] = false;
    std::string configStr = config.dump();

    // Call function
    char* result = nullptr;
    int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);

    if (ret > 0 && result != nullptr) {
        std::cout << "Success! Return value: " << ret << std::endl;
        std::cout << "Result JSON: " << result << std::endl;

        // Parse result
        json resultJson = json::parse(result);
        if (resultJson.contains("X") && resultJson.contains("Y") && 
            resultJson.contains("Width") && resultJson.contains("Height")) {
            std::cout << "Luminous area (relative to ROI): X=" << resultJson["X"] 
                      << ", Y=" << resultJson["Y"]
                      << ", Width=" << resultJson["Width"]
                      << ", Height=" << resultJson["Height"] << std::endl;
        }
    } else {
        std::cout << "Failed! Error code: " << ret << std::endl;
    }
}

// Test 6: Test from real image file
void testWithRealImage(const std::string& imagePath)
{
    std::cout << "\n=== Test 6: Test from real image file ===" << std::endl;
    std::cout << "Image path: " << imagePath << std::endl;

    // Read image
    cv::Mat image = cv::imread(imagePath, cv::IMREAD_UNCHANGED);

    if (image.empty()) {
        std::cout << "Warning: Cannot read image file, skipping this test" << std::endl;
        std::cout << "You can provide a valid image path to test real images" << std::endl;
        return;
    }

    std::cout << "Image size: " << image.cols << "x" << image.rows 
              << ", Channels: " << image.channels() 
              << ", Depth: " << image.depth() << std::endl;

    HImage himg = createHImageFromMat(image);
    RoiRect roi = {0, 0, 0, 0};

    // Test fixed threshold
    std::cout << "\n--- Fixed Threshold (Threshold=100) ---" << std::endl;
    json config1;
    config1["Threshold"] = 100;
    config1["UseRotatedRect"] = false;
    std::string configStr1 = config1.dump();

    char* result1 = nullptr;
    int ret1 = M_FindLuminousArea(himg, roi, configStr1.c_str(), &result1);
    if (ret1 > 0 && result1 != nullptr) {
        std::cout << "Result: " << result1 << std::endl;
        FreeResult(result1);
    }

    // Test auto threshold
    std::cout << "\n--- Auto Threshold (OTSU) ---" << std::endl;
    json config2;
    config2["UseRotatedRect"] = false;
    std::string configStr2 = config2.dump();

    char* result2 = nullptr;
    int ret2 = M_FindLuminousArea(himg, roi, configStr2.c_str(), &result2);
    if (ret2 > 0 && result2 != nullptr) {
        std::cout << "Result: " << result2 << std::endl;
        FreeResult(result2);
    }
}

int main(int argc, char* argv[])
{
    if (argc == 2 && std::string(argv[1]) == "--surface-defect-equivalence") {
        return runSurfaceDefectEquivalenceTests() ? 0 : 1;
    }
    if (argc == 2 && std::string(argv[1]) == "--surface-defect-benchmark") {
        return runSurfaceDefectBenchmarkMode() ? 0 : 1;
    }
    if (argc == 2 && std::string(argv[1]) == "--calibration-smoke") {
        return RunCalibrationApiSmokeTests() ? 0 : 1;
    }
    if (argc == 2 && std::string(argv[1]) == "--calibration-cache-small-budget") {
        return RunCalibrationCacheSmallBudgetTests() ? 0 : 1;
    }
    if (argc == 3 && std::string(argv[1]) == "--calibration-real-data") {
        return RunCalibrationRealDataTests(std::filesystem::u8path(argv[2])) ? 0 : 1;
    }
    if (argc == 5 && std::string(argv[1]) == "--calibration-legacy-color") {
        return RunCalibrationLegacyColorComparison(
            std::filesystem::u8path(argv[2]),
            std::filesystem::u8path(argv[3]),
            std::filesystem::u8path(argv[4])) ? 0 : 1;
    }
    if (argc == 2 && std::string(argv[1]) == "--p2-only") {
        return RunP2AlgorithmTests() ? 0 : 1;
    }
    if (argc == 2 && std::string(argv[1]) == "--native-log") {
        return RunNativeLoggingTests() ? 0 : 1;
    }

    std::cout << "========================================" << std::endl;
    std::cout << "M_FindLuminousArea smoke test" << std::endl;
    std::cout << "========================================" << std::endl;

    if (!RunCalibrationApiSmokeTests()) {
        std::cerr << "Calibration API smoke test failed" << std::endl;
        return 1;
    }

    if (!smokeHImageHelpersValidateLayoutAndOwnership()) {
        std::cerr << "HImage helper validation test failed" << std::endl;
        return 1;
    }

    if (!smokeCommonExportsFailSafely()) {
        std::cerr << "Common legacy export failure guard test failed" << std::endl;
        return 1;
    }

    if (!smokeFindLuminousArea()) {
        std::cerr << "Smoke test failed" << std::endl;
        return 1;
    }

    if (!smokeInvalidJsonDoesNotThrow()) {
        std::cerr << "Invalid JSON guard test failed" << std::endl;
        return 1;
    }

    if (!smokeFreeResultAcceptsNull()) {
        std::cerr << "FreeResult(nullptr) test failed" << std::endl;
        return 1;
    }

    if (!smokeCalArtculationInvalidImageDoesNotThrow()) {
        std::cerr << "M_CalArtculation invalid image guard test failed" << std::endl;
        return 1;
    }

    if (!smokeCalArtculationUsesRawPixelScale()) {
        std::cerr << "M_CalArtculation raw pixel scale test failed" << std::endl;
        return 1;
    }

    if (!smokeCalArtculationGray32FloatDoesNotMutateSource()) {
        std::cerr << "M_CalArtculation Gray32Float source mutation test failed" << std::endl;
        return 1;
    }

    if (!smokeGetMinMaxClearsOutputsOnFailure()) {
        std::cerr << "M_GetMinMax failure-clear test failed" << std::endl;
        return 1;
    }

    if (!smokeSfrOutputsClearOnFailure()) {
        std::cerr << "M_CalSFR failure-clear test failed" << std::endl;
        return 1;
    }

    if (!smokeSfrCalculatesSyntheticSlantedEdge()) {
        std::cerr << "M_CalSFR synthetic slanted-edge test failed" << std::endl;
        return 1;
    }

    if (!smokeSfrMatchesSfrmat5MonoFixture()) {
        std::cerr << "M_CalSFR sfrmat5 fixture regression test failed" << std::endl;
        return 1;
    }

    if (!smokeSfrMatchesSfrmat5ColorFixture()) {
        std::cerr << "M_CalSFRMultiChannel sfrmat5 color fixture regression test failed" << std::endl;
        return 1;
    }

    if (!smokeSfrBmw4In1SyntheticTarget()) {
        std::cerr << "M_CalSFRBmw4In1 synthetic BMW target test failed" << std::endl;
        return 1;
    }

    if (!smokeDistortionP9SyntheticTarget()) {
        std::cerr << "M_CalDistortionP9 synthetic point9 test failed" << std::endl;
        return 1;
    }

    if (!smokeDistortionP9ReportsMissingPoint()) {
        std::cerr << "M_CalDistortionP9 missing-point diagnostic test failed" << std::endl;
        return 1;
    }

    if (!smokeDistortionP9ReportsExtraCandidateWarning()) {
        std::cerr << "M_CalDistortionP9 extra-candidate warning test failed" << std::endl;
        return 1;
    }

    if (!smokeDistortionP9DesktopFixtureIfPresent()) {
        std::cerr << "M_CalDistortionP9 desktop fixture test failed" << std::endl;
        return 1;
    }

    if (!smokeSurfaceDefectDetectsSyntheticBrightAndDark()) {
        std::cerr << "M_DetectSurfaceDefects synthetic bright/dark test failed" << std::endl;
        return 1;
    }

    if (!smokeConvertImageHandlesStrideAndOwnedBuffer()) {
        std::cerr << "M_ConvertImage stride/ownership test failed" << std::endl;
        return 1;
    }

    if (!smokeConvertImageClearsOutputsOnFailure()) {
        std::cerr << "M_ConvertImage failure-clear test failed" << std::endl;
        return 1;
    }

    if (!smokeDrawPoiImageInvalidArgsClearOutput()) {
        std::cerr << "M_DrawPoiImage invalid-args clear test failed" << std::endl;
        return 1;
    }

    if (!smokeChannelExportsInvalidArgsClearOutput()) {
        std::cerr << "Channel export invalid-args clear test failed" << std::endl;
        return 1;
    }

    if (!smokeGammaInvalidArgsClearOutput()) {
        std::cerr << "Gamma invalid-args clear test failed" << std::endl;
        return 1;
    }

    if (!smokeAutoAdjustExportsNormalizeInputFormats()) {
        std::cerr << "Auto-adjust export format normalization test failed" << std::endl;
        return 1;
    }

    if (!smokeFusionReturnsOwnedHImage()) {
        std::cerr << "M_Fusion ownership test failed" << std::endl;
        return 1;
    }

    if (!smokeStitchImagesReturnsOwnedHImage()) {
        std::cerr << "M_StitchImages ownership test failed" << std::endl;
        return 1;
    }

    if (!smokeHImageExportsClearOutputOnFailure()) {
        std::cerr << "HImage export failure-clear test failed" << std::endl;
        return 1;
    }

    if (!smokeVideoInvalidCalls()) {
        std::cerr << "Video API invalid-call test failed" << std::endl;
        return 1;
    }

    if (!smokeVideoApiLifecycle()) {
        std::cerr << "Video API lifecycle test failed" << std::endl;
        return 1;
    }

    if (!smokeCudaFusionBatchClearsOutputsOnFailure()) {
        std::cerr << "CUDA fusion batch failure-clear test failed" << std::endl;
        return 1;
    }

    if (!RunP2AlgorithmTests()) {
        std::cerr << "P2 native algorithm regression tests failed" << std::endl;
        return 1;
    }

    if (!RunNativeLoggingTests()) {
        std::cerr << "Native logging tests failed" << std::endl;
        return 1;
    }

    cv::Mat image = createTestImage(640, 480, 200, 150);
    testFixedThreshold(image);
    testAutoThresholdExplicit(image);
    testAutoThresholdOmitted();
    testRotatedRectWithAutoThreshold();
    testWithROIAndAutoThreshold();

    if (argc > 1) {
        testWithRealImage(argv[1]);
    }

    std::cout << "\n========================================" << std::endl;
    std::cout << "All tests completed!" << std::endl;
    std::cout << "========================================" << std::endl;

    return 0;
}

