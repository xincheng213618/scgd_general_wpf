#include "luminous_area_v2.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <iterator>
#include <limits>
#include <numeric>
#include <random>
#include <utility>

#include <opencv2/imgproc.hpp>

namespace cvnative::luminous
{
namespace
{

constexpr int kSideCount = 4;
constexpr int kCandidatesPerCaliper = 4;
constexpr double kEpsilon = 1e-9;

struct CoarseCandidate
{
    std::array<cv::Point2f, 4> quad{};
    double score = 0.0;
    double contrast = 0.0;
    double areaRatio = 0.0;
    bool touchesBorder = false;
    // Number of distinct (sigma, de-duplicated threshold) observations.
    int sourceVotes = 1;
    unsigned int sigmaMask = 0;
};

struct EdgeCandidate
{
    cv::Point2f point{};
    double strength = 0.0;
    double offset = 0.0;
    int caliper = 0;
};

struct LineModel
{
    cv::Vec3d equation{ 0.0, 0.0, 0.0 };
    bool valid = false;
};

struct SideFit
{
    LineModel line;
    LuminousSideQuality quality;
    bool accepted = false;
    bool usable = false;
    bool anchored = false;
};

struct SuccessfulCandidate
{
    FindLuminousAreaV2Result result;
    double rank = 0.0;
    double areaRatio = 0.0;
    int scaleVotes = 1;
};

double Clamp01(double value)
{
    return std::clamp(value, 0.0, 1.0);
}

int CountBits(unsigned int value)
{
    int count = 0;
    while (value != 0) {
        count += static_cast<int>(value & 1U);
        value >>= 1U;
    }
    return count;
}

double SignedArea(const std::array<cv::Point2f, 4>& points)
{
    double area = 0.0;
    for (int i = 0; i < kSideCount; ++i) {
        const cv::Point2f& a = points[i];
        const cv::Point2f& b = points[(i + 1) % kSideCount];
        area += static_cast<double>(a.x) * b.y - static_cast<double>(a.y) * b.x;
    }
    return 0.5 * area;
}

std::array<cv::Point2f, 4> OrderCorners(const std::array<cv::Point2f, 4>& input)
{
    cv::Point2f center{};
    for (const cv::Point2f& point : input) {
        center += point;
    }
    center *= 0.25f;

    std::array<cv::Point2f, 4> ordered = input;
    std::sort(ordered.begin(), ordered.end(), [&](const cv::Point2f& left, const cv::Point2f& right) {
        return std::atan2(left.y - center.y, left.x - center.x)
            < std::atan2(right.y - center.y, right.x - center.x);
    });

    if (SignedArea(ordered) < 0.0) {
        std::reverse(ordered.begin(), ordered.end());
    }

    auto first = std::min_element(ordered.begin(), ordered.end(), [](const cv::Point2f& left, const cv::Point2f& right) {
        const double leftScore = left.x + left.y;
        const double rightScore = right.x + right.y;
        return leftScore == rightScore ? left.y < right.y : leftScore < rightScore;
    });
    std::rotate(ordered.begin(), first, ordered.end());
    return ordered;
}

template<typename T>
double Quantile(std::vector<T> values, double quantile)
{
    if (values.empty()) {
        return 0.0;
    }
    const size_t index = static_cast<size_t>(std::clamp(quantile, 0.0, 1.0) * (values.size() - 1));
    std::nth_element(values.begin(), values.begin() + index, values.end());
    return values[index];
}

bool NormalizeGray(const cv::Mat& image, cv::Mat& normalized, double& sourceRange)
{
    if (image.empty()) {
        return false;
    }

    cv::Mat gray;
    if (image.channels() == 1) {
        gray = image;
    }
    else if (image.channels() == 3) {
        cv::cvtColor(image, gray, cv::COLOR_BGR2GRAY);
    }
    else if (image.channels() == 4) {
        cv::cvtColor(image, gray, cv::COLOR_BGRA2GRAY);
    }
    else {
        return false;
    }

    gray.convertTo(normalized, CV_32F);
    cv::patchNaNs(normalized, 0.0);

    const double sampleScale = std::min(1.0, 512.0 / std::max(normalized.cols, normalized.rows));
    cv::Mat sampled;
    if (sampleScale < 1.0) {
        cv::resize(normalized, sampled, cv::Size(), sampleScale, sampleScale, cv::INTER_AREA);
    }
    else {
        sampled = normalized;
    }

    std::vector<float> values;
    values.reserve(sampled.total());
    for (int y = 0; y < sampled.rows; ++y) {
        const float* row = sampled.ptr<float>(y);
        for (int x = 0; x < sampled.cols; ++x) {
            if (std::isfinite(row[x])) {
                values.push_back(row[x]);
            }
        }
    }
    if (values.size() < 16) {
        return false;
    }

    // Preserve a thin dark surround when the panel fills almost the whole
    // frame, while excluding sub-percent saturated leaks from the scale. A
    // small target over a flat background still clips cleanly to 1.0.
    const double low = Quantile(values, 0.001);
    double high = Quantile(values, 0.99);
    double sourceRangeCandidate = high - low;
    const double initialScaleReference = std::max({ std::abs(low), std::abs(high), 1.0 });
    if (!std::isfinite(sourceRangeCandidate)
        || sourceRangeCandidate <= initialScaleReference * 1e-6) {
        high = Quantile(std::move(values), 0.9995);
    }
    sourceRange = high - low;
    const double scaleReference = std::max({ std::abs(low), std::abs(high), 1.0 });
    if (!std::isfinite(sourceRange) || sourceRange <= scaleReference * 1e-6) {
        normalized.release();
        return true;
    }

    normalized = (normalized - low) / sourceRange;
    cv::max(normalized, 0.0, normalized);
    cv::min(normalized, 1.0, normalized);
    return true;
}

cv::Point2f BilinearPoint(const cv::Mat& image, const cv::Point2f& point, bool& valid)
{
    valid = point.x >= 0.0f && point.y >= 0.0f
        && point.x < image.cols - 1.0f && point.y < image.rows - 1.0f;
    return point;
}

float BilinearSample(const cv::Mat& image, const cv::Point2f& point, bool& valid)
{
    BilinearPoint(image, point, valid);
    if (!valid) {
        return 0.0f;
    }
    const int x = static_cast<int>(std::floor(point.x));
    const int y = static_cast<int>(std::floor(point.y));
    const float fx = point.x - x;
    const float fy = point.y - y;
    const float* row0 = image.ptr<float>(y);
    const float* row1 = image.ptr<float>(y + 1);
    return (1.0f - fy) * ((1.0f - fx) * row0[x] + fx * row0[x + 1])
        + fy * ((1.0f - fx) * row1[x] + fx * row1[x + 1]);
}

bool TouchesBorder(const std::vector<cv::Point>& contour, const cv::Size& size)
{
    const cv::Rect bounds = cv::boundingRect(contour);
    return bounds.x <= 1 || bounds.y <= 1
        || bounds.br().x >= size.width - 1 || bounds.br().y >= size.height - 1;
}

double PolygonContrast(const cv::Mat& gray, const std::array<cv::Point2f, 4>& quad)
{
    std::vector<cv::Point> polygon;
    polygon.reserve(4);
    for (const cv::Point2f& point : quad) {
        polygon.emplace_back(cvRound(point.x), cvRound(point.y));
    }

    cv::Mat mask = cv::Mat::zeros(gray.size(), CV_8U);
    cv::fillConvexPoly(mask, polygon, cv::Scalar(255));
    const int radius = std::max(2, cvRound(std::min(gray.cols, gray.rows) * 0.006));
    const cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(radius * 2 + 1, radius * 2 + 1));
    cv::Mat inner;
    cv::Mat outer;
    cv::erode(mask, inner, kernel);
    cv::dilate(mask, outer, kernel);
    outer.setTo(0, mask);

