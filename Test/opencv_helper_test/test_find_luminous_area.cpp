// Test for M_FindLuminousArea with self-adaptive thresholding
// 测试 M_FindLuminousArea 的自适应阈值功能

#include <iostream>
#include <opencv2/opencv.hpp>
#include <nlohmann/json.hpp>
#include "../../include/opencv_media_export.h"

using json = nlohmann::json;

// 辅助函数：创建测试图像（亮区在中心）
cv::Mat createTestImage(int width, int height, int brightWidth, int brightHeight)
{
    cv::Mat image = cv::Mat::zeros(height, width, CV_8UC1);
    
    // 在中心创建亮区
    int startX = (width - brightWidth) / 2;
    int startY = (height - brightHeight) / 2;
    cv::Rect brightArea(startX, startY, brightWidth, brightHeight);
    image(brightArea) = 200; // 设置亮区像素值
    
    // 添加一些噪声使其更真实
    cv::Mat noise(height, width, CV_8UC1);
    cv::randn(noise, 0, 10);
    image += noise;
    
    return image;
}

// 辅助函数：从 cv::Mat 创建 HImage
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

// 测试1：使用固定阈值
void testFixedThreshold()
{
    std::cout << "\n=== 测试1: 固定阈值 (Threshold=100) ===" << std::endl;
    
    // 创建测试图像
    cv::Mat testImg = createTestImage(640, 480, 200, 150);
    HImage himg = createHImageFromMat(testImg);
    
    // 设置ROI为空（使用整个图像）
    RoiRect roi = {0, 0, 0, 0};
    
    // 创建配置JSON（固定阈值=100）
    json config;
    config["Threshold"] = 100;
    config["UseRotatedRect"] = false;
    std::string configStr = config.dump();
    
    // 调用函数
    char* result = nullptr;
    int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);
    
    if (ret > 0 && result != nullptr) {
        std::cout << "成功! 返回值: " << ret << std::endl;
        std::cout << "结果JSON: " << result << std::endl;
        
        // 解析结果
        json resultJson = json::parse(result);
        if (resultJson.contains("X") && resultJson.contains("Y") && 
            resultJson.contains("Width") && resultJson.contains("Height")) {
            std::cout << "发光区域: X=" << resultJson["X"] 
                      << ", Y=" << resultJson["Y"]
                      << ", Width=" << resultJson["Width"]
                      << ", Height=" << resultJson["Height"] << std::endl;
        }
        
        FreeResult(result);
    } else {
        std::cout << "失败! 错误码: " << ret << std::endl;
    }
}

// 测试2：使用自动阈值（OTSU方法，Threshold=-1）
void testAutoThresholdExplicit()
{
    std::cout << "\n=== 测试2: 自动阈值 (Threshold=-1, OTSU) ===" << std::endl;
    
    // 创建测试图像
    cv::Mat testImg = createTestImage(640, 480, 200, 150);
    HImage himg = createHImageFromMat(testImg);
    
    // 设置ROI为空（使用整个图像）
    RoiRect roi = {0, 0, 0, 0};
    
    // 创建配置JSON（设置Threshold=-1启用自动阈值）
    json config;
    config["Threshold"] = -1;
    config["UseRotatedRect"] = false;
    std::string configStr = config.dump();
    
    // 调用函数
    char* result = nullptr;
    int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);
    
    if (ret > 0 && result != nullptr) {
        std::cout << "成功! 返回值: " << ret << std::endl;
        std::cout << "结果JSON: " << result << std::endl;
        
        // 解析结果
        json resultJson = json::parse(result);
        if (resultJson.contains("X") && resultJson.contains("Y") && 
            resultJson.contains("Width") && resultJson.contains("Height")) {
            std::cout << "发光区域: X=" << resultJson["X"] 
                      << ", Y=" << resultJson["Y"]
                      << ", Width=" << resultJson["Width"]
                      << ", Height=" << resultJson["Height"] << std::endl;
        }
        
        FreeResult(result);
    } else {
        std::cout << "失败! 错误码: " << ret << std::endl;
    }
}

// 测试3：省略Threshold参数（默认使用自动阈值）
void testAutoThresholdOmitted()
{
    std::cout << "\n=== 测试3: 自动阈值 (省略Threshold参数) ===" << std::endl;
    
    // 创建测试图像
    cv::Mat testImg = createTestImage(640, 480, 200, 150);
    HImage himg = createHImageFromMat(testImg);
    
    // 设置ROI为空（使用整个图像）
    RoiRect roi = {0, 0, 0, 0};
    
    // 创建配置JSON（不包含Threshold参数）
    json config;
    config["UseRotatedRect"] = false;
    std::string configStr = config.dump();
    
    // 调用函数
    char* result = nullptr;
    int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);
    
    if (ret > 0 && result != nullptr) {
        std::cout << "成功! 返回值: " << ret << std::endl;
        std::cout << "结果JSON: " << result << std::endl;
        
        // 解析结果
        json resultJson = json::parse(result);
        if (resultJson.contains("X") && resultJson.contains("Y") && 
            resultJson.contains("Width") && resultJson.contains("Height")) {
            std::cout << "发光区域: X=" << resultJson["X"] 
                      << ", Y=" << resultJson["Y"]
                      << ", Width=" << resultJson["Width"]
                      << ", Height=" << resultJson["Height"] << std::endl;
        }
        
        FreeResult(result);
    } else {
        std::cout << "失败! 错误码: " << ret << std::endl;
    }
}

