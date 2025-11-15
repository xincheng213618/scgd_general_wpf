#pragma once
#include <string>
#include <vector>
#include <cstdint>
#include <opencv2/opencv.hpp>

enum class CVType
{
    None = -1,
    Raw,
    Src,
    CIE,
    Calibration,
    Tif,
    Dat
};

struct CVCIEFile
{
    uint32_t    Version = 1;               // 文件版本
    CVType      FileExtType = CVType::None;  // 扩展类型（Raw/CIE/Src/Tif 等）
    int         Rows = 0;               // 图像高度
    int         Cols = 0;               // 图像宽度
    int         Bpp = 0;               // 每通道位深（8/16/32/64）
    int         Channels = 0;               // 通道数
    float       Gain = 1.0f;            // 增益
    std::vector<float> Exp;                  // 每通道曝光值
    std::string SrcFileName;                 // 源文件名（字符串，在头里）
    std::vector<uint8_t> Data;               // 原始图像数据（连续缓冲）
    std::string FilePath;                    // 实际文件路径（可选）

    // 与 C# Depth 属性对应的 OpenCV depth 常量
    int getCvDepth() const
    {
        switch (Bpp)
        {
        case 8:  return CV_8U;
        case 16: return CV_16U;
        case 32: return CV_32F;
        case 64: return CV_64F;
        default: return CV_8U; // 默认
        }
    }

    // 转成 Mat（不做归一化，只是视图）
    cv::Mat toMatView() const
    {
        if (Rows <= 0 || Cols <= 0 || Channels <= 0 || Data.empty())
            return cv::Mat();

        int depth = getCvDepth();
        int type = CV_MAKETYPE(depth, Channels);
        cv::Mat mat = cv::Mat(Rows, Cols, type, const_cast<uint8_t*>(Data.data()));
        // 注意：此 Mat 共享 Data 数据，不复制
        return mat;
    }

    // 生成可显示的 8U Mat（仿 C# 里 Bpp==32 时 Normalize+ConvertTo 的流程）
    cv::Mat toDisplayMat() const
    {
        cv::Mat src = toMatView();
        if (src.empty())
            return cv::Mat();

        if (Bpp == 32)
        {
            cv::Mat norm, dst8u;
            cv::normalize(src, norm, 0, 255, cv::NORM_MINMAX);
            norm.convertTo(dst8u, CV_8U);
            return dst8u;
        }
        else
        {
            // 已经是 8/16/64 等，根据使用场景再转换
            return src;
        }
    }
};