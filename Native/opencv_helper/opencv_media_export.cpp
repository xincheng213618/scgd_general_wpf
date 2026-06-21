#include "pch.h"
#include "Windows.h"
#include "opencv_media_export.h"
#include "algorithm.h"
#include "algorithm/distortion/distortion_p9.h"
#include "algorithm/sfr/sfr_bmw4.h"
#include "algorithm/surface_defect/surface_defect.h"
#include <opencv2/opencv.hpp>
#include <nlohmann\json.hpp>
#include <algorithm>
#include <string>
#include <locale>
#include <codecvt>
#include <cmath>
#include <combaseapi.h>
#include <cctype>
#include <cstring>
#include <exception>
#include <future>
#include <limits>
#include <memory>
#include <vector>

using json = nlohmann::json;

namespace
{
constexpr int ExportInvalidArgument = -1;
constexpr int ExportAlgorithmFailed = -2;
constexpr int ExportAllocationFailed = -3;
constexpr int ExportInvalidJson = -4;
constexpr int ExportOpenCvException = -5;
constexpr int ExportStdException = -6;
constexpr int ExportUnknownException = -7;

template <typename Func>
int GuardIntExport(Func func) noexcept
{
	try {
		return func();
	}
	catch (const json::exception&) {
		return ExportInvalidJson;
	}
	catch (const cv::Exception&) {
		return ExportOpenCvException;
	}
	catch (const std::exception&) {
		return ExportStdException;
	}
	catch (...) {
		return ExportUnknownException;
	}
}

template <typename Func>
double GuardDoubleExport(Func func) noexcept
{
	try {
		return func();
	}
	catch (const cv::Exception&) {
		return -1.0;
	}
	catch (const std::exception&) {
		return -1.0;
	}
	catch (...) {
		return -1.0;
	}
}

template <typename Func>
int GuardHImageExport(HImage* outImage, Func func) noexcept
{
	if (outImage != nullptr) {
		*outImage = HImage{};
	}

	return GuardIntExport([&]() -> int {
		if (outImage == nullptr) {
			return ExportInvalidArgument;
		}

		return func();
		});
}

cv::Mat CreateMatView(const HImage& img)
{
	return HImageToMatView(img);
}

bool TryParseJson(const char* text, json& parsed)
{
	if (text == nullptr) {
		return false;
	}

	parsed = json::parse(text, nullptr, false);
	return !parsed.is_discarded();
}

bool TryParseJson(const std::string& text, json& parsed)
{
	parsed = json::parse(text, nullptr, false);
	return !parsed.is_discarded();
}

int CopyJsonResult(const json& outputJson, char** result)
{
	if (result == nullptr) {
		return ExportInvalidArgument;
	}

	*result = nullptr;
	const std::string output = outputJson.dump();
	const size_t length = output.length() + 1;
	if (length > static_cast<size_t>(std::numeric_limits<int>::max())) {
		return ExportAllocationFailed;
	}

	char* buffer = static_cast<char*>(CoTaskMemAlloc(static_cast<SIZE_T>(length)));
	if (buffer == nullptr) {
		return ExportAllocationFailed;
	}

	std::memcpy(buffer, output.c_str(), length);
	*result = buffer;
	return static_cast<int>(length);
}

void ReadBmwSfr4ConfigFields(const json& j, cvcore::sfr::BmwSfr4Config& config)
{
	if (!j.is_object()) {
		return;
	}

	config.pixelPitch = j.value("PixelPitch", j.value("pixelPitch", config.pixelPitch));
	config.polynomialDegree = j.value("PolynomialDegree", j.value("polynomialDegree", config.polynomialDegree));
	config.binning = j.value("Binning", j.value("binning", config.binning));
	config.threshold = j.value("Threshold", j.value("threshold", config.threshold));
	config.minTargetArea = j.value("MinArea", j.value("minTargetArea", config.minTargetArea));
	config.maxTargetArea = j.value("MaxArea", j.value("maxTargetArea", config.maxTargetArea));
	config.maxTargets = j.value("MaxTargets", j.value("maxTargets", config.maxTargets));
	config.roiWidth = j.value("RoiWidth", j.value("roi_w", j.value("dst_roi_w", config.roiWidth)));
	config.roiHeight = j.value("RoiHeight", j.value("roi_h", j.value("dst_roi_h", config.roiHeight)));
	config.maxCurveLength = j.value("MaxCurveLength", j.value("maxCurveLength", config.maxCurveLength));
	config.edgeOffsetRatio = j.value("EdgeOffsetRatio", j.value("edgeOffsetRatio", config.edgeOffsetRatio));
	config.minAspectRatio = j.value("MinAspectRatio", j.value("minAspectRatio", config.minAspectRatio));
	config.maxAspectRatio = j.value("MaxAspectRatio", j.value("maxAspectRatio", config.maxAspectRatio));
	config.closeKernel = j.value("CloseKernel", j.value("closeKernel", config.closeKernel));
	config.openKernel = j.value("OpenKernel", j.value("openKernel", config.openKernel));
	config.borderMargin = j.value("BorderMargin", j.value("borderMargin", config.borderMargin));
	config.requireFullTarget = j.value("RequireFullTarget", j.value("requireFullTarget", config.requireFullTarget));
	config.requireFourCurves = j.value("RequireFourCurves", j.value("requireFourCurves", config.requireFourCurves));
	config.usePcaAngle = j.value("UsePcaAngle", j.value("usePcaAngle", config.usePcaAngle));

	config.activeEdges[0] = j.value("ActiveLeft", j.value("active_Left", config.activeEdges[0]));
	config.activeEdges[1] = j.value("ActiveTop", j.value("active_Top", config.activeEdges[1]));
	config.activeEdges[2] = j.value("ActiveRight", j.value("active_Right", config.activeEdges[2]));
	config.activeEdges[3] = j.value("ActiveBottom", j.value("active_Bottom", config.activeEdges[3]));
}

cvcore::sfr::BmwSfr4Config ParseBmwSfr4Config(const json& root)
{
	cvcore::sfr::BmwSfr4Config config;
	ReadBmwSfr4ConfigFields(root, config);
	if (root.contains("sfrAutoPoi1")) {
		ReadBmwSfr4ConfigFields(root.at("sfrAutoPoi1"), config);
	}
	return config;
}

void ReadDistortionP9ConfigFields(const json& j, cvcore::distortion::DistortionP9Config& config)
{
	if (!j.is_object()) {
		return;
	}

	config.expectedRows = j.value("expectedRows", j.value("brightNumY", config.expectedRows));
	config.expectedCols = j.value("expectedCols", j.value("brightNumX", config.expectedCols));
	config.threshold = j.value("threshold", j.value("Threshold", config.threshold));
	config.brightTarget = j.value("brightTarget", j.value("BrightTarget", config.brightTarget));
	config.minRectSize = j.value("minRectSize", j.value("outRectSizeMin", j.value("MinRectSize", config.minRectSize)));
	config.maxRectSize = j.value("maxRectSize", j.value("outRectSizeMax", j.value("MaxRectSize", config.maxRectSize)));
	config.minArea = j.value("minArea", j.value("MinArea", config.minArea));
	config.maxArea = j.value("maxArea", j.value("MaxArea", config.maxArea));
	config.erodeKernel = j.value("erodeKernel", j.value("ErodeKernel", config.erodeKernel));
	config.erodeIterations = j.value("erodeIterations", j.value("erodeTime", j.value("ErodeIterations", config.erodeIterations)));
	config.dilateIterations = j.value("dilateIterations", j.value("DilateIterations", config.dilateIterations));
	config.maxCandidates = j.value("maxCandidates", j.value("MaxCandidates", config.maxCandidates));
	config.tvCalcWay = j.value("tvCalcWay", j.value("TvCaclWay", config.tvCalcWay));
	config.sortWithPca = j.value("sortWithPca", j.value("SortWithPca", config.sortWithPca));
}

cvcore::distortion::DistortionP9Config ParseDistortionP9Config(const json& root)
{
	cvcore::distortion::DistortionP9Config config;
	ReadDistortionP9ConfigFields(root, config);
	if (root.contains("CommonParams")) {
		ReadDistortionP9ConfigFields(root.at("CommonParams"), config);
	}
	if (root.contains("Point9Params")) {
		ReadDistortionP9ConfigFields(root.at("Point9Params"), config);
	}
	if (root.contains("caclDistorType")) {
		ReadDistortionP9ConfigFields(root.at("caclDistorType"), config);
	}
	return config;
}

double ReadRatioValue(const json& root, const char* lowerName, const char* upperName, double fallback)
{
	double value = root.value(lowerName, root.value(upperName, fallback));
	return value > 1.0 ? value / 100.0 : value;
}

std::vector<int> ReadIntArray(const json& root, const char* lowerName, const char* upperName, const std::vector<int>& fallback)
{
	const json* value = nullptr;
	if (root.contains(lowerName) && root.at(lowerName).is_array()) {
		value = &root.at(lowerName);
	}
	else if (root.contains(upperName) && root.at(upperName).is_array()) {
		value = &root.at(upperName);
	}

	if (value == nullptr) {
		return fallback;
	}

	std::vector<int> output;
	output.reserve(value->size());
	for (const auto& item : *value) {
		if (item.is_number_integer()) {
			output.push_back(item.get<int>());
		}
	}

	return output.empty() ? fallback : output;
}

int ReadSurfaceDefectChannel(const json& root, int fallback)
{
	if (root.contains("channel")) {
		const json& value = root.at("channel");
		if (value.is_number_integer()) {
			return value.get<int>();
		}
		if (value.is_string()) {
			std::string channel = value.get<std::string>();
			std::transform(channel.begin(), channel.end(), channel.begin(), [](unsigned char c) {
				return static_cast<char>(std::tolower(c));
			});
			if (channel == "b" || channel == "blue") return 0;
			if (channel == "g" || channel == "green") return 1;
			if (channel == "r" || channel == "red") return 2;
			return -1;
		}
	}
	if (root.contains("Channel") && root.at("Channel").is_number_integer()) {
		return root.at("Channel").get<int>();
	}
	return fallback;
}

cvcore::surface_defect::SurfaceDefectConfig ParseSurfaceDefectConfig(const json& root)
{
	cvcore::surface_defect::SurfaceDefectConfig config;
	if (!root.is_object()) {
		return config;
	}

	config.channel = ReadSurfaceDefectChannel(root, config.channel);
	config.scales = ReadIntArray(root, "scales", "Scales", config.scales);
	config.darkThreshold = ReadRatioValue(root, "darkThreshold", "DarkThreshold", config.darkThreshold);
	config.brightThreshold = ReadRatioValue(root, "brightThreshold", "BrightThreshold", config.brightThreshold);
	config.minArea = root.value("minArea", root.value("MinArea", config.minArea));
	config.maxArea = root.value("maxArea", root.value("MaxArea", config.maxArea));
	config.muraMinArea = root.value("muraMinArea", root.value("MuraMinArea", config.muraMinArea));
	config.openKernel = root.value("openKernel", root.value("OpenKernel", config.openKernel));
	config.closeKernel = root.value("closeKernel", root.value("CloseKernel", config.closeKernel));
	config.mergeDistance = root.value("mergeDistance", root.value("MergeDistance", config.mergeDistance));
	config.maxDefects = root.value("maxDefects", root.value("MaxDefects", config.maxDefects));
	config.enableDark = root.value("enableDark", root.value("EnableDark", config.enableDark));
	config.enableBright = root.value("enableBright", root.value("EnableBright", config.enableBright));
	config.enableLineDetect = root.value("enableLineDetect", root.value("EnableLineDetect", config.enableLineDetect));
	config.lineAspectRatio = root.value("lineAspectRatio", root.value("LineAspectRatio", config.lineAspectRatio));
	config.minSeverity = root.value("minSeverity", root.value("MinSeverity", config.minSeverity));
	config.minorSeverity = root.value("minorSeverity", root.value("MinorSeverity", config.minorSeverity));
	config.majorSeverity = root.value("majorSeverity", root.value("MajorSeverity", config.majorSeverity));
	config.criticalSeverity = root.value("criticalSeverity", root.value("CriticalSeverity", config.criticalSeverity));
	return config;
}

json SurfaceDefectConfigToJson(const cvcore::surface_defect::SurfaceDefectConfig& config)
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

json DistortionP9ConfigToJson(const cvcore::distortion::DistortionP9Config& config)
{
	return json{
		{ "expectedRows", config.expectedRows },
		{ "expectedCols", config.expectedCols },
		{ "threshold", config.threshold },
		{ "brightTarget", config.brightTarget },
		{ "minRectSize", config.minRectSize },
		{ "maxRectSize", config.maxRectSize },
		{ "minArea", config.minArea },
		{ "maxArea", config.maxArea },
		{ "erodeKernel", config.erodeKernel },
		{ "erodeIterations", config.erodeIterations },
		{ "dilateIterations", config.dilateIterations },
		{ "maxCandidates", config.maxCandidates },
		{ "tvCalcWay", config.tvCalcWay },
		{ "sortWithPca", config.sortWithPca }
	};
}

json DistortionP9MetricsToJson(const cvcore::distortion::DistortionP9Metric& metrics)
{
	return json{
		{ "horizontalTvPercent", metrics.horizontalTvPercent },
		{ "verticalTvPercent", metrics.verticalTvPercent },
		{ "topPercent", metrics.topPercent },
		{ "bottomPercent", metrics.bottomPercent },
		{ "leftPercent", metrics.leftPercent },
		{ "rightPercent", metrics.rightPercent },
		{ "keystoneHorizontalPercent", metrics.keystoneHorizontalPercent },
		{ "keystoneVerticalPercent", metrics.keystoneVerticalPercent },
		{ "topWidth", metrics.topWidth },
		{ "middleWidth", metrics.middleWidth },
		{ "bottomWidth", metrics.bottomWidth },
		{ "leftHeight", metrics.leftHeight },
		{ "centerHeight", metrics.centerHeight },
		{ "rightHeight", metrics.rightHeight },
		{ "gridWidth", metrics.gridWidth },
		{ "gridHeight", metrics.gridHeight }
	};
}

json StringArrayToJson(const std::vector<std::string>& values)
{
	json output = json::array();
	for (const auto& value : values) {
		output.push_back(value);
	}
	return output;
}

json RectToJson(const cv::Rect& rect, const cv::Point& origin);

json DistortionP9PointToJson(const cvcore::distortion::DistortionP9Point& point, const cv::Point& origin)
{
	return json{
		{ "id", point.id },
		{ "row", point.row },
		{ "col", point.col },
		{ "name", point.name },
		{ "x", point.center.x + origin.x },
		{ "y", point.center.y + origin.y },
		{ "area", point.area },
		{ "boundingRect", RectToJson(point.boundingRect, origin) }
	};
}

cv::Rect TryGetConfigMaskRect(const json& root)
{
	if (!root.contains("MaskRect") || !root.at("MaskRect").is_object()) {
		return cv::Rect();
	}

	const json& mask = root.at("MaskRect");
	if (!mask.value("enable", false)) {
		return cv::Rect();
	}

	return cv::Rect(
		mask.value("x", 0),
		mask.value("y", 0),
		mask.value("w", 0),
		mask.value("h", 0));
}

json RectToJson(const cv::Rect& rect, const cv::Point& origin)
{
	return json{
		{ "x", rect.x + origin.x },
		{ "y", rect.y + origin.y },
		{ "w", rect.width },
		{ "h", rect.height }
	};
}

json PointToJson(const cv::Point2d& point, const cv::Point& origin)
{
	return json{
		{ "x", point.x + origin.x },
		{ "y", point.y + origin.y }
	};
}

std::string SurfaceDefectGrade(double severity, const cvcore::surface_defect::SurfaceDefectConfig& config)
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

json SurfaceDefectToJson(
	const cvcore::surface_defect::SurfaceDefectItem& defect,
	const cv::Point& origin,
	const cvcore::surface_defect::SurfaceDefectConfig& config)
{
	return json{
		{ "id", defect.id },
		{ "type", defect.type },
		{ "polarity", defect.polarity },
		{ "grade", SurfaceDefectGrade(defect.severity, config) },
		{ "scale", defect.scale },
		{ "x", defect.boundingRect.x + origin.x },
		{ "y", defect.boundingRect.y + origin.y },
		{ "w", defect.boundingRect.width },
		{ "h", defect.boundingRect.height },
		{ "centerX", defect.center.x + origin.x },
		{ "centerY", defect.center.y + origin.y },
		{ "area", defect.area },
		{ "meanDelta", defect.meanDelta },
		{ "minDelta", defect.minDelta },
		{ "maxDelta", defect.maxDelta },
		{ "maxDeltaAbs", defect.maxDeltaAbs },
		{ "severity", defect.severity },
		{ "aspectRatio", defect.aspectRatio },
		{ "fillRatio", defect.fillRatio },
		{ "boundingRect", RectToJson(defect.boundingRect, origin) }
	};
}

json SfrCurveToJson(const cvcore::sfr::BmwSfr4Curve& curve, const cv::Point& origin, int maxCurveLength)
{
	json curveJson;
	curveJson["id"] = curve.id;
	curveJson["name"] = curve.name;
	curveJson["roi"] = RectToJson(curve.roi, origin);
	curveJson["edgeSlope"] = curve.sfr.edgeSlope;
	curveJson["mtf10_norm"] = curve.sfr.mtf10_norm;
	curveJson["mtf50_norm"] = curve.sfr.mtf50_norm;
	curveJson["mtf10_cypix"] = curve.sfr.mtf10_cypix;
	curveJson["mtf50_cypix"] = curve.sfr.mtf50_cypix;

	int length = static_cast<int>(std::min(curve.sfr.freq.size(), curve.sfr.sfr.size()));
	if (maxCurveLength > 0) {
		length = std::min(length, maxCurveLength);
	}

	curveJson["frequency"] = json::array();
	curveJson["domainSamplingData"] = json::array();
	for (int i = 0; i < length; ++i) {
		curveJson["frequency"].push_back(curve.sfr.freq[static_cast<size_t>(i)]);
		curveJson["domainSamplingData"].push_back(curve.sfr.sfr[static_cast<size_t>(i)]);
	}

	return curveJson;
}

struct CoTaskMemBufferDeleter
{
	void operator()(unsigned char* data) const noexcept
	{
		if (data != nullptr) {
			CoTaskMemFree(data);
		}
	}
};

using CoTaskMemBuffer = std::unique_ptr<unsigned char, CoTaskMemBufferDeleter>;

int SelectSingleChannelSource(const cv::Mat& mat, int channel, cv::Mat& temp, cv::Mat& source)
{
	if (mat.empty()) {
		return -1;
	}

	if (mat.channels() == 1) {
		source = mat;
		return 0;
	}

	if (channel >= 0 && channel < mat.channels()) {
		cv::extractChannel(mat, temp, channel);
	}
	else {
		cv::cvtColor(mat, temp, cv::COLOR_BGR2GRAY);
	}

	source = temp;
	return 0;
}

bool IsPseudoColorOutputCompatible(const cv::Mat& source, const cv::Mat& output)
{
	return !source.empty()
		&& !output.empty()
		&& output.rows == source.rows
		&& output.cols == source.cols
		&& output.type() == CV_8UC3;
}

bool TryBuildBgr8Image(const cv::Mat& mat, cv::Mat& bgr8)
{
	if (mat.empty()) {
		return false;
	}

	cv::Mat bgr;
	switch (mat.channels())
	{
	case 3:
		bgr = mat;
		break;
	case 4:
		cv::cvtColor(mat, bgr, cv::COLOR_BGRA2BGR);
		break;
	default:
		return false;
	}

	if (bgr.depth() == CV_8U) {
		bgr8 = bgr;
		return !bgr8.empty() && bgr8.type() == CV_8UC3;
	}

	cv::Mat source = bgr;
	cv::Mat temp32;
	if (bgr.depth() == CV_64F) {
		bgr.convertTo(temp32, CV_MAKETYPE(CV_32F, bgr.channels()));
		source = temp32;
	}
	if (source.depth() == CV_32F) {
		cv::patchNaNs(source, 0.0);
	}

	cv::normalize(source, bgr8, 0, 255, cv::NORM_MINMAX, CV_8U);
	return !bgr8.empty() && bgr8.type() == CV_8UC3;
}

cv::Mat ClipToRoi(const cv::Mat& mat, const RoiRect& roi)
{
	if (mat.empty()) {
		return mat;
	}

	const cv::Rect imageRect(0, 0, mat.cols, mat.rows);
	const cv::Rect requested(roi.x, roi.y, roi.width, roi.height);
	const cv::Rect clipped = requested & imageRect;

	if (clipped.width > 0 && clipped.height > 0) {
		return mat(clipped);
	}

	return mat;
}

int LoadImagesParallel(const std::vector<std::string>& files, std::vector<cv::Mat>& images)
{
	if (files.empty()) {
		return ExportInvalidArgument;
	}

	for (const auto& file : files) {
		if (file.empty()) {
			return ExportInvalidArgument;
		}
	}

	std::vector<std::future<cv::Mat>> futures;
	futures.reserve(files.size());
	for (const auto& file : files) {
		futures.emplace_back(std::async(std::launch::async, [file]() {
			return cv::imread(file, cv::IMREAD_UNCHANGED);
			}));
	}

	images.resize(files.size());
	for (size_t i = 0; i < futures.size(); ++i) {
		images[i] = futures[i].get();
		if (images[i].empty()) {
			return ExportAlgorithmFailed;
		}
	}

	return 0;
}

bool TryConvertToGray(const cv::Mat& mat, cv::Mat& grayMat)

{
	switch (mat.channels())
	{
	case 1:
		grayMat = mat;
		break;
	case 3:
		cv::cvtColor(mat, grayMat, cv::COLOR_BGR2GRAY);
		break;
	case 4:
		cv::cvtColor(mat, grayMat, cv::COLOR_BGRA2GRAY);
		break;
	default:
		cv::extractChannel(mat, grayMat, 0);
		break;
	}

	return !grayMat.empty();
}

double GetFocusLinearScale(int depth)
{
	switch (depth)
	{
	case CV_8U:
		return 1.0 / 255.0;
	case CV_16U:
		return 1.0 / 65535.0;
	default:
		return 1.0;
	}
}

bool TryBuildGrayFocusInput(const HImage& img, const RoiRect& roi, cv::Mat& grayMat, double& linearScale, double& squaredScale)
{
	cv::Mat mat = ClipToRoi(CreateMatView(img), roi);
	if (mat.empty() || mat.data == nullptr) {
		return false;
	}

	linearScale = GetFocusLinearScale(mat.depth());
	squaredScale = linearScale * linearScale;

	if (mat.depth() == CV_64F) {
		cv::Mat mat32;
		mat.convertTo(mat32, CV_MAKETYPE(CV_32F, mat.channels()));
		if (!TryConvertToGray(mat32, grayMat)) {
			return false;
		}
		cv::patchNaNs(grayMat, 0.0f);
		linearScale = 1.0;
		squaredScale = 1.0;
		return true;
	}

	if (!TryConvertToGray(mat, grayMat)) {
		return false;
	}

	if (grayMat.depth() == CV_32F) {
		cv::patchNaNs(grayMat, 0.0f);
	}
	else if (grayMat.depth() != CV_8U && grayMat.depth() != CV_16U) {
		return false;
	}

	return true;
}

inline double Square(double value)
{
	return value * value;
}
}