    const double inside = cv::mean(gray, inner.empty() ? mask : inner)[0];
    const double outside = cv::countNonZero(outer) > 0 ? cv::mean(gray, outer)[0] : inside;
    return inside - outside;
}

bool SimilarQuad(const CoarseCandidate& left, const CoarseCandidate& right)
{
    cv::Point2f leftCenter{};
    cv::Point2f rightCenter{};
    double leftLength = 0.0;
    for (int i = 0; i < kSideCount; ++i) {
        leftCenter += left.quad[i];
        rightCenter += right.quad[i];
        leftLength += cv::norm(left.quad[(i + 1) % kSideCount] - left.quad[i]);
    }
    leftCenter *= 0.25f;
    rightCenter *= 0.25f;
    const double scale = std::max(1.0, leftLength * 0.25);
    return cv::norm(leftCenter - rightCenter) / scale < 0.05
        && std::abs(left.areaRatio - right.areaRatio) < std::max(0.01, left.areaRatio * 0.15);
}

std::array<cv::Point2f, 4> QuadFromContour(const std::vector<cv::Point>& contour)
{
    std::vector<cv::Point> hull;
    cv::convexHull(contour, hull);
    std::vector<cv::Point> approximation;
    const double perimeter = cv::arcLength(hull, true);
    for (double epsilon : { 0.01, 0.015, 0.02, 0.03, 0.04, 0.06 }) {
        cv::approxPolyDP(hull, approximation, perimeter * epsilon, true);
        if (approximation.size() == 4 && cv::isContourConvex(approximation)) {
            std::array<cv::Point2f, 4> quad{};
            std::transform(approximation.begin(), approximation.end(), quad.begin(), [](const cv::Point& point) {
                return cv::Point2f(point);
            });
            return OrderCorners(quad);
        }
    }

    cv::Point2f vertices[4];
    cv::minAreaRect(hull).points(vertices);
    return OrderCorners({ vertices[0], vertices[1], vertices[2], vertices[3] });
}

std::array<cv::Point2f, 4> RobustBoxFromContour(const std::vector<cv::Point>& contour)
{
    cv::Point2f vertices[4];
    cv::minAreaRect(contour).points(vertices);
    const auto box = OrderCorners({ vertices[0], vertices[1], vertices[2], vertices[3] });
    cv::Point2f axisU = box[1] - box[0];
    cv::Point2f axisV = box[3] - box[0];
    axisU *= static_cast<float>(1.0 / std::max(1e-6, cv::norm(axisU)));
    axisV *= static_cast<float>(1.0 / std::max(1e-6, cv::norm(axisV)));

    std::vector<float> projectionsU;
    std::vector<float> projectionsV;
    projectionsU.reserve(contour.size());
    projectionsV.reserve(contour.size());
    for (const cv::Point& point : contour) {
        const cv::Point2f value(point);
        projectionsU.push_back(value.dot(axisU));
        projectionsV.push_back(value.dot(axisV));
    }
    const float lowU = static_cast<float>(Quantile(projectionsU, 0.08));
    const float highU = static_cast<float>(Quantile(std::move(projectionsU), 0.92));
    const float lowV = static_cast<float>(Quantile(projectionsV, 0.08));
    const float highV = static_cast<float>(Quantile(std::move(projectionsV), 0.92));
    return OrderCorners({
        axisU * lowU + axisV * lowV,
        axisU * highU + axisV * lowV,
        axisU * highU + axisV * highV,
        axisU * lowU + axisV * highV
    });
}

std::vector<CoarseCandidate> FindCoarseCandidates(
    const cv::Mat& gray,
    const FindLuminousAreaV2Config& config)
{
    std::vector<CoarseCandidate> candidates;
    cv::Mat gray8;
    gray.convertTo(gray8, CV_8U, 255.0);

    static constexpr std::array<double, 3> sigmas{ 0.8, 1.6, 3.0 };
    for (size_t sigmaIndex = 0; sigmaIndex < sigmas.size(); ++sigmaIndex) {
        const double sigma = sigmas[sigmaIndex];
        cv::Mat blurred;
        cv::GaussianBlur(gray8, blurred, cv::Size(), sigma, sigma, cv::BORDER_REPLICATE);
        cv::Mat temporary;
        const double otsu = cv::threshold(blurred, temporary, 0, 255, cv::THRESH_BINARY | cv::THRESH_OTSU) / 255.0;
        // Low thresholds keep a dark side of a strongly sloped display in the
        // same coarse component as its bright side. Later line evidence and
        // confidence checks, rather than this threshold, decide acceptance.
        std::vector<double> thresholds{ 0.05, 0.08, 0.10, 0.15, 0.28, 0.45, 0.65, otsu };
        for (double& threshold : thresholds) {
            threshold = std::clamp(threshold, 0.04, 0.90);
        }
        std::sort(thresholds.begin(), thresholds.end());
        thresholds.erase(
            std::unique(thresholds.begin(), thresholds.end(), [](double left, double right) {
                return std::abs(left - right) <= 0.01;
            }),
            thresholds.end());

        for (double threshold : thresholds) {
            cv::Mat binary;
            cv::threshold(blurred, binary, threshold * 255.0, 255, cv::THRESH_BINARY);
            const int radius = std::max(1, cvRound(sigma * 1.5));
            const cv::Mat kernel = cv::getStructuringElement(
                cv::MORPH_ELLIPSE,
                cv::Size(radius * 2 + 1, radius * 2 + 1));
            cv::morphologyEx(binary, binary, cv::MORPH_OPEN, kernel);
            cv::morphologyEx(binary, binary, cv::MORPH_CLOSE, kernel, cv::Point(-1, -1), 2);

            std::vector<std::vector<cv::Point>> contours;
            cv::findContours(binary, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_NONE);
            for (const auto& contour : contours) {
                const double contourArea = std::abs(cv::contourArea(contour));
                const double areaRatio = contourArea / static_cast<double>(gray.total());
                if (areaRatio < config.minAreaRatio || areaRatio > config.maxAreaRatio || contour.size() < 4) {
                    continue;
                }
                const bool border = TouchesBorder(contour, gray.size());
                if (border && !config.allowBorder) {
                    continue;
                }
                const auto appendCandidate = [&](const std::array<cv::Point2f, 4>& quad) {
                    CoarseCandidate candidate;
                    candidate.quad = quad;
                    candidate.areaRatio = areaRatio;
                    candidate.touchesBorder = border;
                    candidate.sigmaMask = 1U << static_cast<unsigned int>(sigmaIndex);
                    const double quadArea = std::abs(SignedArea(candidate.quad));
                    if (quadArea < 25.0) {
                        return;
                    }
                    const double fill = Clamp01(contourArea / quadArea);
                    candidate.contrast = PolygonContrast(gray, candidate.quad);
                    const double areaScore = Clamp01(std::log1p(areaRatio * 200.0) / std::log(21.0));
                    candidate.score = 2.2 * Clamp01(candidate.contrast / 0.25)
                        + 0.9 * fill + 0.7 * areaScore - (border ? 0.35 : 0.0);
                    if (candidate.contrast > 0.01) {
                        candidates.push_back(candidate);
                    }
                };
                appendCandidate(QuadFromContour(contour));
                appendCandidate(RobustBoxFromContour(contour));
            }
        }
    }

    std::sort(candidates.begin(), candidates.end(), [](const CoarseCandidate& left, const CoarseCandidate& right) {
        return left.score > right.score;
    });
    constexpr size_t maximumRetainedCandidates = 10;
    constexpr size_t areaReservoirSize = 16;
    constexpr size_t stableAreaReservations = 2;
    std::vector<CoarseCandidate> highestScore;
    std::vector<CoarseCandidate> largestArea;
    const auto mergeObservation = [&](std::vector<CoarseCandidate>& grouped, const CoarseCandidate& candidate) {
        auto duplicate = std::find_if(grouped.begin(), grouped.end(), [&](const CoarseCandidate& existing) {
            return SimilarQuad(candidate, existing);
        });
        if (duplicate == grouped.end()) {
            return false;
        }
        ++duplicate->sourceVotes;
        duplicate->sigmaMask |= candidate.sigmaMask;
        return true;
    };
    for (const CoarseCandidate& candidate : candidates) {
        if (!mergeObservation(highestScore, candidate)
            && highestScore.size() < maximumRetainedCandidates) {
            highestScore.push_back(candidate);
        }
        if (!mergeObservation(largestArea, candidate)) {
            if (largestArea.size() < areaReservoirSize) {
                largestArea.push_back(candidate);
            }
            else {
                auto smallest = std::min_element(
                    largestArea.begin(),
                    largestArea.end(),
                    [](const CoarseCandidate& left, const CoarseCandidate& right) {
                        return left.areaRatio < right.areaRatio;
                    });
                if (candidate.areaRatio > smallest->areaRatio) {
                    *smallest = candidate;
                }
            }
        }
    }

    // Preserve the bounded fine-stage cost, but do not let numerous high-
    // contrast glyphs or highlights consume every slot before a weak outer
    // display boundary can accumulate its cross-scale evidence. Both coarse
    // reservoirs are bounded, avoiding quadratic growth on highly textured
    // frames while keeping two slots for stable large-area observations.
    std::vector<CoarseCandidate> stableByArea;
    std::copy_if(
        largestArea.begin(),
        largestArea.end(),
        std::back_inserter(stableByArea),
        [](const CoarseCandidate& candidate) { return CountBits(candidate.sigmaMask) >= 2; });
    std::sort(
        stableByArea.begin(),
        stableByArea.end(),
        [](const CoarseCandidate& left, const CoarseCandidate& right) {
            return left.areaRatio == right.areaRatio
                ? left.score > right.score
                : left.areaRatio > right.areaRatio;
        });

    std::vector<CoarseCandidate> retained;
    for (const CoarseCandidate& candidate : stableByArea) {
        if (retained.size() >= stableAreaReservations) {
            break;
        }
        const bool alreadyRetained = std::any_of(
            retained.begin(),
            retained.end(),
            [&](const CoarseCandidate& existing) { return SimilarQuad(candidate, existing); });
        if (!alreadyRetained) {
            retained.push_back(candidate);
        }
    }
    for (const CoarseCandidate& candidate : highestScore) {
        if (retained.size() >= maximumRetainedCandidates) {
            break;
        }
        const bool alreadyRetained = std::any_of(
            retained.begin(),
            retained.end(),
            [&](const CoarseCandidate& existing) { return SimilarQuad(candidate, existing); });
        if (!alreadyRetained) {
            retained.push_back(candidate);
        }
    }
    std::sort(retained.begin(), retained.end(), [](const CoarseCandidate& left, const CoarseCandidate& right) {
        return left.score > right.score;
    });
    return retained;
}

LineModel LineFromPoints(const cv::Point2f& first, const cv::Point2f& second)
{
    const cv::Point2f delta = second - first;
    const double length = cv::norm(delta);
    if (length <= 1e-5) {
        return {};
    }
    LineModel line;
    line.equation = cv::Vec3d(delta.y / length, -delta.x / length, 0.0);
    line.equation[2] = -line.equation[0] * first.x - line.equation[1] * first.y;
    line.valid = true;
    return line;
}

double PointLineDistance(const LineModel& line, const cv::Point2f& point)
{
    return std::abs(line.equation[0] * point.x + line.equation[1] * point.y + line.equation[2]);
}

double DirectionAgreement(const LineModel& line, const cv::Point2f& referenceDirection)
{
    const cv::Point2f direction(
        static_cast<float>(-line.equation[1]),
        static_cast<float>(line.equation[0]));
    const double denominator = std::max(kEpsilon, cv::norm(direction) * cv::norm(referenceDirection));
    return std::abs(direction.dot(referenceDirection) / denominator);
}

std::vector<std::vector<EdgeCandidate>> CollectCaliperCandidates(
    const cv::Mat& gray,
    const cv::Point2f& start,
    const cv::Point2f& end,
    int count,
    double searchHalfWidth,
    double minContrast)
{
    std::vector<std::vector<EdgeCandidate>> all(static_cast<size_t>(count));
    cv::Point2f direction = end - start;
    const double length = cv::norm(direction);
    if (length < 8.0) {
        return all;
    }
    direction *= static_cast<float>(1.0 / length);
    const cv::Point2f outward(direction.y, -direction.x);
    const double step = 0.75;
    const int sampleCount = std::max(9, cvRound(searchHalfWidth * 2.0 / step) + 1);
    const int band = 3;

    for (int caliper = 0; caliper < count; ++caliper) {
        const double fraction = 0.08 + 0.84 * (caliper + 0.5) / count;
        const cv::Point2f base = start + direction * static_cast<float>(length * fraction);
        std::vector<float> profile(static_cast<size_t>(sampleCount), 0.0f);
        std::vector<unsigned char> valid(static_cast<size_t>(sampleCount), 0);
        for (int sample = 0; sample < sampleCount; ++sample) {
            const double offset = -searchHalfWidth + sample * step;
            bool isValid = false;
            profile[sample] = BilinearSample(gray, base + outward * static_cast<float>(offset), isValid);
            valid[sample] = isValid ? 1 : 0;
        }

        const auto contrastAt = [&](int index, bool& hasContrast) {
            int availableBand = std::min({ band, index, sampleCount - 1 - index });
            while (availableBand > 0
                && (!valid[index - availableBand] || !valid[index + availableBand])) {
                --availableBand;
            }
            if (availableBand <= 0) {
                hasContrast = false;
                return 0.0;
            }
            double inside = 0.0;
            double outside = 0.0;
            for (int offset = 1; offset <= availableBand; ++offset) {
                inside += profile[index - offset];
                outside += profile[index + offset];
            }
            hasContrast = true;
            return (inside - outside) / availableBand;
        };

        std::vector<EdgeCandidate> local;
        for (int sample = 1; sample + 1 < sampleCount; ++sample) {
            bool hasContrast = false;
            const double strength = contrastAt(sample, hasContrast);
            if (!hasContrast) {
                continue;
            }
            if (strength < minContrast) {
                continue;
            }

            bool hasPreviousContrast = false;
            const double previousContrast = contrastAt(sample - 1, hasPreviousContrast);
            if (hasPreviousContrast && strength < previousContrast) {
                continue;
            }
            bool hasNextContrast = false;
            const double nextContrast = contrastAt(sample + 1, hasNextContrast);
            if (hasNextContrast && strength < nextContrast) {
                continue;
            }

            const double offset = -searchHalfWidth + sample * step;
            local.push_back({
                base + outward * static_cast<float>(offset),
                strength,
                offset,
                caliper
            });
        }

        std::sort(local.begin(), local.end(), [&](const EdgeCandidate& left, const EdgeCandidate& right) {
            const double leftPrior = left.strength * (1.0 - 0.18 * std::abs(left.offset) / searchHalfWidth);
            const double rightPrior = right.strength * (1.0 - 0.18 * std::abs(right.offset) / searchHalfWidth);
            return leftPrior > rightPrior;
        });
        if (local.size() > kCandidatesPerCaliper) {
            local.resize(kCandidatesPerCaliper);
        }
        all[caliper] = std::move(local);
    }
    return all;
}

std::vector<EdgeCandidate> SelectInliers(
    const std::vector<std::vector<EdgeCandidate>>& candidates,
    const LineModel& line,
    double distanceThreshold)
{
    std::vector<EdgeCandidate> selected;
    for (const auto& caliper : candidates) {
        const EdgeCandidate* best = nullptr;
        double bestCost = std::numeric_limits<double>::max();
        for (const EdgeCandidate& candidate : caliper) {
            const double distance = PointLineDistance(line, candidate.point);
            if (distance > distanceThreshold) {
                continue;
            }
            const double cost = distance - std::min(0.35, candidate.strength) * distanceThreshold * 0.30;
            if (cost < bestCost) {
                bestCost = cost;
                best = &candidate;
            }
        }
        if (best != nullptr) {
            selected.push_back(*best);
        }
    }
    return selected;
}

LineModel WeightedLineFit(
    const std::vector<EdgeCandidate>& points,
    const std::vector<double>& robustWeights)
{
    if (points.size() < 2 || points.size() != robustWeights.size()) {
        return {};
    }
    double weightSum = 0.0;
    cv::Point2d center{};
    double maxStrength = 0.0;
    for (const EdgeCandidate& point : points) {
        maxStrength = std::max(maxStrength, point.strength);
    }
    for (size_t index = 0; index < points.size(); ++index) {
        const double strengthWeight = 0.25 + 0.75 * points[index].strength / std::max(kEpsilon, maxStrength);
        const double weight = robustWeights[index] * strengthWeight;
        weightSum += weight;
        center.x += weight * points[index].point.x;
        center.y += weight * points[index].point.y;
    }
    if (weightSum <= kEpsilon) {
        return {};
    }
    center *= 1.0 / weightSum;

    double xx = 0.0;
    double xy = 0.0;
    double yy = 0.0;
    for (size_t index = 0; index < points.size(); ++index) {
        const double strengthWeight = 0.25 + 0.75 * points[index].strength / std::max(kEpsilon, maxStrength);
        const double weight = robustWeights[index] * strengthWeight;
        const double x = points[index].point.x - center.x;
        const double y = points[index].point.y - center.y;
        xx += weight * x * x;
        xy += weight * x * y;
        yy += weight * y * y;
    }
    cv::Matx22d covariance(xx / weightSum, xy / weightSum, xy / weightSum, yy / weightSum);
    cv::Mat eigenValues;
    cv::Mat eigenVectors;
    if (!cv::eigen(cv::Mat(covariance), eigenValues, eigenVectors)) {
        return {};
    }
    const cv::Vec2d direction(eigenVectors.at<double>(0, 0), eigenVectors.at<double>(0, 1));
    LineModel line;
    line.equation = cv::Vec3d(direction[1], -direction[0], 0.0);
    line.equation[2] = -line.equation[0] * center.x - line.equation[1] * center.y;
    line.valid = true;
    return line;
}

LineModel RobustRefineLine(
    const std::vector<std::vector<EdgeCandidate>>& candidates,
    LineModel line,
    double distanceThreshold)
{
    for (int iteration = 0; iteration < 7 && line.valid; ++iteration) {
        std::vector<EdgeCandidate> selected = SelectInliers(candidates, line, distanceThreshold * 2.2);
        if (selected.size() < 2) {
            break;
        }
        std::vector<double> residuals;
        residuals.reserve(selected.size());
        for (const EdgeCandidate& candidate : selected) {
            residuals.push_back(PointLineDistance(line, candidate.point));
        }
        std::vector<double> residualCopy = residuals;
        const double median = Quantile(std::move(residualCopy), 0.5);
        const double scale = std::max(0.25, 1.4826 * median);
        std::vector<double> weights(residuals.size(), 0.0);
        for (size_t index = 0; index < residuals.size(); ++index) {
            const double u = residuals[index] / (4.685 * scale);
            if (u < 1.0) {
                const double oneMinus = 1.0 - u * u;
                weights[index] = oneMinus * oneMinus;
            }
        }
        LineModel refined = WeightedLineFit(selected, weights);
        if (!refined.valid) {
            break;
        }
        line = refined;
    }
    return line;
}

SideFit FitSide(
    const cv::Mat& gray,
    const cv::Point2f& start,
    const cv::Point2f& end,
    int caliperCount,
    double searchHalfWidth,
    double minContrast,
    int seed)
{
    SideFit fit;
    const cv::Point2f referenceDirection = end - start;
    const auto candidates = CollectCaliperCandidates(
        gray, start, end, caliperCount, searchHalfWidth, minContrast);
    std::vector<const EdgeCandidate*> flattened;
    for (const auto& caliper : candidates) {
        for (const EdgeCandidate& candidate : caliper) {
            flattened.push_back(&candidate);
        }
    }
    fit.quality.sampleCount = caliperCount;
    if (flattened.size() < 2) {
        return fit;
    }

    const double distanceThreshold = std::max(1.35, std::min(gray.cols, gray.rows) * 0.0016);
    LineModel best = LineFromPoints(start, end);
    double bestScore = -1.0;
    auto scoreLine = [&](const LineModel& line) {
        if (!line.valid || DirectionAgreement(line, referenceDirection) < std::cos(38.0 * CV_PI / 180.0)) {
            return -1.0;
        }
        const std::vector<EdgeCandidate> selected = SelectInliers(candidates, line, distanceThreshold);
        double strength = 0.0;
        double residual = 0.0;
        for (const EdgeCandidate& candidate : selected) {
            strength += std::min(1.0, candidate.strength / std::max(minContrast, 0.04));
            residual += PointLineDistance(line, candidate.point) / distanceThreshold;
        }
        const double coarseDeviation = 0.5 * (
            PointLineDistance(line, start) + PointLineDistance(line, end));
        const double coarsePriorPenalty = caliperCount * 0.75
            * Clamp01(coarseDeviation / std::max(1.0, searchHalfWidth));
        return selected.size() * 2.0 + strength * 0.35
            - residual * 0.20 - coarsePriorPenalty;
    };
    bestScore = scoreLine(best);

    std::mt19937 generator(static_cast<unsigned int>(seed));
    std::uniform_int_distribution<size_t> distribution(0, flattened.size() - 1);
    for (int iteration = 0; iteration < 260; ++iteration) {
        const EdgeCandidate* first = flattened[distribution(generator)];
        const EdgeCandidate* second = flattened[distribution(generator)];
        if (first->caliper == second->caliper || std::abs(first->caliper - second->caliper) < caliperCount / 5) {
            continue;
        }
        const LineModel line = LineFromPoints(first->point, second->point);
        const double score = scoreLine(line);
        if (score > bestScore) {
            bestScore = score;
            best = line;
        }
    }
    if (!best.valid) {
        return fit;
    }

    best = RobustRefineLine(candidates, best, distanceThreshold);
    std::vector<EdgeCandidate> inliers = SelectInliers(candidates, best, distanceThreshold);
    if (inliers.size() < 2) {
        return fit;
    }
    std::sort(inliers.begin(), inliers.end(), [](const EdgeCandidate& left, const EdgeCandidate& right) {
        return left.caliper < right.caliper;
    });

    double squaredResidual = 0.0;
    std::vector<float> strengths;
    strengths.reserve(inliers.size());
    int largestGap = inliers.front().caliper;
    for (size_t index = 0; index < inliers.size(); ++index) {
        const double residual = PointLineDistance(best, inliers[index].point);
        squaredResidual += residual * residual;
        strengths.push_back(static_cast<float>(inliers[index].strength));
        if (index > 0) {
            const int gap = inliers[index].caliper - inliers[index - 1].caliper - 1;
            largestGap = std::max(largestGap, gap);
        }
    }
    largestGap = std::max(largestGap, caliperCount - 1 - inliers.back().caliper);

    fit.line = best;
    fit.quality.inlierCount = static_cast<int>(inliers.size());
    fit.quality.inlierRatio = static_cast<double>(inliers.size()) / caliperCount;
    fit.quality.coverage = caliperCount > 1
        ? static_cast<double>(inliers.back().caliper - inliers.front().caliper) / (caliperCount - 1)
        : 0.0;
    fit.quality.contrastP10 = Quantile(std::move(strengths), 0.10);
    fit.quality.fitRms = std::sqrt(squaredResidual / inliers.size());
    fit.quality.maxGap = static_cast<double>(largestGap) / caliperCount;

    const double inlierScore = Clamp01((fit.quality.inlierRatio - 0.20) / 0.65);
    const double coverageScore = Clamp01((fit.quality.coverage - 0.40) / 0.55);
    const double contrastScore = Clamp01(fit.quality.contrastP10 / 0.12);
    const double rmsScore = std::exp(-fit.quality.fitRms / std::max(0.5, distanceThreshold * 0.75));
    const double gapScore = Clamp01((0.50 - fit.quality.maxGap) / 0.42);
    fit.quality.confidence = Clamp01(
        0.25 * inlierScore + 0.20 * coverageScore + 0.25 * contrastScore
        + 0.20 * rmsScore + 0.10 * gapScore);
    // A long opaque obstruction can create a very convincing parallel edge
    // inside the display. Every fit must therefore remain anchored to at least
    // one end of the coarse convex-hull side. This still permits a dark corner:
    // the visible end anchors the line while the missing end is extrapolated.
    const double coarseAnchorDeviation = std::min(
        PointLineDistance(best, start),
        PointLineDistance(best, end)) / std::max(1.0, searchHalfWidth);
    const int lastCaliper = std::max(1, caliperCount - 1);
    const bool strongEndpointSupport = inliers.front().caliper <= cvRound(lastCaliper * 0.22)
        || inliers.back().caliper >= cvRound(lastCaliper * 0.78);
    const bool usableEndpointSupport = inliers.front().caliper <= cvRound(lastCaliper * 0.28)
        || inliers.back().caliper >= cvRound(lastCaliper * 0.72);
    fit.anchored = coarseAnchorDeviation <= 0.35;
    fit.usable = inliers.size() >= static_cast<size_t>(std::max(4, caliperCount / 8))
        && fit.quality.coverage >= 0.08
        && coarseAnchorDeviation <= 0.55
        && usableEndpointSupport
        && fit.quality.contrastP10 >= minContrast * 0.55
        && fit.quality.fitRms <= distanceThreshold * 1.8;
    fit.accepted = inliers.size() >= static_cast<size_t>(std::max(7, caliperCount / 4))
        && fit.quality.coverage >= 0.22
        && fit.anchored
        && strongEndpointSupport
        && fit.quality.contrastP10 >= minContrast;
    return fit;
}

bool IntersectLines(const LineModel& first, const LineModel& second, cv::Point2f& intersection)
{
    const double determinant = first.equation[0] * second.equation[1]
        - second.equation[0] * first.equation[1];
    if (std::abs(determinant) < 1e-4) {
        return false;
    }
    intersection.x = static_cast<float>((first.equation[1] * second.equation[2]
        - second.equation[1] * first.equation[2]) / determinant);
    intersection.y = static_cast<float>((first.equation[2] * second.equation[0]
        - second.equation[2] * first.equation[0]) / determinant);
    return std::isfinite(intersection.x) && std::isfinite(intersection.y);
}

bool ValidGeometry(const std::array<cv::Point2f, 4>& corners, const cv::Size& size, const FindLuminousAreaV2Config& config)
{
    std::vector<cv::Point2f> contour(corners.begin(), corners.end());
    if (!cv::isContourConvex(contour)) {
        return false;
    }
    const double areaRatio = std::abs(SignedArea(corners)) / static_cast<double>(size.area());
    if (areaRatio < config.minAreaRatio * 0.65 || areaRatio > std::min(0.999, config.maxAreaRatio * 1.10)) {
        return false;
    }
    const double margin = std::min(size.width, size.height) * (config.allowBorder ? 0.12 : 0.08);
    for (int side = 0; side < kSideCount; ++side) {
        if (cv::norm(corners[(side + 1) % kSideCount] - corners[side]) < 7.0) {
            return false;
        }
        if (corners[side].x < -margin || corners[side].y < -margin
            || corners[side].x > size.width - 1 + margin || corners[side].y > size.height - 1 + margin) {
            return false;
        }
    }
    return true;
}

bool SimilarDetection(
    const FindLuminousAreaV2Result& left,
    const FindLuminousAreaV2Result& right)
{
    if (!left.hasCorners || !right.hasCorners) {
        return false;
    }
    std::vector<cv::Point2f> leftContour(left.corners.begin(), left.corners.end());
    std::vector<cv::Point2f> rightContour(right.corners.begin(), right.corners.end());
    const double leftArea = std::abs(SignedArea(left.corners));
    const double rightArea = std::abs(SignedArea(right.corners));
    std::vector<cv::Point2f> intersection;
    const double intersectionArea = cv::intersectConvexConvex(
        leftContour, rightContour, intersection, true);
    const double unionArea = leftArea + rightArea - intersectionArea;
    const double smallerArea = std::min(leftArea, rightArea);
    if (smallerArea > kEpsilon && intersectionArea / smallerArea >= 0.90) {
        return true;
    }
    if (unionArea > kEpsilon && intersectionArea / unionArea >= 0.45) {
        return true;
    }

    double meanCornerDistance = 0.0;
    for (int corner = 0; corner < kSideCount; ++corner) {
        meanCornerDistance += cv::norm(left.corners[corner] - right.corners[corner]) * 0.25;
    }
    return meanCornerDistance <= std::sqrt(std::max(1.0, std::min(leftArea, rightArea))) * 0.10;
}

void RestoreInputScale(FindLuminousAreaV2Result& result, double processingScale)
{
    if (processingScale >= 1.0) {
        return;
    }
    if (result.hasCorners) {
        for (cv::Point2f& corner : result.corners) {
            corner *= static_cast<float>(1.0 / processingScale);
        }
    }
    for (LuminousSideQuality& side : result.sideQuality) {
        side.fitRms /= processingScale;
    }
}

bool CoincidesWithImageBorder(
    const cv::Point2f& start,
    const cv::Point2f& end,
    const cv::Size& size)
{
    constexpr float tolerance = 2.5f;
    const auto nearLeft = [&](const cv::Point2f& point) { return point.x <= tolerance; };
    const auto nearRight = [&](const cv::Point2f& point) { return point.x >= size.width - 1.0f - tolerance; };
    const auto nearTop = [&](const cv::Point2f& point) { return point.y <= tolerance; };
    const auto nearBottom = [&](const cv::Point2f& point) { return point.y >= size.height - 1.0f - tolerance; };
    return (nearLeft(start) && nearLeft(end))
        || (nearRight(start) && nearRight(end))
        || (nearTop(start) && nearTop(end))
        || (nearBottom(start) && nearBottom(end));
}

FindLuminousAreaV2Result EvaluateCandidate(
    const cv::Mat& gray,
    const CoarseCandidate& coarse,
    const FindLuminousAreaV2Config& config,
    int candidateIndex)
{
    FindLuminousAreaV2Result result;
    const double averageShortSide = 0.5 * (
        cv::norm(coarse.quad[3] - coarse.quad[0])
        + cv::norm(coarse.quad[2] - coarse.quad[1]));
    const double searchHalfWidth = std::clamp(
        averageShortSide * config.searchWidthRatio,
        6.0,
        std::min(gray.cols, gray.rows) * 0.30);

    std::array<SideFit, 4> fits;
    std::array<LineModel, 4> selectedLines;
    int strongSideCount = 0;
    std::array<bool, 4> denseSides{};
    int denseSideCount = 0;
    int missingSideCount = 0;
    static constexpr std::array<const char*, 4> sideNames{ "Top", "Right", "Bottom", "Left" };
    for (int side = 0; side < kSideCount; ++side) {
        fits[side] = FitSide(
            gray,
            coarse.quad[side],
            coarse.quad[(side + 1) % kSideCount],
            config.caliperCount,
            searchHalfWidth,
            config.minEdgeContrast,
            0x51A7 + candidateIndex * 101 + side * 7919);
        result.sideQuality[side] = fits[side].quality;
        if (fits[side].accepted) {
            ++strongSideCount;
        }
        denseSides[side] = fits[side].line.valid
            && (fits[side].accepted || fits[side].usable)
            && fits[side].quality.inlierRatio >= 0.70
            && fits[side].quality.coverage >= 0.45
            && fits[side].quality.fitRms <= std::max(
                0.45,
                cv::norm(coarse.quad[(side + 1) % kSideCount] - coarse.quad[side]) * 0.015);
        denseSideCount += denseSides[side] ? 1 : 0;
        if (fits[side].line.valid && (fits[side].accepted || fits[side].usable)) {
            selectedLines[side] = fits[side].line;
            if (!fits[side].accepted) {
                result.warnings.push_back(std::string("Weak") + sideNames[side] + "Side");
            }
        }
        else {
            ++missingSideCount;
        }
    }

    bool hasAdjacentDenseSides = false;
    for (int side = 0; side < kSideCount; ++side) {
        hasAdjacentDenseSides = hasAdjacentDenseSides
            || (denseSides[side] && denseSides[(side + 1) % kSideCount]);
    }
    if (!hasAdjacentDenseSides) {
        result.failureReason = "InsufficientIndependentGeometry";
        result.warnings.push_back("SparseIndependentLineEvidence");
        return result;
    }

    // A threshold-stable coarse quadrilateral can complete one weak side, or
    // two adjacent weak sides when two non-parallel sides have dense measured
    // support. Two opposite measured sides alone remain under-constrained.
    // Never promote the image border itself to a display edge.
    const int coarseScaleVotes = CountBits(coarse.sigmaMask);
    const int maximumInferredSides = coarseScaleVotes >= 3 ? 2 : 1;
    int inferredSideCount = 0;
    const int initiallyMissingSideCount = missingSideCount;
    if (missingSideCount <= maximumInferredSides) {
        for (int side = 0; side < kSideCount; ++side) {
            if (selectedLines[side].valid) {
                continue;
            }
            const bool recoverableFineLine = fits[side].line.valid
                && fits[side].anchored
                && fits[side].quality.inlierCount >= 3
                && fits[side].quality.coverage >= 0.15
                && fits[side].quality.contrastP10 >= config.minEdgeContrast * 0.75;
            if (recoverableFineLine) {
                selectedLines[side] = fits[side].line;
                result.warnings.push_back(std::string("Inferred") + sideNames[side] + "Side");
                --missingSideCount;
                ++inferredSideCount;
                continue;
            }
            const bool coincidesWithBorder = CoincidesWithImageBorder(
                coarse.quad[side], coarse.quad[(side + 1) % kSideCount], gray.size());
            const bool supportedBorderInference = config.allowBorder
                && initiallyMissingSideCount == 1
                && denseSideCount >= 3
                && coarseScaleVotes >= 2;
            if (coincidesWithBorder && !supportedBorderInference) {
                continue;
            }
            selectedLines[side] = LineFromPoints(
                coarse.quad[side], coarse.quad[(side + 1) % kSideCount]);
            if (selectedLines[side].valid) {
                result.warnings.push_back(std::string("Inferred") + sideNames[side] + "Side");
                --missingSideCount;
                ++inferredSideCount;
            }
        }
    }
    if (missingSideCount != 0) {
        result.failureReason = "InsufficientSideSupport";
        for (int side = 0; side < kSideCount; ++side) {
            if (!selectedLines[side].valid) {
                result.warnings.push_back(std::string("Weak") + sideNames[side] + "Side");
            }
        }
        return result;
    }

    std::array<cv::Point2f, 4> corners{};
    if (!IntersectLines(selectedLines[3], selectedLines[0], corners[0])
        || !IntersectLines(selectedLines[0], selectedLines[1], corners[1])
        || !IntersectLines(selectedLines[1], selectedLines[2], corners[2])
        || !IntersectLines(selectedLines[2], selectedLines[3], corners[3])) {
        result.failureReason = "UnstableCorners";
        return result;
    }
    if (!ValidGeometry(corners, gray.size(), config)) {
        result.failureReason = "InvalidGeometry";
        return result;
    }

    // Fine line fitting can move a near-45-degree corner just across the
    // x+y tie used by the public LT contract. Canonicalize the refined result,
    // rotating side diagnostics with it so Corners and SideQuality remain in
    // the same LT/RT/RB/LB and top/right/bottom/left frame.
    const auto refinedLeftTop = std::min_element(
        corners.begin(),
        corners.end(),
        [](const cv::Point2f& left, const cv::Point2f& right) {
            const double leftScore = static_cast<double>(left.x) + left.y;
            const double rightScore = static_cast<double>(right.x) + right.y;
            return leftScore == rightScore ? left.y < right.y : leftScore < rightScore;
        });
    const int sideRotation = static_cast<int>(std::distance(corners.begin(), refinedLeftTop));
    if (sideRotation != 0) {
        std::rotate(corners.begin(), refinedLeftTop, corners.end());
        std::rotate(fits.begin(), fits.begin() + sideRotation, fits.end());
        std::rotate(
            result.sideQuality.begin(),
            result.sideQuality.begin() + sideRotation,
            result.sideQuality.end());
        for (std::string& warning : result.warnings) {
            bool renamed = false;
            for (int oldSide = 0; oldSide < kSideCount && !renamed; ++oldSide) {
                const int newSide = (oldSide - sideRotation + kSideCount) % kSideCount;
                for (const char* prefix : { "Weak", "Inferred" }) {
                    if (warning == std::string(prefix) + sideNames[oldSide] + "Side") {
                        warning = std::string(prefix) + sideNames[newSide] + "Side";
                        renamed = true;
                        break;
                    }
                }
            }
        }
    }

    double minimumSideConfidence = 1.0;
    double meanSideConfidence = 0.0;
    for (const SideFit& fit : fits) {
        minimumSideConfidence = std::min(minimumSideConfidence, fit.quality.confidence);
        meanSideConfidence += fit.quality.confidence * 0.25;
    }
    for (int side = 0; side < kSideCount; ++side) {
        if (fits[side].quality.coverage < 0.65 || fits[side].quality.maxGap > 0.35) {
            result.warnings.push_back(std::string("Partial") + sideNames[side] + "Support");
        }
    }
    const double coarseContrastScore = Clamp01(coarse.contrast / 0.15);
    const double strongSideScore = static_cast<double>(strongSideCount) / kSideCount;
    result.confidence = Clamp01(
        0.15 * minimumSideConfidence + 0.50 * meanSideConfidence
        + 0.20 * coarseContrastScore + 0.15 * strongSideScore);
    if (inferredSideCount == 1) {
        result.confidence *= 0.82;
    }
    else if (inferredSideCount == 2) {
        result.confidence *= 0.68;
    }
    result.corners = corners;
    result.hasCorners = true;
    if (coarse.touchesBorder) {
        result.warnings.push_back("CandidateTouchesImageBorder");
    }
    if (result.confidence < config.minConfidence) {
        result.failureReason = "LowConfidence";
        return result;
    }

    result.success = true;
    return result;
}

template<typename T>
bool ReadNumber(const nlohmann::json& json, const char* name, T minimum, T maximum, T& output, std::string& error)
{
    if (!json.contains(name)) {
        return true;
    }
    if (!json.at(name).is_number()) {
        error = std::string(name) + " must be numeric";
        return false;
    }
    const double value = json.at(name).get<double>();
    if (!std::isfinite(value) || value < static_cast<double>(minimum) || value > static_cast<double>(maximum)) {
        error = std::string(name) + " is outside its supported range";
        return false;
    }
    output = static_cast<T>(value);
    return true;
}

} // namespace

