#include "function.h"

// 绘制旋转矩形
void DrawRotatedRect(cv::Mat mask, const cv::RotatedRect &rotatedrect, const cv::Scalar &color, int thickness, int lineType)
{
	// 提取旋转矩形的四个角点
	cv::Point2f ps[4];
	rotatedrect.points(ps);

	// 构建轮廓线
	std::vector<std::vector<cv::Point>> tmpContours;    // 创建一个InputArrayOfArrays 类型的点集
	std::vector<cv::Point> contours;
	for (int i = 0; i != 4; ++i) {
		contours.emplace_back(cv::Point2i(ps[i]));
	}
	tmpContours.insert(tmpContours.end(), contours);

	// 绘制轮廓，即旋转矩形
	drawContours(mask, tmpContours, 0, color, thickness, lineType);  // 填充mask
}