COLORVISIONCORE_API void M_FreeHImageData(unsigned char* data)
{
	if (data != nullptr) {
		CoTaskMemFree(data);
	}
}

COLORVISIONCORE_API double M_CalArtculation(HImage img, FocusAlgorithm type, RoiRect roi)
{
	return GuardDoubleExport([&]() -> double {
		cv::Mat gray_mat;
		double linearScale = 1.0;
		double squaredScale = 1.0;
		if (!TryBuildGrayFocusInput(img, roi, gray_mat, linearScale, squaredScale)) {
			return -1.0;
		}

		double value = -1.0;
		cv::Mat mean;
		cv::Mat stddev;
		cv::Mat laplacian_mat;
		cv::Mat grad_x;
		cv::Mat grad_y;
		cv::Mat gradient_mat;

		switch (type)
		{
		case Variance:
			cv::meanStdDev(gray_mat, mean, stddev);
			value = Square(stddev.at<double>(0, 0)) * squaredScale;
			break;

		case StandardDeviation:
			cv::meanStdDev(gray_mat, mean, stddev);
			value = stddev.at<double>(0, 0) * linearScale;
			break;

		case Tenengrad:
			if (gray_mat.rows < 2 || gray_mat.cols < 2) {
				return 0.0;
			}
			cv::Sobel(gray_mat, grad_x, CV_32F, 1, 0, 3);
			cv::Sobel(gray_mat, grad_y, CV_32F, 0, 1, 3);
			cv::magnitude(grad_x, grad_y, gradient_mat);
			value = cv::mean(gradient_mat)[0] * linearScale;
			break;

		case Laplacian:
			if (gray_mat.rows < 2 || gray_mat.cols < 2) {
				return 0.0;
			}
			cv::Laplacian(gray_mat, laplacian_mat, CV_32F, 3);
			value = cv::mean(cv::abs(laplacian_mat))[0] * linearScale;
			break;

		case VarianceOfLaplacian:
			if (gray_mat.rows < 2 || gray_mat.cols < 2) {
				return 0.0;
			}
			cv::Laplacian(gray_mat, laplacian_mat, CV_32F, 3);
			cv::meanStdDev(laplacian_mat, mean, stddev);
			value = Square(stddev.at<double>(0, 0)) * squaredScale;
			break;

		case EnergyOfGradient:
			if (gray_mat.rows < 2 || gray_mat.cols < 2) {
				return 0.0;
			}
			cv::subtract(gray_mat(cv::Rect(1, 0, gray_mat.cols - 1, gray_mat.rows)), gray_mat(cv::Rect(0, 0, gray_mat.cols - 1, gray_mat.rows)), grad_x, cv::noArray(), CV_32F);
			cv::subtract(gray_mat(cv::Rect(0, 1, gray_mat.cols, gray_mat.rows - 1)), gray_mat(cv::Rect(0, 0, gray_mat.cols, gray_mat.rows - 1)), grad_y, cv::noArray(), CV_32F);
			cv::multiply(grad_x, grad_x, grad_x);
			cv::multiply(grad_y, grad_y, grad_y);
			value = (cv::mean(grad_x)[0] + cv::mean(grad_y)[0]) * squaredScale;
			break;

		case SpatialFrequency:
		{
			if (gray_mat.rows < 2 || gray_mat.cols < 2) {
				return 0.0;
			}

			double RF = 0.0;
			double CF = 0.0;
			cv::Mat diff_x;
			cv::Mat diff_y;

			cv::subtract(gray_mat.colRange(1, gray_mat.cols), gray_mat.colRange(0, gray_mat.cols - 1), diff_x, cv::noArray(), CV_32F);
			cv::subtract(gray_mat.rowRange(1, gray_mat.rows), gray_mat.rowRange(0, gray_mat.rows - 1), diff_y, cv::noArray(), CV_32F);
			cv::multiply(diff_x, diff_x, diff_x);
			cv::multiply(diff_y, diff_y, diff_y);
			RF = std::sqrt(cv::mean(diff_x)[0]);
			CF = std::sqrt(cv::mean(diff_y)[0]);
			value = std::sqrt(RF * RF + CF * CF) * linearScale;
			break;
		}
		default:
			cv::meanStdDev(gray_mat, mean, stddev);
			value = Square(stddev.at<double>(0, 0)) * squaredScale;
			break;
		}

		return std::isfinite(value) ? value : -1.0;
		});
}

	COLORVISIONCORE_API int FreeResult(char* result) {
		if (result != nullptr) {
			CoTaskMemFree(result);
		}
		return 0;
	}



