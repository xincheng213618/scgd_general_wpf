#include "grid_distortion_v2.h"

#include <opencv2/calib3d.hpp>
#include <opencv2/imgproc.hpp>
#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <limits>
#include <numeric>
#include <vector>

namespace cvcore::distortion {
namespace {
using json = nlohmann::json;
using Clock = std::chrono::steady_clock;

struct Options {
    int rows = 3;
    int cols = 3;
    bool bright = true;
    int maxSize = 1600;
    int tvCalcWay = 0;
    double minimumContrast = 0.02;
    double maxResidual = 0.3;
};

struct Candidate {
    cv::Point2f center;
    cv::Rect bounds;
    double area = 0.0;
};

double elapsed(Clock::time_point start)
{
    return std::chrono::duration<double, std::milli>(Clock::now() - start).count();
}

double median(std::vector<double> values)
{
    if (values.empty()) return 0.0;
    const size_t mid = values.size() / 2;
    std::nth_element(values.begin(), values.begin() + mid, values.end());
    return values[mid];
}

cv::Mat grayFloat(const cv::Mat& image)
{
    cv::Mat converted, gray;
    image.convertTo(converted, CV_32F);
    if (converted.channels() == 1) return converted;
    cv::cvtColor(converted, gray, converted.channels() == 3 ? cv::COLOR_BGR2GRAY : cv::COLOR_BGRA2GRAY);
    return gray;
}

std::vector<Candidate> detectCandidates(const cv::Mat& gray8, double threshold, const Options& options)
{
    cv::Mat mask;
    cv::threshold(gray8, mask, threshold, 255, cv::THRESH_BINARY);
    std::vector<std::vector<cv::Point>> contours;
    cv::findContours(mask, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);
    std::vector<Candidate> candidates;
    const double maxWidth = gray8.cols * 0.75 / std::max(2, options.cols - 1);
    const double maxHeight = gray8.rows * 0.75 / std::max(2, options.rows - 1);
    for (const auto& contour : contours) {
        const cv::Rect bounds = cv::boundingRect(contour);
        if (bounds.width < 3 || bounds.height < 3 || bounds.width > maxWidth || bounds.height > maxHeight) continue;
        if (bounds.x <= 0 || bounds.y <= 0 || bounds.br().x >= gray8.cols || bounds.br().y >= gray8.rows) continue;
        const double area = cv::contourArea(contour);
        const double perimeter = cv::arcLength(contour, true);
        const double extent = area / (bounds.width * bounds.height);
        const double aspect = static_cast<double>(std::min(bounds.width, bounds.height)) / std::max(bounds.width, bounds.height);
        const double circularity = perimeter > 0 ? 4.0 * CV_PI * area / (perimeter * perimeter) : 0.0;
        // Filled rectangular reflections must not displace a dim circular point.
        if (area < 5 || extent < 0.30 || extent > 0.92 || aspect < 0.35 || circularity < 0.45) continue;
        const cv::Moments moments = cv::moments(contour);
        if (moments.m00 <= 0) continue;
        candidates.push_back({ cv::Point2f(static_cast<float>(moments.m10 / moments.m00), static_cast<float>(moments.m01 / moments.m00)), bounds, area });
    }
    return candidates;
}

void orientGrid(std::vector<cv::Point2f>& grid, int rows, int cols)
{
    // Symmetric circle grids do not encode orientation. Choose image-right columns
    // and image-down rows; transpose a square only when its row direction is vertical.
    const cv::Point2f rowDirection = grid[cols - 1] - grid[0];
    if (rows == cols && std::abs(rowDirection.y) > std::abs(rowDirection.x)) {
        auto copy = grid;
        for (int r = 0; r < rows; ++r) for (int c = 0; c < cols; ++c) grid[r * cols + c] = copy[c * cols + r];
    }
    double leftX = 0, rightX = 0, topY = 0, bottomY = 0;
    for (int r = 0; r < rows; ++r) { leftX += grid[r * cols].x; rightX += grid[r * cols + cols - 1].x; }
    if (leftX > rightX) for (int r = 0; r < rows; ++r) std::reverse(grid.begin() + r * cols, grid.begin() + (r + 1) * cols);
    for (int c = 0; c < cols; ++c) { topY += grid[c].y; bottomY += grid[(rows - 1) * cols + c].y; }
    if (topY > bottomY) {
        auto copy = grid;
        for (int r = 0; r < rows; ++r) for (int c = 0; c < cols; ++c) grid[r * cols + c] = copy[(rows - 1 - r) * cols + c];
    }
}

bool validateGrid(const std::vector<cv::Point2f>& points, const Options& options, double& residual, cv::Mat& homography)
{
    if (points.size() != static_cast<size_t>(options.rows * options.cols)) return false;
    std::vector<cv::Point2f> ideal;
    std::vector<double> spacings;
    for (int r = 0; r < options.rows; ++r) {
        for (int c = 0; c < options.cols; ++c) {
            const int id = r * options.cols + c;
            ideal.emplace_back(static_cast<float>(c), static_cast<float>(r));
            if (c + 1 < options.cols) spacings.push_back(cv::norm(points[id + 1] - points[id]));
            if (r + 1 < options.rows) spacings.push_back(cv::norm(points[id + options.cols] - points[id]));
            if (c + 1 < options.cols && r + 1 < options.rows) {
                const cv::Point2f a = points[id + 1] - points[id];
                const cv::Point2f b = points[id + options.cols] - points[id];
                if (a.x * b.y - a.y * b.x <= 0) return false;
            }
        }
    }
    const double spacing = median(spacings);
    if (spacing < 3 || *std::min_element(spacings.begin(), spacings.end()) < spacing * 0.20) return false;
    homography = cv::findHomography(ideal, points, 0);
    if (homography.empty() || !cv::checkRange(homography)) return false;
    std::vector<cv::Point2f> predicted;
    cv::perspectiveTransform(ideal, predicted, homography);
    residual = 0;
    for (size_t i = 0; i < points.size(); ++i) residual = std::max(residual, cv::norm(points[i] - predicted[i]) / spacing);
    return std::isfinite(residual) && residual <= options.maxResidual;
}

bool orderCandidates(const std::vector<Candidate>& candidates, const Options& options,
    std::vector<cv::Point2f>& ordered, double& residual, bool& ambiguous)
{
    const int expected = options.rows * options.cols;
    if (candidates.size() < static_cast<size_t>(expected)) return false;
    std::vector<double> areas;
    for (const auto& candidate : candidates) areas.push_back(candidate.area);
    std::vector<cv::Point2f> points;
    for (const auto& candidate : candidates) {
        points.push_back(candidate.center);
    }
    cv::Mat homography;
    auto tryOrder = [&](const std::vector<cv::Point2f>& input) {
        if (input.size() < static_cast<size_t>(expected) || input.size() > static_cast<size_t>(expected * 4 + 64)) return false;
        // With a complete candidate set, first test image axes and PCA axes.
        // This preserves genuinely bent grids that a perspective clustering
        // model can miss. Geometry is validated; row sorting alone never accepts.
        if (input.size() == static_cast<size_t>(expected)) {
            for (int usePca = 0; usePca < 2; ++usePca) {
                cv::Point2f horizontal(1, 0), vertical(0, 1);
                if (usePca) {
                    cv::Mat coordinates(expected, 2, CV_32F);
                    for (int i = 0; i < expected; ++i) { coordinates.at<float>(i, 0) = input[i].x; coordinates.at<float>(i, 1) = input[i].y; }
                    cv::PCA pca(coordinates, cv::Mat(), cv::PCA::DATA_AS_ROW);
                    const cv::Point2f a(pca.eigenvectors.at<float>(0, 0), pca.eigenvectors.at<float>(0, 1));
                    const cv::Point2f b(pca.eigenvectors.at<float>(1, 0), pca.eigenvectors.at<float>(1, 1));
                    horizontal = std::abs(a.x) >= std::abs(b.x) ? a : b;
                    vertical = std::abs(a.x) >= std::abs(b.x) ? b : a;
                }
                if (horizontal.x < 0) horizontal *= -1;
                if (vertical.y < 0) vertical *= -1;
                ordered = input;
                std::sort(ordered.begin(), ordered.end(), [&](const auto& a, const auto& b) { return a.dot(vertical) < b.dot(vertical); });
                for (int r = 0; r < options.rows; ++r) std::sort(ordered.begin() + r * options.cols, ordered.begin() + (r + 1) * options.cols,
                    [&](const auto& a, const auto& b) { return a.dot(horizontal) < b.dot(horizontal); });
                orientGrid(ordered, options.rows, options.cols);
                if (validateGrid(ordered, options, residual, homography)) return true;
            }
        }
        const bool found = cv::findCirclesGrid(input, cv::Size(options.cols, options.rows), ordered,
            cv::CALIB_CB_SYMMETRIC_GRID | cv::CALIB_CB_CLUSTERING, nullptr);
        if (!found || ordered.size() != static_cast<size_t>(expected)) return false;
        orientGrid(ordered, options.rows, options.cols);
        return validateGrid(ordered, options, residual, homography);
    };
    // Local contrast normalization can expose many tiny noise components. Search
    // coherent size populations, each of which must form a complete valid grid.
    // This is a scale hypothesis, never selection of the largest N components:
    // a large reflection alone cannot meet either the count or topology checks.
    bool found = false;
    std::sort(areas.begin(), areas.end(), std::greater<double>());
    double previousScale = std::numeric_limits<double>::infinity();
    for (const double scale : areas) {
        if (scale > previousScale * 0.5) continue;
        previousScale = scale;
        std::vector<cv::Point2f> comparable;
        for (const auto& candidate : candidates) if (candidate.area >= scale * 0.25 && candidate.area <= scale * 4.0) comparable.push_back(candidate.center);
        if (comparable.size() == points.size()) continue;
        if (tryOrder(comparable)) { found = true; break; }
    }
    if (!found) found = tryOrder(points);
    if (!found) return false;
    if (candidates.size() > ordered.size()) {
        std::vector<double> selectedAreas;
        for (const auto& p : ordered) {
            const auto selected = std::min_element(candidates.begin(), candidates.end(), [&](const auto& a, const auto& b) { return cv::norm(a.center - p) < cv::norm(b.center - p); });
            selectedAreas.push_back(selected->area);
        }
        const double typicalArea = median(selectedAreas);
        std::vector<cv::Point2f> lattice;
        cv::perspectiveTransform(points, lattice, homography.inv());
        for (size_t i = 0; i < points.size(); ++i) {
            if (candidates[i].area < typicalArea * 0.125 || candidates[i].area > typicalArea * 4.0) continue;
            bool selected = false;
            for (const auto& p : ordered) if (cv::norm(p - points[i]) < 0.1) { selected = true; break; }
            if (selected) continue;
            const cv::Point2f p = lattice[i];
            if (std::isfinite(p.x) && std::isfinite(p.y)
                && std::abs(p.x - std::round(p.x)) < 0.20 && std::abs(p.y - std::round(p.y)) < 0.20) {
                ambiguous = true;
                return false;
            }
        }
    }
    return true;
}

bool refinePoint(const cv::Mat& source, const Candidate& candidate, double sx, double sy,
    const Options& options, cv::Point2f& center, double& area, double& contrast)
{
    const double margin = std::max(3.0, std::max(candidate.bounds.width / sx, candidate.bounds.height / sy) * 0.40);
    const int x0 = std::max(0, static_cast<int>(std::floor(candidate.bounds.x / sx - margin)));
    const int y0 = std::max(0, static_cast<int>(std::floor(candidate.bounds.y / sy - margin)));
    const int x1 = std::min(source.cols, static_cast<int>(std::ceil(candidate.bounds.br().x / sx + margin)));
    const int y1 = std::min(source.rows, static_cast<int>(std::ceil(candidate.bounds.br().y / sy + margin)));
    if (x1 - x0 < 3 || y1 - y0 < 3) return false;
    cv::Mat patch = grayFloat(source(cv::Rect(x0, y0, x1 - x0, y1 - y0)));
    if (!cv::checkRange(patch)) return false;
    std::vector<cv::Point3d> border;
    for (int x = 0; x < patch.cols; ++x) { border.emplace_back(x, 0, patch.at<float>(0, x)); border.emplace_back(x, patch.rows - 1, patch.at<float>(patch.rows - 1, x)); }
    for (int y = 1; y + 1 < patch.rows; ++y) { border.emplace_back(0, y, patch.at<float>(y, 0)); border.emplace_back(patch.cols - 1, y, patch.at<float>(y, patch.cols - 1)); }
    // Fit a local background plane using the surrounding ring, then trim large
    // residuals once. A gradient must not pull the measured centroid toward leakage.
    cv::Vec3d plane(0, 0, 0);
    std::vector<double> errors(border.size(), 0.0);
    double trim = std::numeric_limits<double>::infinity();
    for (int iteration = 0; iteration < 2; ++iteration) {
        cv::Matx33d normal = cv::Matx33d::zeros();
        cv::Vec3d rhs(0, 0, 0);
        for (size_t i = 0; i < border.size(); ++i) {
            if (errors[i] > trim) continue;
            const auto& p = border[i];
            const cv::Vec3d row(p.x, p.y, 1);
            normal += row * row.t(); rhs += row * p.z;
        }
        if (!cv::solve(normal, rhs, plane, cv::DECOMP_SVD)) return false;
        for (size_t i = 0; i < border.size(); ++i) errors[i] = std::abs(border[i].z - plane.dot(cv::Vec3d(border[i].x, border[i].y, 1)));
        trim = std::max(1e-6, median(errors) * 3.0);
    }
    cv::Mat excess(patch.size(), CV_32F);
    std::vector<double> intensities;
    intensities.reserve(patch.total());
    for (int y = 0; y < patch.rows; ++y) for (int x = 0; x < patch.cols; ++x) {
        const double value = (options.bright ? 1 : -1) * (patch.at<float>(y, x) - plane.dot(cv::Vec3d(x, y, 1)));
        excess.at<float>(y, x) = static_cast<float>(value); intensities.push_back(value);
    }
    const size_t peakIndex = static_cast<size_t>(intensities.size() * 0.995);
    std::nth_element(intensities.begin(), intensities.begin() + peakIndex, intensities.end());
    const double amplitude = intensities[peakIndex];
    const double background = plane.dot(cv::Vec3d((candidate.center.x + 0.5) / sx - 0.5 - x0, (candidate.center.y + 0.5) / sy - 0.5 - y0, 1));
    const double peak = background + (options.bright ? amplitude : -amplitude);
    contrast = std::clamp(amplitude / std::max({ std::abs(background), std::abs(peak), amplitude, 1e-12 }), 0.0, 1.0);
    if (!std::isfinite(contrast) || amplitude <= 0 || contrast < options.minimumContrast) return false;
    double sum = 0, wx = 0, wy = 0;
    area = 0;
    for (int y = 0; y < patch.rows; ++y) {
        const float* row = excess.ptr<float>(y);
        for (int x = 0; x < patch.cols; ++x) {
            if (row[x] < amplitude * 0.10) continue;
            const double weight = row[x] - amplitude * 0.05;
            sum += weight; wx += weight * x; wy += weight * y; area += 1;
        }
    }
    if (sum <= 0 || area < 3) return false;
    center = cv::Point2f(static_cast<float>(x0 + wx / sum), static_cast<float>(y0 + wy / sum));
    const cv::Point2f initial(static_cast<float>((candidate.center.x + 0.5) / sx - 0.5), static_cast<float>((candidate.center.y + 0.5) / sy - 0.5));
    return cv::norm(center - initial) <= std::max(candidate.bounds.width / sx, candidate.bounds.height / sy);
}

double signedDistance(const cv::Point2f& p, const cv::Point2f& a, const cv::Point2f& b)
{
    const cv::Point2d edge = cv::Point2d(b) - cv::Point2d(a);
    const cv::Point2d delta = cv::Point2d(p) - cv::Point2d(a);
    return (edge.x * delta.y - edge.y * delta.x) / cv::norm(edge);
}

json metrics(const std::vector<cv::Point2f>& grid, const std::array<int, 9>& ids, int tvCalcWay)
{
    std::array<cv::Point2f, 9> p;
    for (size_t i = 0; i < ids.size(); ++i) p[i] = grid[ids[i]];
    const double top = cv::norm(p[0] - p[2]), middle = cv::norm(p[3] - p[5]), bottom = cv::norm(p[6] - p[8]);
    const double left = cv::norm(p[0] - p[6]), center = cv::norm(p[1] - p[7]), right = cv::norm(p[2] - p[8]);
    if (std::min({ top, middle, bottom, left, center, right }) <= 1e-9) return nullptr;
    const double tvScale = tvCalcWay == 1 ? 50.0 : 100.0;
    return {
        {"horizontalTvPercent", tvScale * ((top + bottom) / 2 - middle) / middle},
        {"verticalTvPercent", tvScale * ((left + right) / 2 - center) / center},
        {"topPercent", 200 * signedDistance(p[1], p[0], p[2]) / (left + right)},
        {"bottomPercent", -200 * signedDistance(p[7], p[6], p[8]) / (left + right)},
        {"leftPercent", -200 * signedDistance(p[3], p[0], p[6]) / (top + bottom)},
        {"rightPercent", 200 * signedDistance(p[5], p[2], p[8]) / (top + bottom)},
        {"keystoneHorizontalPercent", 200 * (left - right) / (left + right)},
        {"keystoneVerticalPercent", 200 * (top - bottom) / (top + bottom)},
        {"topWidth", top}, {"middleWidth", middle}, {"bottomWidth", bottom},
        {"leftHeight", left}, {"centerHeight", center}, {"rightHeight", right}
    };
}
} // namespace

json calculateGridDistortionV2(const cv::Mat& image, const json& config, const cv::Point& origin)
{
    const auto totalStart = Clock::now();
    Options options;
    bool integerOptionsValid = true;
    if (config.is_object()) {
        auto integerOption = [&](const char* key, int fallback) {
            if (!config.contains(key)) return fallback;
            const auto& value = config.at(key);
            if (!value.is_number_integer() || value.get<double>() < std::numeric_limits<int>::min() || value.get<double>() > std::numeric_limits<int>::max()) {
                integerOptionsValid = false;
                return fallback;
            }
            return value.get<int>();
        };
        options.rows = integerOption("expectedRows", options.rows);
        options.cols = integerOption("expectedCols", options.cols);
        options.bright = config.value("brightTarget", options.bright);
        options.maxSize = integerOption("maxProcessingSize", options.maxSize);
        options.tvCalcWay = integerOption("tvCalcWay", options.tvCalcWay);
        options.minimumContrast = config.value("minimumContrast", options.minimumContrast);
        options.maxResidual = config.value("maximumGridResidualFraction", options.maxResidual);
    }
    json result = {
        {"algorithm", "GridDistortion"}, {"version", "2.0"}, {"success", false},
        {"statusCode", "invalid_image"}, {"message", "Invalid or unsupported image."},
        {"expectedRows", options.rows}, {"expectedCols", options.cols},
        {"candidateCount", 0}, {"selectedCount", 0}, {"points", json::array()},
        {"referencePointIds", json::array()}, {"metrics", nullptr}, {"warnings", json::array()},
        {"configUsed", {{"expectedRows", options.rows}, {"expectedCols", options.cols}, {"brightTarget", options.bright}, {"maxProcessingSize", options.maxSize}, {"minimumContrast", options.minimumContrast}, {"maximumGridResidualFraction", options.maxResidual}, {"tvCalcWay", options.tvCalcWay}}},
        {"formula", "TV: edge-average versus center (tvCalcWay=1 halves TV); Point9: two-opposite-edge normalization; Keystone H: left/right heights; V: top/bottom widths; inward edge bow is positive."},
        {"quality", {{"score", 0.0}, {"minimumContrast", 0.0}, {"gridResidualFraction", 0.0}, {"processingScale", 1.0}}},
        {"timings", {{"preprocessMs", 0.0}, {"candidatesMs", 0.0}, {"gridMs", 0.0}, {"refineMs", 0.0}, {"metricsMs", 0.0}, {"totalMs", 0.0}}}
    };
    auto finish = [&](const char* status, const char* message) {
        result["statusCode"] = status; result["message"] = message;
        result["timings"]["totalMs"] = elapsed(totalStart);
        return result;
    };
    if (!config.is_object() || !integerOptionsValid || options.rows < 3 || options.cols < 3 || options.rows > 15 || options.cols > 15
        || options.rows % 2 == 0 || options.cols % 2 == 0 || options.maxSize < 256 || options.maxSize > 4096
        || !std::isfinite(options.minimumContrast) || options.minimumContrast < 0 || options.minimumContrast > 1
        || !std::isfinite(options.maxResidual) || options.maxResidual <= 0 || options.maxResidual > 1 || options.tvCalcWay < 0 || options.tvCalcWay > 1)
        return finish("invalid_config", "Use odd grid dimensions from 3 to 15 and finite option values in the documented ranges.");
    if (image.empty() || (image.channels() != 1 && image.channels() != 3 && image.channels() != 4)
        || (image.depth() != CV_8U && image.depth() != CV_16U && image.depth() != CV_32F && image.depth() != CV_64F))
        return finish("invalid_image", "Expected a nonempty 8U, 16U, 32F or 64F image with 1, 3 or 4 channels.");

    auto stage = Clock::now();
    const double scale = std::min(1.0, static_cast<double>(options.maxSize) / std::max(image.cols, image.rows));
    cv::Mat small;
    if (scale < 1.0) cv::resize(image, small, cv::Size(std::max(1, cvRound(image.cols * scale)), std::max(1, cvRound(image.rows * scale))), 0, 0, cv::INTER_AREA);
    else small = image;
    const double sx = static_cast<double>(small.cols) / image.cols, sy = static_cast<double>(small.rows) / image.rows;
    cv::Mat gray = grayFloat(small), gray8;
    result["quality"]["processingScale"] = scale;
    if (!cv::checkRange(gray)) return finish("invalid_image", "Image contains non-finite intensities.");
    double minimum = 0, maximum = 0;
    cv::minMaxLoc(gray, &minimum, &maximum);
    const double range = maximum - minimum;
    if (!std::isfinite(range) || range <= 1e-12) return finish("uniform_image", "Image has no measurable contrast.");
    // Estimate low-frequency leakage on a bounded secondary thumbnail. Candidate
    // thresholds operate on local background residuals, not absolute image gray.
    cv::Mat backgroundSmall, background, signal, localScale;
    const double backgroundScale = std::min(1.0, 400.0 / std::max(gray.cols, gray.rows));
    cv::resize(gray, backgroundSmall, cv::Size(std::max(1, cvRound(gray.cols * backgroundScale)), std::max(1, cvRound(gray.rows * backgroundScale))), 0, 0, cv::INTER_AREA);
    const double sigma = std::max(2.0, std::min(static_cast<double>(backgroundSmall.cols) / options.cols, static_cast<double>(backgroundSmall.rows) / options.rows) * 0.12);
    cv::GaussianBlur(backgroundSmall, backgroundSmall, cv::Size(), sigma, sigma, cv::BORDER_REFLECT101);
    cv::resize(backgroundSmall, background, gray.size(), 0, 0, cv::INTER_LINEAR);
    signal = options.bright ? gray - background : background - gray;
    // Preserve high local contrast at dim points even when an unrelated saturated
    // reflection dominates a 16-bit/float image's global range. Global min/max is
    // only a numerical floor; it must not quantize those point amplitudes to zero.
    cv::Mat grayMagnitude = cv::abs(gray), backgroundMagnitude = cv::abs(background);
    cv::max(grayMagnitude, backgroundMagnitude, localScale);
    cv::max(localScale, range * 1e-6, localScale);
    cv::divide(signal, localScale, signal);
    signal.convertTo(gray8, CV_8U, 255.0);
    result["timings"]["preprocessMs"] = elapsed(stage);
    cv::Mat ignored;
    const double otsu = cv::threshold(gray8, ignored, 0, 255, cv::THRESH_BINARY | cv::THRESH_OTSU);
    const double floor = std::max(1.0, options.minimumContrast * 255.0 * 0.5);
    const std::array<double, 3> thresholds = { std::max(otsu, floor), std::max(otsu * 0.35, floor), std::max(otsu * 0.10, floor) };
    std::vector<Candidate> candidates;
    std::vector<cv::Point2f> ordered;
    double residual = 0, candidateMs = 0, gridMs = 0;
    int maxCandidates = 0, usedAttempt = -1;
    bool ambiguous = false;
    for (int attempt = 0; attempt < static_cast<int>(thresholds.size()); ++attempt) {
        if (attempt > 0 && std::abs(thresholds[attempt] - thresholds[attempt - 1]) < 0.5) continue;
        stage = Clock::now();
        candidates = detectCandidates(gray8, thresholds[attempt], options);
        candidateMs += elapsed(stage);
        maxCandidates = std::max(maxCandidates, static_cast<int>(candidates.size()));
        stage = Clock::now();
        const bool found = orderCandidates(candidates, options, ordered, residual, ambiguous);
        gridMs += elapsed(stage);
        if (found) { usedAttempt = attempt; break; }
    }
    result["timings"]["candidatesMs"] = candidateMs;
    result["timings"]["gridMs"] = gridMs;
    result["candidateCount"] = usedAttempt >= 0 ? static_cast<int>(candidates.size()) : maxCandidates;
    if (usedAttempt < 0) {
        if (ambiguous) return finish("ambiguous_grid", "Additional candidates fit an extension of the selected grid; verify the configured rows and columns.");
        if (maxCandidates < options.rows * options.cols) return finish("too_few_candidates", "The complete grid was not detected. Check missing points, contrast and ROI coverage.");
        return finish("grid_not_found", "Candidates do not form a complete, consistent grid within the residual limit.");
    }

    stage = Clock::now();
    std::vector<cv::Point2f> refined;
    std::vector<double> areas, contrasts;
    for (const auto& center : ordered) {
        const auto best = std::min_element(candidates.begin(), candidates.end(), [&](const auto& a, const auto& b) { return cv::norm(a.center - center) < cv::norm(b.center - center); });
        cv::Point2f point;
        double area = 0, contrast = 0;
        if (best == candidates.end() || !refinePoint(image, *best, sx, sy, options, point, area, contrast)) {
            result["timings"]["refineMs"] = elapsed(stage);
            return finish("low_contrast", "At least one selected point lacks sufficient original-image contrast for refinement.");
        }
        refined.push_back(point); areas.push_back(area); contrasts.push_back(contrast);
    }
    result["timings"]["refineMs"] = elapsed(stage);
    stage = Clock::now();
    cv::Mat homography;
    if (!validateGrid(refined, options, residual, homography)) return finish("grid_residual_too_large", "Refined measured centers fail the grid consistency or residual check.");
    const int midR = options.rows / 2, midC = options.cols / 2, lastR = options.rows - 1, lastC = options.cols - 1;
    const std::array<int, 9> references = { 0, midC, lastC, midR * options.cols, midR * options.cols + midC, midR * options.cols + lastC, lastR * options.cols, lastR * options.cols + midC, lastR * options.cols + lastC };
    result["metrics"] = metrics(refined, references, options.tvCalcWay);
    if (result["metrics"].is_null()) return finish("degenerate_grid", "A reference span is degenerate.");
    const std::array<const char*, 9> names = { "TL", "TC", "TR", "ML", "C", "MR", "BL", "BC", "BR" };
    for (int id = 0; id < static_cast<int>(refined.size()); ++id) {
        std::string name = "R" + std::to_string(id / options.cols + 1) + "C" + std::to_string(id % options.cols + 1);
        for (size_t i = 0; i < references.size(); ++i) if (references[i] == id) name = names[i];
        result["points"].push_back({ {"id", id}, {"row", id / options.cols}, {"col", id % options.cols}, {"name", name},
            {"x", static_cast<double>(refined[id].x) + origin.x}, {"y", static_cast<double>(refined[id].y) + origin.y}, {"area", areas[id]}, {"contrast", contrasts[id]} });
    }
    result["referencePointIds"] = references;
    result["selectedCount"] = refined.size();
    const double minimumContrast = *std::min_element(contrasts.begin(), contrasts.end());
    result["quality"]["minimumContrast"] = minimumContrast;
    result["quality"]["gridResidualFraction"] = residual;
    // A reproducible heuristic, not a calibrated probability or an accuracy estimate.
    result["quality"]["score"] = std::clamp((1.0 - residual / options.maxResidual) * std::min(1.0, minimumContrast / std::max(0.10, options.minimumContrast)), 0.0, 1.0);
    if (usedAttempt > 0) result["warnings"].push_back("A lower segmentation threshold was needed to recover the complete grid.");
    if (candidates.size() > refined.size()) result["warnings"].push_back("Non-grid candidates were excluded by topology validation.");
    result["timings"]["metricsMs"] = elapsed(stage);
    result["success"] = true;
    return finish("ok", "Complete grid measured; quality score is a heuristic, not a probability.");
}
} // namespace cvcore::distortion