// 测试4：使用旋转矩形模式 + 自动阈值
void testRotatedRectWithAutoThreshold()
{
    std::cout << "\n=== 测试4: 旋转矩形 + 自动阈值 ===" << std::endl;
    
    // 创建测试图像
    cv::Mat testImg = createTestImage(640, 480, 200, 150);
    HImage himg = createHImageFromMat(testImg);
    
    // 设置ROI为空（使用整个图像）
    RoiRect roi = {0, 0, 0, 0};
    
    // 创建配置JSON
    json config;
    config["Threshold"] = -1;
    config["UseRotatedRect"] = true;
    std::string configStr = config.dump();
    
    // 调用函数
    char* result = nullptr;
    int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);
    
    if (ret > 0 && result != nullptr) {
        std::cout << "成功! 返回值: " << ret << std::endl;
        std::cout << "结果JSON: " << result << std::endl;
        
        // 解析结果
        json resultJson = json::parse(result);
        if (resultJson.contains("Corners")) {
            std::cout << "旋转矩形角点: " << resultJson["Corners"] << std::endl;
        }
        
        FreeResult(result);
    } else {
        std::cout << "失败! 错误码: " << ret << std::endl;
    }
}

// 测试5：使用ROI + 自动阈值
void testWithROIAndAutoThreshold()
{
    std::cout << "\n=== 测试5: ROI区域 + 自动阈值 ===" << std::endl;
    
    // 创建测试图像
    cv::Mat testImg = createTestImage(640, 480, 200, 150);
    HImage himg = createHImageFromMat(testImg);
    
    // 设置ROI（只处理中心区域）
    RoiRect roi = {100, 100, 440, 280};
    
    // 创建配置JSON（省略Threshold）
    json config;
    config["UseRotatedRect"] = false;
    std::string configStr = config.dump();
    
    // 调用函数
    char* result = nullptr;
    int ret = M_FindLuminousArea(himg, roi, configStr.c_str(), &result);
    
    if (ret > 0 && result != nullptr) {
        std::cout << "成功! 返回值: " << ret << std::endl;
        std::cout << "结果JSON: " << result << std::endl;
        
        // 解析结果
        json resultJson = json::parse(result);
        if (resultJson.contains("X") && resultJson.contains("Y") && 
            resultJson.contains("Width") && resultJson.contains("Height")) {
            std::cout << "发光区域（相对于ROI）: X=" << resultJson["X"] 
                      << ", Y=" << resultJson["Y"]
                      << ", Width=" << resultJson["Width"]
                      << ", Height=" << resultJson["Height"] << std::endl;
        }
        
        FreeResult(result);
    } else {
        std::cout << "失败! 错误码: " << ret << std::endl;
    }
}

// 测试6：从真实图像文件测试
void testWithRealImage(const std::string& imagePath)
{
    std::cout << "\n=== 测试6: 从真实图像文件测试 ===" << std::endl;
    std::cout << "图像路径: " << imagePath << std::endl;
    
    // 读取图像
    cv::Mat image = cv::imread(imagePath, cv::IMREAD_UNCHANGED);
    
    if (image.empty()) {
        std::cout << "警告: 无法读取图像文件，跳过此测试" << std::endl;
        std::cout << "您可以提供有效的图像路径来测试真实图像" << std::endl;
        return;
    }
    
    std::cout << "图像尺寸: " << image.cols << "x" << image.rows 
              << ", 通道数: " << image.channels() 
              << ", 深度: " << image.depth() << std::endl;
    
    HImage himg = createHImageFromMat(image);
    RoiRect roi = {0, 0, 0, 0};
    
    // 测试固定阈值
    std::cout << "\n--- 固定阈值 (Threshold=100) ---" << std::endl;
    json config1;
    config1["Threshold"] = 100;
    config1["UseRotatedRect"] = false;
    std::string configStr1 = config1.dump();
    
    char* result1 = nullptr;
    int ret1 = M_FindLuminousArea(himg, roi, configStr1.c_str(), &result1);
    if (ret1 > 0 && result1 != nullptr) {
        std::cout << "结果: " << result1 << std::endl;
        FreeResult(result1);
    }
    
    // 测试自动阈值
    std::cout << "\n--- 自动阈值 (OTSU) ---" << std::endl;
    json config2;
    config2["UseRotatedRect"] = false;
    std::string configStr2 = config2.dump();
    
    char* result2 = nullptr;
    int ret2 = M_FindLuminousArea(himg, roi, configStr2.c_str(), &result2);
    if (ret2 > 0 && result2 != nullptr) {
        std::cout << "结果: " << result2 << std::endl;
        FreeResult(result2);
    }
}

int main(int argc, char* argv[])
{
    std::cout << "========================================" << std::endl;
    std::cout << "M_FindLuminousArea 自适应阈值测试程序" << std::endl;
    std::cout << "========================================" << std::endl;
    
    // 运行所有测试
    testFixedThreshold();
    testAutoThresholdExplicit();
    testAutoThresholdOmitted();
    testRotatedRectWithAutoThreshold();
    testWithROIAndAutoThreshold();
    
    // 如果提供了图像路径参数，测试真实图像
    if (argc > 1) {
        testWithRealImage(argv[1]);
    } else {
        std::cout << "\n提示: 您可以通过命令行参数提供图像路径来测试真实图像" << std::endl;
        std::cout << "用法: " << argv[0] << " <图像文件路径>" << std::endl;
    }
    
    std::cout << "\n========================================" << std::endl;
    std::cout << "所有测试完成!" << std::endl;
    std::cout << "========================================" << std::endl;
    
    return 0;
}