bool ParseFindLuminousAreaV2Config(
    const nlohmann::json& json,
    FindLuminousAreaV2Config& config,
    std::string& error)
{
    error.clear();
    if (!json.is_object()) {
        error = "configuration must be a JSON object";
        return false;
    }
    if (!ReadNumber(json, "MinConfidence", 0.0, 1.0, config.minConfidence, error)
        || !ReadNumber(json, "MinAreaRatio", 0.00005, 0.50, config.minAreaRatio, error)
        || !ReadNumber(json, "MaxAreaRatio", 0.01, 0.999, config.maxAreaRatio, error)
        || !ReadNumber(json, "SearchWidthRatio", 0.03, 0.35, config.searchWidthRatio, error)
        || !ReadNumber(json, "MinEdgeContrast", 0.005, 0.50, config.minEdgeContrast, error)
        || !ReadNumber(json, "CaliperCount", 12, 128, config.caliperCount, error)
        || !ReadNumber(json, "MaxProcessingSize", 320, 4096, config.maxProcessingSize, error)) {
        return false;
    }
    if (json.contains("AllowBorder")) {
        if (!json.at("AllowBorder").is_boolean()) {
            error = "AllowBorder must be boolean";
            return false;
        }
        config.allowBorder = json.at("AllowBorder").get<bool>();
    }
    if (config.minAreaRatio >= config.maxAreaRatio) {
        error = "MinAreaRatio must be smaller than MaxAreaRatio";
        return false;
    }
    return true;
}

