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

void AutoLevelsAdjust(cv::Mat& src, cv::Mat& dst)
{
    CV_Assert(!src.empty() && src.channels() == 3);
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