COLORVISIONCORE_API int M_PseudoColor(HImage img, HImage* outImage, uint min, uint max, cv::ColormapTypes types, int channel)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);

		if (mat.empty())
			return -1;

		cv::Mat temp;
		cv::Mat source;
		int sourceRet = SelectSingleChannelSource(mat, channel, temp, source);
		if (sourceRet != 0)
			return sourceRet;

		cv::Mat out;
		int ret = pseudoColorTo(source, out, min, max, types);
		if (ret != 0)
			return ret;

		return MatToHImage(out, outImage);
		});
}

COLORVISIONCORE_API int M_PseudoColorAutoRange(HImage img, HImage* outImage, uint min, uint max, cv::ColormapTypes types, int channel, uint dataMin, uint dataMax)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);

		if (mat.empty())
			return -1;

		cv::Mat temp;
		cv::Mat source;
		int sourceRet = SelectSingleChannelSource(mat, channel, temp, source);
		if (sourceRet != 0)
			return sourceRet;

		cv::Mat out;
		int ret = pseudoColorAutoRangeTo(source, out, min, max, types, dataMin, dataMax);
		if (ret != 0)
			return ret;

		return MatToHImage(out, outImage);
		});
}

COLORVISIONCORE_API int M_PseudoColorInto(HImage img, HImage outImage, uint min, uint max, cv::ColormapTypes types, int channel)
{
	return GuardIntExport([&]() -> int {
		cv::Mat mat = CreateMatView(img);
		cv::Mat output = CreateMatView(outImage);

		if (mat.empty())
			return -1;

		if (!IsPseudoColorOutputCompatible(mat, output))
			return -2;

		cv::Mat temp;
		cv::Mat source;
		int sourceRet = SelectSingleChannelSource(mat, channel, temp, source);
		if (sourceRet != 0)
			return sourceRet;

		return pseudoColorTo(source, output, min, max, types);
		});
}

