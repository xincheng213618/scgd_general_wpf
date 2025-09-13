#include "function.h"

// ������ת����
void DrawRotatedRect(cv::Mat mask, const cv::RotatedRect &rotatedrect, const cv::Scalar &color, int thickness, int lineType)
{
	// ��ȡ��ת���ε��ĸ��ǵ�
	cv::Point2f ps[4];
	rotatedrect.points(ps);

	// ����������
	std::vector<std::vector<cv::Point>> tmpContours;    // ����һ��InputArrayOfArrays ���͵ĵ㼯
	std::vector<cv::Point> contours;
	for (int i = 0; i != 4; ++i) {
		contours.emplace_back(cv::Point2i(ps[i]));
	}
	tmpContours.insert(tmpContours.end(), contours);

	// ��������������ת����
	drawContours(mask, tmpContours, 0, color, thickness, lineType);  // ���mask
}
