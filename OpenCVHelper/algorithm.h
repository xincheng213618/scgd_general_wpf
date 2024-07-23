#pragma once

#pragma warning(disable:4305 4244)

#include <opencv2/core/core.hpp>  

void autoLevelsAdjust(cv::Mat& src, cv::Mat& dst);

void automaticColorAdjustment(cv::Mat& image);

void automaticToneAdjustment(cv::Mat& image, double clip_hist_percent = 1);