COLORVISIONCORE_API int M_PseudoColorAutoRangeInto(HImage img, HImage outImage, uint min, uint max, cv::ColormapTypes types, int channel, uint dataMin, uint dataMax)
{
	return GuardIntExport([&]() -> int {
		cv::Mat mat = CreateMatView(img);
		cv::Mat output = CreateMatView(outImage);

		if (mat.empty())
			return -1;

		if (!IsPseudoColorOutputCompatible(mat, output))
			return -2;

		cv::Mat temp;
		cv::Mat source;
		int sourceRet = SelectSingleChannelSource(mat, channel, temp, source);
		if (sourceRet != 0)
			return sourceRet;

		return pseudoColorAutoRangeTo(source, output, min, max, types, dataMin, dataMax);
		});
}

COLORVISIONCORE_API int M_GetMinMax(HImage img, uint* outMin, uint* outMax, int channel)
{
	return GuardIntExport([&]() -> int {
		if (outMin != nullptr) {
			*outMin = 0;
		}
		if (outMax != nullptr) {
			*outMax = 0;
		}

		if (outMin == nullptr || outMax == nullptr) {
			return ExportInvalidArgument;
		}

		cv::Mat mat = CreateMatView(img);

		if (mat.empty())
			return -1;

		cv::Mat temp;
		cv::Mat gray;
		int sourceRet = SelectSingleChannelSource(mat, channel, temp, gray);
		if (sourceRet != 0)
			return sourceRet;

		double minVal, maxVal;
		cv::minMaxLoc(gray, &minVal, &maxVal);

		*outMin = (uint)std::max(minVal, 0.0);
		*outMax = (uint)std::max(maxVal, 0.0);

		return 0;
		});
}

