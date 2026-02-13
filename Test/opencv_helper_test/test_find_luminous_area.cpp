// Test for M_FindLuminousArea with self-adaptive thresholding
// Test M_FindLuminousArea adaptive threshold function

#include <iostream>
#include <opencv2/opencv.hpp>
#include <nlohmann/json.hpp>
#include "../../include/opencv_media_export.h"
#include <string>

using json = nlohmann::json;

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

// Helper function: create HImage from cv::Mat
HImage createHImageFromMat(const cv::Mat& mat)
{
    HImage himg;
    himg.rows = mat.rows;
    himg.cols = mat.cols;
    himg.channels = mat.channels();
    himg.depth = mat.elemSize1() * 8;
    himg.stride = static_cast<int>(mat.step);
    himg.pData = const_cast<unsigned char*>(mat.data);
    return himg;
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
    }
}

//int main(int argc, char* argv[])
//{
//    std::cout << "========================================" << std::endl;
//    std::cout << "M_FindLuminousArea Adaptive Threshold Test Program" << std::endl;
//    std::cout << "========================================" << std::endl;
//    cv::Mat image = cv::imread("C:\\Users\\17917\\Desktop\\2025-12\\20251125T142736.7608573_WhiteSrc.tiff");
//
//    if (image.empty()) {
//        std::cout << "Warning: Cannot read default test image, will use generated image for testing" << std::endl;
//        image = createTestImage(640, 480, 200, 150);
//    }
//
//    // Run all tests
//    testFixedThreshold(image);
//    testAutoThresholdExplicit(image);
//    testAutoThresholdOmitted();
//    testRotatedRectWithAutoThreshold();
//    testWithROIAndAutoThreshold();
//
//    // If image path argument is provided, test real image
//    if (argc > 1) {
//        testWithRealImage(argv[1]);
//    } else {
//        std::cout << "\nHint: You can provide image path via command line argument to test real image" << std::endl;
//        std::cout << "Usage: " << argv[0] << " <image_file_path>" << std::endl;
//    }
//
//    std::cout << "\n========================================" << std::endl;
//    std::cout << "All tests completed!" << std::endl;
//    std::cout << "========================================" << std::endl;
//
//    return 0;
//}

