// Test for M_FindLuminousArea with self-adaptive thresholding
// Test M_FindLuminousArea adaptive threshold function

#include <iostream>
#include <opencv2/opencv.hpp>
#include <nlohmann/json.hpp>
#include "../../Native/include/opencv_media_export.h"
#include "../../Native/include/video_export.h"
#include <atomic>
#include <chrono>
#include <cmath>
#include <combaseapi.h>
#include <cstddef>
#include <cstdio>
#include <cstdint>
#include <filesystem>
#include <limits>
#include <string>
#include <thread>
#include <vector>

using json = nlohmann::json;

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
    std::cout << "========================================" << std::endl;
    std::cout << "M_FindLuminousArea smoke test" << std::endl;
    std::cout << "========================================" << std::endl;

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