COLORVISIONCORE_API int M_AutoLevelsAdjust(HImage img, HImage* outImage)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);

		if (mat.empty())
			return -1;
		cv::Mat out;
		if (!TryBuildBgr8Image(mat, out)) {
			return ExportInvalidArgument;
		}
		cv::Mat outMat;
		autoLevelsAdjust(out, outMat);
		return MatToHImage(outMat, outImage);
		});
}

COLORVISIONCORE_API int M_AutomaticColorAdjustment(HImage img, HImage* outImage)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		cv::Mat out;
		if (!TryBuildBgr8Image(mat, out)) {
			return ExportInvalidArgument;
		}
		automaticColorAdjustment(out);
		return MatToHImage(out, outImage);
		});
}

COLORVISIONCORE_API int M_AutomaticToneAdjustment(HImage img, HImage* outImage)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		cv::Mat out;
		if (!TryBuildBgr8Image(mat, out)) {
			return ExportInvalidArgument;
		}
		automaticToneAdjustment(out, 1);
		return MatToHImage(out, outImage);
		});
}

COLORVISIONCORE_API int M_DrawPoiImage(HImage img, HImage* outImage,int radius, int* point , int pointCount, int thickness)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		if (radius <= 0 || thickness < -1 || pointCount < 0 || (pointCount % 2) != 0) {
			return ExportInvalidArgument;
		}
		if (pointCount > 0 && point == nullptr) {
			return ExportInvalidArgument;
		}
		if (mat.channels() != 3) {
			if (mat.channels() == 1) {
				// ����ͨ��ͼ��ת��Ϊ��ͨ��
				cv::cvtColor(mat, mat, cv::COLOR_GRAY2BGR);
			}
			else {
				return ExportInvalidArgument;
			}
		}

		cv::Mat out = mat.clone();
		const int drawRet = drawPoiImage(out, out, radius, point, pointCount, thickness);
		if (drawRet != 0) {
			return ExportAlgorithmFailed;
		}

		const int convertCode = MatToHImage(out, outImage);
		if (convertCode != 0) {
			return ExportAllocationFailed;
		}
		return 0;
		});
}





int FindClosestFactor(int value, const int* allowedFactors, int size = 13)
{
	int closestFactor = allowedFactors[0];
	for (int i = 1; i < size; ++i)
	{
		if (std::abs(value - allowedFactors[i]) < std::abs(value - closestFactor))
		{
			closestFactor = allowedFactors[i];
		}
	}
	return closestFactor;
}

COLORVISIONCORE_API int M_ConvertImage(HImage img, uchar** rowGrayPixels, int* length, int* scaleFactout,int targetPixelsX, int targetPixelsY)
{
	return GuardIntExport([&]() -> int {
		if (rowGrayPixels != nullptr) {
			*rowGrayPixels = nullptr;
		}
		if (length != nullptr) {
			*length = 0;
		}
		if (scaleFactout != nullptr) {
			*scaleFactout = 0;
		}

		if (rowGrayPixels == nullptr || length == nullptr || scaleFactout == nullptr ||
			targetPixelsX <= 0 || targetPixelsY <= 0) {
			return ExportInvalidArgument;
		}

		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;

		if (mat.channels() == 4) {
			cv::cvtColor(mat, mat, cv::COLOR_BGRA2GRAY);
		}
		else if (mat.channels() == 3) {
			cv::cvtColor(mat, mat, cv::COLOR_BGR2GRAY);
		}
		else if (mat.channels() != 1) {
			return ExportInvalidArgument;
		}

		if (mat.depth() != CV_8U) {
			cv::normalize(mat, mat, 0, 255, cv::NORM_MINMAX, CV_8U);
		}

		const long long targetPixels = static_cast<long long>(targetPixelsX) * targetPixelsY;
		const int originalWidth = mat.cols;
		const int originalHeight = mat.rows;
		if (targetPixels <= 0 || originalWidth <= 0 || originalHeight <= 0) {
			return ExportInvalidArgument;
		}

		double initialScaleFactor = std::sqrt(static_cast<double>(originalWidth) * originalHeight / targetPixels);

		int allowedFactors[] = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048 };
		int scaleFactor = FindClosestFactor(static_cast<int>(std::round(initialScaleFactor)), allowedFactors);
		if (scaleFactor <= 0) {
			scaleFactor = 1;
		}

		const int newWidth = std::max(originalWidth / scaleFactor, 1);
		const int newHeight = std::max(originalHeight / scaleFactor, 1);
		const long long outputLength = static_cast<long long>(newWidth) * newHeight;
		if (outputLength <= 0 || outputLength > std::numeric_limits<int>::max()) {
			return ExportAllocationFailed;
		}

		CoTaskMemBuffer buffer(static_cast<uchar*>(CoTaskMemAlloc(static_cast<SIZE_T>(outputLength))));
		if (buffer == nullptr) {
			return ExportAllocationFailed;
		}
		uchar* const outputBuffer = buffer.get();

#pragma omp parallel for
		for (int y = 0; y < newHeight; ++y)
		{
			uchar* row = outputBuffer + static_cast<size_t>(y) * newWidth;
			for (int x = 0; x < newWidth; ++x)
			{
				const int oldX = std::min(x * scaleFactor, originalWidth - 1);
				const int oldY = std::min(y * scaleFactor, originalHeight - 1);
				row[x] = mat.at<uchar>(oldY, oldX);
			}
		}

		*length = static_cast<int>(outputLength);
		*scaleFactout = scaleFactor;
		*rowGrayPixels = buffer.release();
		return 0;
		});
}

COLORVISIONCORE_API int M_ExtractChannel(HImage img, HImage* outImage, int channel)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		if (channel < 0 || channel >= mat.channels()) {
			return ExportInvalidArgument;
		}
		cv::Mat outMat;
		cv::extractChannel(mat, outMat, channel);
		return MatToHImage(outMat, outImage);
		});
}


COLORVISIONCORE_API int M_GetWhiteBalance(HImage img, HImage* outImage, double redBalance, double greenBalance, double blueBalance)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		if (mat.channels() != 3
			|| !std::isfinite(redBalance)
			|| !std::isfinite(greenBalance)
			|| !std::isfinite(blueBalance)) {
			return ExportInvalidArgument;
		}
		cv::Mat dst;

		AdjustWhiteBalance(mat,dst, redBalance, greenBalance, blueBalance);

		return MatToHImage(dst, outImage);
		});
}

COLORVISIONCORE_API int M_ApplyGammaCorrection(HImage img, HImage* outImage, double gamma)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		if (!std::isfinite(gamma) || gamma <= 0.0) {
			return ExportInvalidArgument;
		}
		cv::Mat dst;

		ApplyGammaCorrection(mat, dst, gamma);

		return MatToHImage(dst, outImage);
		});
}

COLORVISIONCORE_API int M_AdjustBrightnessContrast(HImage img, HImage* outImage, double alpha, double beta)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;

		cv::Mat dst;
		AdjustBrightnessContrast(mat, dst, alpha, beta);

		return MatToHImage(dst, outImage);
		});
}

/// <summary>
/// ����
/// </summary>
/// <param name="img"></param>
/// <param name="outImage"></param>
/// <returns></returns>
COLORVISIONCORE_API int M_InvertImage(HImage img, HImage* outImage)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;

		cv::Mat dst;
		cv::bitwise_not(mat, dst);

		return MatToHImage(dst, outImage);
		});
}