FindLuminousAreaV2Result FindLuminousAreaV2(
    const cv::Mat& image,
    const FindLuminousAreaV2Config& config)
{
    FindLuminousAreaV2Result result;
    const double processingScale = std::min(
        1.0,
        static_cast<double>(config.maxProcessingSize) / std::max(image.cols, image.rows));
    cv::Mat processingImage;
    if (processingScale < 1.0) {
        // Downsample before color conversion and CV_32F normalization. A typical
        // 61 MP, 16-bit RGB frame otherwise needs another ~244 MB just for the
        // temporary full-resolution float gray image.
        cv::resize(image, processingImage, cv::Size(), processingScale, processingScale, cv::INTER_AREA);
    }
    else {
        processingImage = image;
    }

    cv::Mat normalized;
    double sourceRange = 0.0;
    if (!NormalizeGray(processingImage, normalized, sourceRange)) {
        result.failureReason = "UnsupportedImage";
        return result;
    }
    if (normalized.empty() || sourceRange <= 0.0) {
        result.failureReason = "NoSignal";
        return result;
    }

    FindLuminousAreaV2Config scaledConfig = config;
    std::vector<CoarseCandidate> coarseCandidates = FindCoarseCandidates(normalized, scaledConfig);
    if (coarseCandidates.empty()) {
        result.failureReason = "NoCandidate";
        return result;
    }

    FindLuminousAreaV2Result bestRejected;
    bestRejected.failureReason = "InsufficientSideSupport";
    double bestRejectedScore = -1.0;
    std::vector<SuccessfulCandidate> successfulCandidates;
    for (size_t index = 0; index < coarseCandidates.size(); ++index) {
        FindLuminousAreaV2Result candidate = EvaluateCandidate(
            normalized, coarseCandidates[index], scaledConfig, static_cast<int>(index));
        double candidateScore = candidate.confidence;
        if (!candidate.hasCorners) {
            candidateScore = 0.0;
            for (const LuminousSideQuality& side : candidate.sideQuality) {
                candidateScore += side.confidence * 0.25;
            }
        }
        if (candidate.success) {
            SuccessfulCandidate evaluated;
            const double areaScore = Clamp01(
                std::log1p(coarseCandidates[index].areaRatio * 200.0) / std::log(21.0));
            const int scaleVotes = CountBits(coarseCandidates[index].sigmaMask);
            const double scaleStability = static_cast<double>(scaleVotes) / 3.0;
            const double thresholdPersistence = Clamp01(
                static_cast<double>(coarseCandidates[index].sourceVotes - scaleVotes) / 6.0);
            const double stabilityScore = 0.85 * scaleStability + 0.15 * thresholdPersistence;
            // True outer boundaries tend to persist across blur/threshold
            // scales, whereas an iso-brightness slice through a display
            // gradient moves substantially. Stability and full-area support
            // therefore participate directly in final candidate selection.
            evaluated.rank = 0.50 * candidate.confidence
                + 0.23 * areaScore
                + 0.22 * stabilityScore
                + 0.05 * Clamp01(coarseCandidates[index].score / 4.0);
            evaluated.areaRatio = coarseCandidates[index].areaRatio;
            evaluated.scaleVotes = scaleVotes;
            evaluated.result = std::move(candidate);

            auto sameDetection = std::find_if(
                successfulCandidates.begin(),
                successfulCandidates.end(),
                [&](const SuccessfulCandidate& existing) {
                    return SimilarDetection(existing.result, evaluated.result);
                });
            if (sameDetection == successfulCandidates.end()) {
                successfulCandidates.push_back(std::move(evaluated));
            }
            else {
                const auto inferredSideCount = [](const SuccessfulCandidate& value) {
                    return static_cast<int>(std::count_if(
                        value.result.warnings.begin(),
                        value.result.warnings.end(),
                        [](const std::string& warning) { return warning.rfind("Inferred", 0) == 0; }));
                };
                const auto isTrustedStableOuter = [&](
                    const SuccessfulCandidate& outer,
                    const SuccessfulCandidate& inner) {
                    const int inferredSides = inferredSideCount(outer);
                    if (outer.result.confidence < inner.result.confidence - 0.45) {
                        return false;
                    }
                    if (inferredSides == 0) {
                        return outer.areaRatio >= inner.areaRatio * 1.12
                            && outer.scaleVotes >= 2;
                    }
                    if (inferredSides != 1
                        || outer.areaRatio < inner.areaRatio * 1.35
                        || outer.scaleVotes < 3) {
                        return false;
                    }
                    const int measuredDenseSides = static_cast<int>(std::count_if(
                        outer.result.sideQuality.begin(),
                        outer.result.sideQuality.end(),
                        [](const LuminousSideQuality& side) {
                            return side.inlierRatio >= 0.70
                                && side.coverage >= 0.45
                                && side.confidence >= 0.35;
                        }));
                    return measuredDenseSides >= 3;
                };
                const bool evaluatedStableOuter = isTrustedStableOuter(evaluated, *sameDetection);
                const bool existingStableOuter = isTrustedStableOuter(*sameDetection, evaluated);
                if (evaluatedStableOuter || (!existingStableOuter && evaluated.rank > sameDetection->rank)) {
                    *sameDetection = std::move(evaluated);
                }
            }
            continue;
        }
        const bool preferGeometricResult = candidate.hasCorners && !bestRejected.hasCorners;
        const bool comparableResult = candidate.hasCorners == bestRejected.hasCorners;
        if (preferGeometricResult || (comparableResult && candidateScore > bestRejectedScore)) {
            bestRejectedScore = candidateScore;
            bestRejected = std::move(candidate);
        }
    }

    if (!successfulCandidates.empty()) {
        std::sort(
            successfulCandidates.begin(),
            successfulCandidates.end(),
            [](const SuccessfulCandidate& left, const SuccessfulCandidate& right) {
                return left.rank == right.rank
                    ? left.areaRatio > right.areaRatio
                    : left.rank > right.rank;
            });
        const SuccessfulCandidate& best = successfulCandidates.front();
        FindLuminousAreaV2Result selected = best.result;
        if (successfulCandidates.size() > 1) {
            const SuccessfulCandidate& second = successfulCandidates[1];
            const double relativeArea = std::min(best.areaRatio, second.areaRatio)
                / std::max(kEpsilon, std::max(best.areaRatio, second.areaRatio));
            if (relativeArea >= 0.50 && second.rank >= best.rank - 0.10) {
                selected.warnings.push_back("AmbiguousCandidates");
                selected.warnings.push_back("MultipleComparableCandidates");
                selected.warnings.push_back(
                    "ComparableCandidateCount=" + std::to_string(successfulCandidates.size()));
                selected.confidence *= 0.90;
            }
        }
        if (selected.confidence < scaledConfig.minConfidence) {
            selected.success = false;
            selected.failureReason = "LowConfidence";
        }
        RestoreInputScale(selected, processingScale);
        return selected;
    }

    RestoreInputScale(bestRejected, processingScale);
    return bestRejected;
}

} // namespace cvnative::luminous