/// <summary>
/// 
/// </summary>
/// <param name="img"></param>
/// <param name="outImage"></param>
/// <returns></returns>
COLORVISIONCORE_API int M_Threshold(HImage img, HImage* outImage, double thresh, double maxval, int type)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;

		cv::Mat dst;
		cv::threshold(mat, dst, thresh, maxval, type);

		return MatToHImage(dst, outImage);
		});
}

COLORVISIONCORE_API int M_FindLuminousArea(HImage img, RoiRect roi, const char* config, char** result)
{
	return GuardIntExport([&]() -> int {
		if (result != nullptr) {
			*result = nullptr;
		}

		cv::Mat mat = CreateMatView(img);
		if (mat.empty() || config == nullptr || result == nullptr) {
			return ExportInvalidArgument;
		}

		cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
		bool use_roi = (mroi.width > 0 && mroi.height > 0 && (mroi & cv::Rect(0, 0, mat.cols, mat.rows)) == mroi);
		mat = use_roi ? mat(mroi) : mat;

		json j;
		if (!TryParseJson(config, j)) {
			return ExportInvalidJson;
		}
		int threshold = -1;
		if (j.contains("Threshold")) {
			threshold = j.at("Threshold").get<int>();
		}
		bool useRotatedRect = false;
		if (j.contains("UseRotatedRect")) {
			useRotatedRect = j.at("UseRotatedRect").get<bool>();
		}

		json outputJson;
		int ret = 0;

		if (useRotatedRect) {
			std::vector<cv::Point2f> corners;
			ret = findLuminousAreaCorners(mat, corners, threshold);
			if (ret == 0 && corners.size() == 4) {
				outputJson["Corners"] = {
					{corners[0].x, corners[0].y},
					{corners[1].x, corners[1].y},
					{corners[2].x, corners[2].y},
					{corners[3].x, corners[3].y}
				};
			}
			else {
				return ExportAlgorithmFailed;
			}
		}
		else {
			cv::Rect LuminousArea;
			ret = findLuminousArea(mat, LuminousArea, threshold);
			if (ret == 0) {
				outputJson["X"] = LuminousArea.x;
				outputJson["Y"] = LuminousArea.y;
				outputJson["Width"] = LuminousArea.width;
				outputJson["Height"] = LuminousArea.height;
			}
			else {
				return ExportAlgorithmFailed;
			}
		}

		return CopyJsonResult(outputJson, result);
	});
}

COLORVISIONCORE_API int M_FindLightBeads(HImage img, RoiRect roi, const char* config, char** result)
{
	return GuardIntExport([&]() -> int {
		if (result != nullptr) {
			*result = nullptr;
		}

		cv::Mat mat = CreateMatView(img);
		if (mat.empty() || config == nullptr || result == nullptr) {
			return ExportInvalidArgument;
		}

		// Validate and apply ROI
		cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
		cv::Rect imageRect(0, 0, mat.cols, mat.rows);
		bool hasValidRoi = (mroi.width > 0 && mroi.height > 0);
		bool roiWithinBounds = hasValidRoi && ((mroi & imageRect) == mroi);
		bool use_roi = hasValidRoi && roiWithinBounds;

		mat = use_roi ? mat(mroi) : mat;

		// 解析 JSON 配置
		json j;
		if (!TryParseJson(config, j)) {
			return ExportInvalidJson;
		}
		int threshold = j.value("Threshold", 20);
		int minSize = j.value("MinSize", 2);
		int maxSize = j.value("MaxSize", 20);
		int rows = j.value("Rows", 650);
		int cols = j.value("Cols", 850);

		std::vector<cv::Point> centers;
		std::vector<cv::Point> blackCenters;

		int ret = findLightBeads(mat, centers, blackCenters, threshold, minSize, maxSize, rows, cols);
		if (ret != 0) {
			return ExportAlgorithmFailed;
		}

		// 构建 JSON 输出
		json outputJson;

		outputJson["Centers"] = nlohmann::json::array();
		for (const auto& center : centers) {
			outputJson["Centers"].push_back({ center.x, center.y });
		}
		outputJson["CenterCount"] = centers.size();

		outputJson["BlackCenters"] = nlohmann::json::array();
		for (const auto& blackCenter : blackCenters) {
			outputJson["BlackCenters"].push_back({ blackCenter.x, blackCenter.y });
		}
		outputJson["BlackCenterCount"] = blackCenters.size();

		// 预期数量 (使用 size_t 避免整数溢出)
		size_t expectedCount = static_cast<size_t>(rows) * static_cast<size_t>(cols);
		size_t actualCount = centers.size();
		size_t missingCount = (expectedCount > actualCount) ? (expectedCount - actualCount) : 0;

		outputJson["ExpectedCount"] = expectedCount;
		outputJson["MissingCount"] = missingCount;

		return CopyJsonResult(outputJson, result);
	});
}

COLORVISIONCORE_API int M_DetectKeyRegions(HImage img, RoiRect roi, const char* config, char** result)
{
	return GuardIntExport([&]() -> int {
		if (result != nullptr) {
			*result = nullptr;
		}

		cv::Mat mat = CreateMatView(img);
		if (mat.empty() || config == nullptr || result == nullptr) {
			return ExportInvalidArgument;
		}

		// 应用ROI
		cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
		bool use_roi = (mroi.width > 0 && mroi.height > 0 && (mroi & cv::Rect(0, 0, mat.cols, mat.rows)) == mroi);
		cv::Mat workMat = use_roi ? mat(mroi) : mat;

		// 解析JSON配置
		json j;
		if (!TryParseJson(config, j)) {
			return ExportInvalidJson;
		}
		int threshold = j.value("Threshold", -1);
		int minArea = j.value("MinArea", 500);
		int maxArea = j.value("MaxArea", 0);
		double marginRatio = j.value("MarginRatio", 0.05);

		std::vector<cv::Rect> keyRects;
		int ret = detectKeyRegions(workMat, keyRects, threshold, minArea, maxArea, marginRatio);
		if (ret != 0 || keyRects.empty()) {
			return ExportAlgorithmFailed;
		}

		// 构建JSON输出
		json outputJson;
		json rectsArray = json::array();
		for (const auto& r : keyRects) {
			json rectObj;
			rectObj["X"] = r.x + (use_roi ? roi.x : 0);
			rectObj["Y"] = r.y + (use_roi ? roi.y : 0);
			rectObj["Width"] = r.width;
			rectObj["Height"] = r.height;
			rectsArray.push_back(rectObj);
		}
		outputJson["KeyRegions"] = rectsArray;
		outputJson["Count"] = keyRects.size();

		return CopyJsonResult(outputJson, result);
	});
}

COLORVISIONCORE_API int M_DetectSurfaceDefects(HImage img, RoiRect roi, const char* config, char** result)
{
	return GuardIntExport([&]() -> int {
		if (result != nullptr) {
			*result = nullptr;
		}

		cv::Mat mat = CreateMatView(img);
		if (mat.empty() || result == nullptr) {
			return ExportInvalidArgument;
		}

		json j = json::object();
		if (config != nullptr && config[0] != '\0') {
			if (!TryParseJson(config, j)) {
				return ExportInvalidJson;
			}
		}

		cv::Point origin(0, 0);
		cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
		const cv::Rect imageRect(0, 0, mat.cols, mat.rows);
		const bool useRoi = (mroi.width > 0 && mroi.height > 0 && (mroi & imageRect) == mroi);
		if (useRoi) {
			mat = mat(mroi);
			origin = cv::Point(mroi.x, mroi.y);
		}

		cvcore::surface_defect::SurfaceDefectConfig defectConfig = ParseSurfaceDefectConfig(j);
		cvcore::surface_defect::SurfaceDefectResult calcResult =
			cvcore::surface_defect::detectSurfaceDefects(mat, defectConfig);

		json outputJson;
		outputJson["algorithm"] = "SurfaceDefect";
		outputJson["version"] = "0.1";
		outputJson["success"] = calcResult.success;
		outputJson["statusCode"] = calcResult.statusCode;
		outputJson["message"] = calcResult.message;
		outputJson["count"] = calcResult.defects.size();
		outputJson["image"] = {
			{ "width", img.cols },
			{ "height", img.rows },
			{ "roi", RectToJson(useRoi ? mroi : cv::Rect(0, 0, mat.cols, mat.rows), cv::Point(0, 0)) }
		};
		outputJson["configUsed"] = SurfaceDefectConfigToJson(defectConfig);
		outputJson["summary"] = {
			{ "defectCount", calcResult.summary.defectCount },
			{ "darkCount", calcResult.summary.darkCount },
			{ "brightCount", calcResult.summary.brightCount },
			{ "maxSeverity", calcResult.summary.maxSeverity },
			{ "meanSeverity", calcResult.summary.meanSeverity },
			{ "grade", calcResult.summary.grade }
		};
		outputJson["diagnostics"] = {
			{ "roiUsed", useRoi },
			{ "relativeResidual", true },
			{ "background", "gaussian" }
		};
		outputJson["defects"] = json::array();
		for (const auto& defect : calcResult.defects) {
			outputJson["defects"].push_back(SurfaceDefectToJson(defect, origin, defectConfig));
		}

		return CopyJsonResult(outputJson, result);
	});
}

COLORVISIONCORE_API int M_CalSFRBmw4In1(HImage img, RoiRect roi, const char* config, char** result)
{
	return GuardIntExport([&]() -> int {
		if (result != nullptr) {
			*result = nullptr;
		}

		cv::Mat mat = CreateMatView(img);
		if (mat.empty() || result == nullptr) {
			return ExportInvalidArgument;
		}

		json j = json::object();
		if (config != nullptr && config[0] != '\0') {
			if (!TryParseJson(config, j)) {
				return ExportInvalidJson;
			}
		}

		cv::Point origin(0, 0);
		cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
		const cv::Rect imageRect(0, 0, mat.cols, mat.rows);
		const bool useRoi = (mroi.width > 0 && mroi.height > 0 && (mroi & imageRect) == mroi);
		if (useRoi) {
			mat = mat(mroi);
			origin = cv::Point(mroi.x, mroi.y);
		}

		cvcore::sfr::BmwSfr4Config sfrConfig = ParseBmwSfr4Config(j);
		cvcore::sfr::BmwSfr4Result calcResult = cvcore::sfr::calculateBmwSfr4In1(mat, sfrConfig);
		if (calcResult.points.empty()) {
			return ExportAlgorithmFailed;
		}

		json outputJson;
		outputJson["count"] = calcResult.points.size();
		outputJson["result"] = json::array();

		for (const auto& point : calcResult.points) {
			json pointJson;
			pointJson["name"] = point.name;
			pointJson["center"] = PointToJson(point.center, origin);
			pointJson["angle"] = point.angleRadians;
			pointJson["targetRect"] = RectToJson(point.targetRect, origin);
			pointJson["data"] = json::array();

			for (const auto& curve : point.curves) {
				pointJson["data"].push_back(SfrCurveToJson(curve, origin, sfrConfig.maxCurveLength));
			}

			outputJson["result"].push_back(std::move(pointJson));
		}

		return CopyJsonResult(outputJson, result);
	});
}

COLORVISIONCORE_API int M_CalDistortionP9(HImage img, RoiRect roi, const char* config, char** result)
{
	return GuardIntExport([&]() -> int {
		if (result != nullptr) {
			*result = nullptr;
		}

		cv::Mat mat = CreateMatView(img);
		if (mat.empty() || result == nullptr) {
			return ExportInvalidArgument;
		}

		json j = json::object();
		if (config != nullptr && config[0] != '\0') {
			if (!TryParseJson(config, j)) {
				return ExportInvalidJson;
			}
		}

		cv::Point origin(0, 0);
		cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
		if (mroi.width <= 0 || mroi.height <= 0) {
			mroi = TryGetConfigMaskRect(j);
		}

		const cv::Rect imageRect(0, 0, mat.cols, mat.rows);
		const bool useRoi = (mroi.width > 0 && mroi.height > 0 && (mroi & imageRect) == mroi);
		if (useRoi) {
			mat = mat(mroi);
			origin = cv::Point(mroi.x, mroi.y);
		}

		cvcore::distortion::DistortionP9Config distortionConfig = ParseDistortionP9Config(j);
		cvcore::distortion::DistortionP9Result calcResult = cvcore::distortion::calculateDistortionP9(mat, distortionConfig);
		const int expectedCount = distortionConfig.expectedRows * distortionConfig.expectedCols;

		json outputJson;
		outputJson["algorithm"] = "DistortionP9";
		outputJson["version"] = "1.0";
		outputJson["success"] = calcResult.success;
		outputJson["statusCode"] = calcResult.statusCode;
		outputJson["message"] = calcResult.message;
		outputJson["count"] = calcResult.points.size();
		outputJson["selectedCount"] = calcResult.points.size();
		outputJson["expectedCount"] = expectedCount;
		outputJson["candidateCount"] = calcResult.candidateCount;
		outputJson["image"] = {
			{ "width", img.cols },
			{ "height", img.rows },
			{ "roi", RectToJson(useRoi ? mroi : cv::Rect(0, 0, mat.cols, mat.rows), cv::Point(0, 0)) }
		};
		outputJson["configUsed"] = DistortionP9ConfigToJson(distortionConfig);
		outputJson["metrics"] = calcResult.success ? DistortionP9MetricsToJson(calcResult.metrics) : json(nullptr);
		outputJson["warnings"] = StringArrayToJson(calcResult.warnings);
		outputJson["diagnostics"] = {
			{ "expectedPointCount", expectedCount },
			{ "candidateCount", calcResult.candidateCount },
			{ "missingCount", std::max(0, expectedCount - calcResult.candidateCount) },
			{ "extraCount", std::max(0, calcResult.candidateCount - expectedCount) },
			{ "roiUsed", useRoi },
			{ "canCalculateMetrics", calcResult.success }
		};
		outputJson["method"] = {
			{ "pointOrder", "TL,TC,TR,ML,C,MR,BL,BC,BR" },
			{ "tvFormula", "((edge1 + edge2) / 2 - center) / center * 100; TvCaclWay=1 halves the percentage" },
			{ "edgeFormula", "signed middle-point bow relative to the edge chord, normalized by average grid span" }
		};

		outputJson["points"] = json::array();
		for (const auto& point : calcResult.points) {
			outputJson["points"].push_back(DistortionP9PointToJson(point, origin));
		}

		outputJson["candidatePoints"] = json::array();
		for (const auto& point : calcResult.candidatePoints) {
			outputJson["candidatePoints"].push_back(DistortionP9PointToJson(point, origin));
		}

		outputJson["grid"] = json::array();
		for (int row = 0; row < distortionConfig.expectedRows; ++row) {
			json rowJson = json::array();
			for (int col = 0; col < distortionConfig.expectedCols; ++col) {
				const int id = row * distortionConfig.expectedCols + col;
				rowJson.push_back(id);
			}
			outputJson["grid"].push_back(std::move(rowJson));
		}

		return CopyJsonResult(outputJson, result);
	});
}


StitchingErrorCode stitchImages(const std::vector<std::string>& image_files, cv::Mat& result) {
	if (image_files.empty()) {
		return StitchingErrorCode::EMPTY_INPUT;
	}

	std::vector<cv::Mat> images;

	for (const auto& file : image_files) {
		std::string ss = UTF8ToGB(file.c_str());
		cv::Mat img = cv::imread(ss, cv::IMREAD_UNCHANGED);
		if (img.empty()) {
			return StitchingErrorCode::FILE_NOT_FOUND;
		}

		if (images.empty()) {
			// ��ȡ��һ��ͼ���Ի�ȡ�ο��ߴ������
			int ref_height = img.rows;
			int ref_width = img.cols;
			int ref_type = img.type(); // ��ȡͼ�����ͣ����� CV_8UC1 ��ʾ�Ҷ�ͼ��

			// ������ͼ��ĳߴ�������Ƿ����һ��ͼ����ͬ
			for (size_t i = 1; i < image_files.size(); ++i) {
				std::string ss = UTF8ToGB(image_files[i].c_str());
				cv::Mat img = cv::imread(ss, cv::IMREAD_UNCHANGED);
				if (img.empty() || img.rows != ref_height || img.cols != ref_width || img.type() != ref_type) {
					return StitchingErrorCode::DIFFERENT_DIMENSIONS;
				}
			}
		}

		images.push_back(img);
	}

	size_t num_images = images.size();
	if (num_images == 0) {
		return StitchingErrorCode::NO_VALID_IMAGES;
	}

	// ʹ�����һ��ͼ����Ϊ��ͼ
	cv::Mat last_image = images.back();
	int result_height = last_image.rows;
	int result_width = last_image.cols;

	result.create(result_height, result_width, last_image.type());

	if (result.empty()) {
		return StitchingErrorCode::NO_VALID_IMAGES;
	}
	last_image.copyTo(result);

	size_t width = result_width / num_images;
	for (int i = 0; i < num_images -1; ++i) {
		cv::Mat part = images[i](cv::Rect(i* (int)width, 0, (int)width, result_height));
		part.copyTo(result(cv::Rect(i * (int)width, 0, (int)width, result_height)));
	}

	return StitchingErrorCode::SUCCESS;
}

bool TryGbkToUtf8(const char* srcStr, std::string& output)
{
	output.clear();
	if (srcStr == nullptr || srcStr[0] == '\0') {
		return false;
	}

	const int wideLen = MultiByteToWideChar(CP_ACP, 0, srcStr, -1, nullptr, 0);
	if (wideLen <= 0) {
		return false;
	}

	std::vector<wchar_t> wideBuffer(static_cast<size_t>(wideLen));
	if (MultiByteToWideChar(CP_ACP, 0, srcStr, -1, wideBuffer.data(), wideLen) == 0) {
		return false;
	}

	const int utf8Len = WideCharToMultiByte(CP_UTF8, 0, wideBuffer.data(), -1, nullptr, 0, nullptr, nullptr);
	if (utf8Len <= 0) {
		return false;
	}

	std::vector<char> utf8Buffer(static_cast<size_t>(utf8Len));
	if (WideCharToMultiByte(CP_UTF8, 0, wideBuffer.data(), -1, utf8Buffer.data(), utf8Len, nullptr, nullptr) == 0) {
		return false;
	}

	output.assign(utf8Buffer.data());
	return true;
}

COLORVISIONCORE_API int M_StitchImages(const char* config, HImage* outImage)
{
	return GuardHImageExport(outImage, [&]() -> int {
		if (config == nullptr) {
			return ExportInvalidArgument;
		}

		std::string utf8Config;
		if (!TryGbkToUtf8(config, utf8Config)) {
			return ExportInvalidArgument;
		}

		json j;
		if (!TryParseJson(utf8Config, j)) {
			return ExportInvalidJson;
		}

		if (!j.contains("ImageFiles") || !j["ImageFiles"].is_array()) {
			return ExportInvalidJson;
		}

		const auto image_files = j.at("ImageFiles").get<std::vector<std::string>>();
		if (image_files.empty()) {
			return ExportInvalidArgument;
		}
		cv::Mat result;

		StitchingErrorCode code = stitchImages(image_files, result);

		if (code != StitchingErrorCode::SUCCESS) {
			return static_cast<int>(code);
		}

		if (result.empty()) {
			return ExportAlgorithmFailed;
		}

		const int convertCode = MatToHImage(result, outImage);
		if (convertCode != 0) {
			return ExportAllocationFailed;
		}
		return 0;
	});
}



COLORVISIONCORE_API int M_ConvertGray32Float(HImage img, HImage* outImage)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);

		if (mat.empty() || mat.type() != CV_32FC1) {
			return -1;
		}

		double minVal, maxVal;
		cv::minMaxLoc(mat, &minVal, &maxVal);

		cv::Mat outMat(img.rows, img.cols, CV_16UC1);

		if (minVal >= 0.0 && maxVal <= 5.0) {
			mat.convertTo(outMat, CV_16UC1, 65535);
		}
		else {
			if (maxVal <= minVal) {
				return ExportInvalidArgument;
			}

			float scale = 65535 / static_cast<float>(maxVal - minVal);
			float delta = static_cast<float>(-minVal) * scale;

			mat.convertTo(outMat, CV_16UC1, scale, delta);
		}

		return MatToHImage(outMat, outImage);
		});
}

COLORVISIONCORE_API int M_CvtColor(HImage img, HImage* outImage, double thresh, double maxval, int type)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;

		cv::Mat dst;
		cv::cvtColor(mat, dst, cv::COLOR_RGBA2GRAY);

		return MatToHImage(dst, outImage);
		});
}
COLORVISIONCORE_API int M_RemoveMoire(HImage img, HImage* outImage)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		cv::Mat dst = removeMoire(mat);
		return MatToHImage(dst, outImage);
		});
}

COLORVISIONCORE_API int M_ApplyGaussianBlur(HImage img, HImage* outImage, int kernelSize, double sigma)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		cv::Mat dst;
		ApplyGaussianBlur(mat, dst, kernelSize, sigma);
		return MatToHImage(dst, outImage);
		});
}

COLORVISIONCORE_API int M_ApplyMedianBlur(HImage img, HImage* outImage, int kernelSize)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		cv::Mat dst;
		ApplyMedianBlur(mat, dst, kernelSize);
		return MatToHImage(dst, outImage);
		});
}

COLORVISIONCORE_API int M_ApplySharpen(HImage img, HImage* outImage)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		cv::Mat dst;
		ApplySharpen(mat, dst);
		return MatToHImage(dst, outImage);
		});
}

COLORVISIONCORE_API int M_ApplyCannyEdgeDetection(HImage img, HImage* outImage, double threshold1, double threshold2)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		cv::Mat dst;
		ApplyCannyEdgeDetection(mat, dst, threshold1, threshold2);
		return MatToHImage(dst, outImage);
		});
}

COLORVISIONCORE_API int M_ApplyHistogramEqualization(HImage img, HImage* outImage)
{
	return GuardHImageExport(outImage, [&]() -> int {
		cv::Mat mat = CreateMatView(img);
		if (mat.empty())
			return -1;
		cv::Mat dst;
		ApplyHistogramEqualization(mat, dst);
		return MatToHImage(dst, outImage);
		});
}

COLORVISIONCORE_API int M_Fusion(const char* fusionjson, HImage* outImage)
{
	return GuardHImageExport(outImage, [&]() -> int {
		if (fusionjson == nullptr) {
			return ExportInvalidArgument;
		}

		json j;
		if (!TryParseJson(fusionjson, j)) {
			return ExportInvalidJson;
		}

		// ��� JSON �����Ƿ�������
		if (!j.is_array()) {
			return ExportInvalidJson;
		}

		std::vector<std::string> files = j.get<std::vector<std::string>>();
		if (files.empty()) {
			std::cerr << "Error: No files provided in JSON array." << std::endl;
			return ExportInvalidArgument;
		}

		std::vector<cv::Mat> imgs;
		const int loadCode = LoadImagesParallel(files, imgs);
		if (loadCode != 0) {
			return loadCode;
		}

		cv::Mat out = fusion(imgs, 2);
		if (out.empty()) {
			return ExportAlgorithmFailed;
		}

		const int convertCode = MatToHImage(out, outImage);
		if (convertCode != 0) {
			return ExportAllocationFailed;
		}

		return 0;
	});
}
