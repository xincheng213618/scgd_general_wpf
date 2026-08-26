#ifndef NOMINMAX
#define NOMINMAX
#endif

#include "pattern_cross.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <limits>
#include <numeric>
#include <utility>
#include <vector>

#include <opencv2/imgproc.hpp>

namespace cvnative::find_cross
{
namespace
{

constexpr double kRadiansToDegrees = 180.0 / CV_PI;
constexpr double kDegreesToRadians = CV_PI / 180.0;

struct ProjectionPeak
{
    double coordinate = 0.0;
    double value = 0.0;
};

struct ArmSupport
{
    double coverage = 0.0;
    double contrast = 0.0;
};

struct CoarseCandidate
{
    bool valid = false;
    double score = 0.0;
    double confidence = 0.0;
    double angleDegrees = 0.0;
    cv::Point2d sourceCenter{};
    PatternPolarity polarity = PatternPolarity::Bright;
    std::array<ArmSupport, 4> arms{};
};

struct CoarseCandidatePair
{
    CoarseCandidate best{};
    CoarseCandidate second{};
};

struct AxisSample
{
    cv::Point2d point{};
    double contrast = 0.0;
    int armIndex = 0;
};

struct AxisFit
{
    bool valid = false;
    cv::Point2d point{};
    cv::Point2d direction{};
    std::vector<AxisSample> inliers;
    std::array<PatternArmQuality, 2> armQuality{};
    double rms = 0.0;
};

double NormalizeAxisAngle(double angleDegrees)
{
    while (angleDegrees >= 90.0) angleDegrees -= 180.0;
    while (angleDegrees < -90.0) angleDegrees += 180.0;
    return angleDegrees;
}

double Percentile(std::vector<double> values, double fraction)
{
    if (values.empty()) return 0.0;
    fraction = std::clamp(fraction, 0.0, 1.0);
    const size_t index = static_cast<size_t>(std::lround(
        fraction * static_cast<double>(values.size() - 1)));
    std::nth_element(values.begin(), values.begin() + index, values.end());
    return values[index];
}

cv::Mat ConvertToGrayFloat(const cv::Mat& input)
{
    cv::Mat gray;
    if (input.channels() == 1) {
        gray = input;
    }
    else if (input.channels() == 3) {
        cv::cvtColor(input, gray, cv::COLOR_BGR2GRAY);
    }
    else if (input.channels() == 4) {
        cv::cvtColor(input, gray, cv::COLOR_BGRA2GRAY);
    }
    else {
        std::vector<cv::Mat> channels;
        cv::split(input, channels);
        if (channels.empty()) return {};
        channels.front().convertTo(gray, CV_32F);
        for (size_t index = 1; index < channels.size(); ++index) {
            cv::Mat channel;
            channels[index].convertTo(channel, CV_32F);
            gray += channel;
        }
        gray *= 1.0 / static_cast<double>(channels.size());
    }

    cv::Mat output;
    switch (gray.depth()) {
    case CV_8U:
        gray.convertTo(output, CV_32F, 1.0 / 255.0);
        break;
    case CV_16U:
        gray.convertTo(output, CV_32F, 1.0 / 65535.0);
        break;
    case CV_8S:
        gray.convertTo(output, CV_32F, 1.0 / 255.0, 128.0 / 255.0);
        break;
    case CV_16S:
        gray.convertTo(output, CV_32F, 1.0 / 65535.0, 32768.0 / 65535.0);
        break;
    default:
        gray.convertTo(output, CV_32F);
        double minimum = 0.0;
        double maximum = 0.0;
        cv::minMaxLoc(output, &minimum, &maximum);
        if (!std::isfinite(minimum) || !std::isfinite(maximum)) return {};
        if (minimum < 0.0 || maximum > 1.0) {
            const double range = maximum - minimum;
            if (range <= std::numeric_limits<double>::epsilon()) {
                output.setTo(0.0f);
            }
            else {
                output.convertTo(output, CV_32F, 1.0 / range, -minimum / range);
            }
        }
        break;
    }
    cv::patchNaNs(output, 0.0);
    return output;
}

cv::Mat BuildSignedResponse(const cv::Mat& gray, PatternPolarity polarity)
{
    const double sigma = std::clamp(
        std::min(gray.rows, gray.cols) * 0.012,
        3.0,
        32.0);
    cv::Mat background;
    cv::GaussianBlur(gray, background, cv::Size(), sigma, sigma, cv::BORDER_REPLICATE);
    cv::Mat residual = polarity == PatternPolarity::Dark
        ? background - gray
        : gray - background;
    cv::max(residual, 0.0, residual);
    cv::GaussianBlur(residual, residual, cv::Size(), 0.7, 0.7, cv::BORDER_REPLICATE);
    return residual;
}

std::vector<double> ReduceProjection(const cv::Mat& image, bool rows, int margin)
{
    const cv::Rect range = rows
        ? cv::Rect(margin, 0, image.cols - 2 * margin, image.rows)
        : cv::Rect(0, margin, image.cols, image.rows - 2 * margin);
    cv::Mat reduced;
    cv::reduce(image(range), reduced, rows ? 1 : 0, cv::REDUCE_AVG, CV_32F);
    std::vector<double> values(rows ? image.rows : image.cols, 0.0);
    if (rows) {
        for (int index = 0; index < reduced.rows; ++index) {
            values[index] = reduced.at<float>(index, 0);
        }
    }
    else {
        for (int index = 0; index < reduced.cols; ++index) {
            values[index] = reduced.at<float>(0, index);
        }
    }
    return values;
}

std::vector<ProjectionPeak> FindProjectionPeaks(
    const std::vector<double>& values,
    int begin,
    int end,
    int minimumSeparation,
    int maximumCount)
{
    std::vector<int> order;
    for (int index = begin; index < end; ++index) order.push_back(index);
    std::sort(order.begin(), order.end(), [&](int left, int right) {
        return values[left] > values[right];
    });

    std::vector<ProjectionPeak> peaks;
    for (int index : order) {
        if (std::any_of(peaks.begin(), peaks.end(), [&](const ProjectionPeak& peak) {
            return std::abs(peak.coordinate - index) < minimumSeparation;
        })) {
            continue;
        }
        const int low = std::max(begin, index - 2);
        const int high = std::min(end - 1, index + 2);
        double baseline = std::numeric_limits<double>::infinity();
        for (int sample = std::max(begin, index - 12);
            sample <= std::min(end - 1, index + 12); ++sample) {
            baseline = std::min(baseline, values[sample]);
        }
        double weightedCoordinate = 0.0;
        double weightSum = 0.0;
        for (int sample = low; sample <= high; ++sample) {
            const double weight = std::max(0.0, values[sample] - baseline);
            weightedCoordinate += sample * weight;
            weightSum += weight;
        }
        peaks.push_back({
            weightSum > 0.0 ? weightedCoordinate / weightSum : static_cast<double>(index),
            values[index]
        });
        if (static_cast<int>(peaks.size()) >= maximumCount) break;
    }
    return peaks;
}

double BilinearSample(const cv::Mat& image, double x, double y)
{
    if (x < 0.0 || y < 0.0 || x > image.cols - 1.0 || y > image.rows - 1.0) return 0.0;
    const int left = static_cast<int>(std::floor(x));
    const int top = static_cast<int>(std::floor(y));
    const int right = std::min(left + 1, image.cols - 1);
    const int bottom = std::min(top + 1, image.rows - 1);
    const double fx = x - left;
    const double fy = y - top;
    const double upper = image.at<float>(top, left) * (1.0 - fx)
        + image.at<float>(top, right) * fx;
    const double lower = image.at<float>(bottom, left) * (1.0 - fx)
        + image.at<float>(bottom, right) * fx;
    return upper * (1.0 - fy) + lower * fy;
}

ArmSupport MeasureArm(
    const cv::Mat& aligned,
    const cv::Point2d& center,
    const cv::Point2d& direction,
    double requiredLength,
    int halfBand,
    double supportThreshold)
{
    const cv::Point2d normal(-direction.y, direction.x);
    const int sampleCount = std::max(8, static_cast<int>(std::floor(requiredLength)));
    std::vector<double> profile;
    profile.reserve(sampleCount);
    const double gap = std::max(3.0, halfBand * 1.5);
    for (int index = 0; index < sampleCount; ++index) {
        const cv::Point2d base = center + direction * (gap + index + 0.5);
        double sum = 0.0;
        int count = 0;
        for (int offset = -halfBand; offset <= halfBand; ++offset) {
            const cv::Point2d point = base + normal * offset;
            if (point.x >= 0.0 && point.y >= 0.0
                && point.x <= aligned.cols - 1.0 && point.y <= aligned.rows - 1.0) {
                sum += BilinearSample(aligned, point.x, point.y);
                count++;
            }
        }
        profile.push_back(count > 0 ? sum / count : 0.0);
    }
    const int supported = static_cast<int>(std::count_if(
        profile.begin(), profile.end(),
        [&](double value) { return value >= supportThreshold; }));
    return {
        static_cast<double>(supported) / std::max<size_t>(1, profile.size()),
        Percentile(profile, 0.35)
    };
}

cv::Point2d TransformPoint(const cv::Mat& transform, const cv::Point2d& point)
{
    return {
        transform.at<double>(0, 0) * point.x
            + transform.at<double>(0, 1) * point.y
            + transform.at<double>(0, 2),
        transform.at<double>(1, 0) * point.x
            + transform.at<double>(1, 1) * point.y
            + transform.at<double>(1, 2)
    };
}

CoarseCandidatePair EvaluateAngle(
    const cv::Mat& response,
    double angleDegrees,
    PatternPolarity polarity,
    const PatternCrossConfig& config,
    double imageScale)
{
    CoarseCandidatePair result;
    std::vector<CoarseCandidate> candidates;

    cv::Mat sourceToAligned = cv::getRotationMatrix2D(
        cv::Point2f((response.cols - 1) * 0.5f, (response.rows - 1) * 0.5f),
        angleDegrees,
        1.0);
    cv::Mat aligned;
    cv::warpAffine(
        response, aligned, sourceToAligned, response.size(), cv::INTER_LINEAR,
        cv::BORDER_CONSTANT, cv::Scalar(0));

    const double requiredLength = std::max(8.0, config.minArmLengthPixels * imageScale);
    const int margin = std::max(
        static_cast<int>(std::ceil(requiredLength + 8.0)),
        static_cast<int>(std::ceil(std::min(aligned.rows, aligned.cols) * 0.055)));
    if (aligned.cols <= margin * 2 + 8 || aligned.rows <= margin * 2 + 8) return result;

    const std::vector<double> rowProjection = ReduceProjection(aligned, true, margin);
    const std::vector<double> columnProjection = ReduceProjection(aligned, false, margin);
    const int separation = std::max(4, static_cast<int>(std::lround(requiredLength * 0.30)));
    const std::vector<ProjectionPeak> rows = FindProjectionPeaks(
        rowProjection, margin, aligned.rows - margin, separation, 8);
    const std::vector<ProjectionPeak> columns = FindProjectionPeaks(
        columnProjection, margin, aligned.cols - margin, separation, 8);
    if (rows.empty() || columns.empty()) return result;

    cv::Scalar mean;
    cv::Scalar standardDeviation;
    cv::meanStdDev(aligned, mean, standardDeviation);
    const double supportThreshold = std::max(
        config.minContrast * 0.22,
        mean[0] + standardDeviation[0] * 0.20);
    cv::Mat alignedToSource;
    cv::invertAffineTransform(sourceToAligned, alignedToSource);
    const std::array<cv::Point2d, 4> directions{
        cv::Point2d(-1.0, 0.0), cv::Point2d(1.0, 0.0),
        cv::Point2d(0.0, -1.0), cv::Point2d(0.0, 1.0)
    };

    for (const ProjectionPeak& row : rows) {
        for (const ProjectionPeak& column : columns) {
            const cv::Point2d center(column.coordinate, row.coordinate);
            std::array<ArmSupport, 4> arms{};
            std::vector<double> contrasts;
            double minimumCoverage = 1.0;
            double maximumContrast = 0.0;
            for (size_t index = 0; index < directions.size(); ++index) {
                arms[index] = MeasureArm(
                    aligned, center, directions[index], requiredLength, 2, supportThreshold);
                contrasts.push_back(arms[index].contrast);
                minimumCoverage = std::min(minimumCoverage, arms[index].coverage);
                maximumContrast = std::max(maximumContrast, arms[index].contrast);
            }
            const double minimumContrast = *std::min_element(contrasts.begin(), contrasts.end());
            const double balance = maximumContrast > 0.0 ? minimumContrast / maximumContrast : 0.0;
            const bool valid = minimumCoverage >= config.minArmCoverage
                && minimumContrast >= config.minContrast * 0.18;
            const double contrastScore = std::clamp(
                minimumContrast / std::max(config.minContrast, 1e-9), 0.0, 1.0);
            const double confidence = std::clamp(
                0.45 * contrastScore + 0.35 * minimumCoverage + 0.20 * balance,
                0.0, 1.0);
            const double score = minimumContrast
                * (0.35 + 0.65 * minimumCoverage)
                * (0.35 + 0.65 * balance)
                * (1.0 + row.value + column.value);
            CoarseCandidate candidate;
            candidate.valid = valid;
            candidate.score = score;
            candidate.confidence = confidence;
            candidate.angleDegrees = angleDegrees;
            candidate.sourceCenter = TransformPoint(alignedToSource, center);
            candidate.polarity = polarity;
            candidate.arms = arms;
            candidates.push_back(std::move(candidate));
        }
    }
    if (candidates.empty()) return result;

    // Preserve the strongest spatially distinct runner-up at each angle. The
    // previous single-candidate return discarded a second complete Pattern
    // before the global ambiguity check could ever see it.
    std::sort(candidates.begin(), candidates.end(), [](const auto& left, const auto& right) {
        if (left.valid != right.valid) return left.valid;
        return left.score > right.score;
    });
    result.best = candidates.front();
    const double minimumSeparation = std::max(4.0, config.minArmLengthPixels * imageScale);
    for (size_t index = 1; index < candidates.size(); ++index) {
        if (!candidates[index].valid) break;
        if (cv::norm(candidates[index].sourceCenter - result.best.sourceCenter)
            > minimumSeparation) {
            result.second = candidates[index];
            break;
        }
    }
    return result;
}

std::vector<PatternPolarity> ResolvePolarities(PatternPolarity polarity)
{
    return polarity == PatternPolarity::Auto
        ? std::vector<PatternPolarity>{ PatternPolarity::Bright, PatternPolarity::Dark }
        : std::vector<PatternPolarity>{ polarity };
}

const char* PolarityName(PatternPolarity polarity)
{
    if (polarity == PatternPolarity::Dark) return "Dark";
    if (polarity == PatternPolarity::Bright) return "Bright";
    return "Auto";
}

bool IntersectLines(
    const cv::Point2d& firstPoint,
    const cv::Point2d& firstDirection,
    const cv::Point2d& secondPoint,
    const cv::Point2d& secondDirection,
    cv::Point2d& intersection)
{
    const double denominator = firstDirection.x * secondDirection.y
        - firstDirection.y * secondDirection.x;
    if (std::abs(denominator) <= 1e-9) return false;
    const cv::Point2d offset = secondPoint - firstPoint;
    const double parameter = (offset.x * secondDirection.y - offset.y * secondDirection.x)
        / denominator;
    intersection = firstPoint + firstDirection * parameter;
    return std::isfinite(intersection.x) && std::isfinite(intersection.y);
}

double DistanceToLine(
    const cv::Point2d& point,
    const cv::Point2d& linePoint,
    const cv::Point2d& lineDirection)
{
    const cv::Point2d delta = point - linePoint;
    return std::abs(delta.x * lineDirection.y - delta.y * lineDirection.x);
}

bool FitAxisRobust(
    const std::vector<AxisSample>& samples,
    const cv::Point2d& expectedDirection,
    AxisFit& fit)
{
    if (samples.size() < 12) return false;
    std::vector<int> inlierIndices(samples.size());
    std::iota(inlierIndices.begin(), inlierIndices.end(), 0);
    cv::Vec4f fitted{};
    for (int iteration = 0; iteration < 4; ++iteration) {
        std::vector<cv::Point2f> points;
        for (int index : inlierIndices) {
            points.emplace_back(
                static_cast<float>(samples[index].point.x),
                static_cast<float>(samples[index].point.y));
        }
        if (points.size() < 8) return false;
        cv::fitLine(points, fitted, cv::DIST_HUBER, 0.0, 0.01, 0.01);
        cv::Point2d direction(fitted[0], fitted[1]);
        if (direction.dot(expectedDirection) < 0.0) direction *= -1.0;
        const cv::Point2d point(fitted[2], fitted[3]);
        std::vector<double> distances;
        for (int index : inlierIndices) {
            distances.push_back(DistanceToLine(samples[index].point, point, direction));
        }
        const double median = Percentile(distances, 0.5);
        std::vector<double> deviations;
        for (double distance : distances) deviations.push_back(std::abs(distance - median));
        const double mad = Percentile(deviations, 0.5);
        const double threshold = std::max(0.45, median + 3.5 * 1.4826 * mad);
        std::vector<int> next;
        for (size_t index = 0; index < inlierIndices.size(); ++index) {
            if (distances[index] <= threshold) next.push_back(inlierIndices[index]);
        }
        if (next.size() == inlierIndices.size()) break;
        inlierIndices = std::move(next);
    }
    if (inlierIndices.size() < 8) return false;

    std::vector<cv::Point2f> finalPoints;
    for (int index : inlierIndices) {
        finalPoints.emplace_back(
            static_cast<float>(samples[index].point.x),
            static_cast<float>(samples[index].point.y));
    }
    cv::fitLine(finalPoints, fitted, cv::DIST_HUBER, 0.0, 0.001, 0.001);
    fit.point = cv::Point2d(fitted[2], fitted[3]);
    fit.direction = cv::Point2d(fitted[0], fitted[1]);
    if (fit.direction.dot(expectedDirection) < 0.0) fit.direction *= -1.0;
    double sumSquares = 0.0;
    for (int index : inlierIndices) {
        fit.inliers.push_back(samples[index]);
        const double distance = DistanceToLine(samples[index].point, fit.point, fit.direction);
        sumSquares += distance * distance;
    }
    fit.rms = std::sqrt(sumSquares / fit.inliers.size());
    fit.valid = true;
    return true;
}

bool SampleAxis(
    const cv::Mat& response,
    const cv::Point2d& coarseCenter,
    const cv::Point2d& direction,
    const PatternCrossConfig& config,
    AxisFit& fit)
{
    const cv::Point2d normal(-direction.y, direction.x);
    const int normalHalfWidth = std::clamp(
        static_cast<int>(std::lround(std::min(response.rows, response.cols) * 0.006)),
        7, 32);
    const double centerGap = std::max(10.0, normalHalfWidth * 1.6);
    const double maximumSpan = std::min(
        std::max(config.minArmLengthPixels * 4.0,
            std::min(response.rows, response.cols) * 0.22),
        std::min(response.rows, response.cols) * 0.46);
    const double step = std::clamp(config.minArmLengthPixels / 24.0, 2.0, 8.0);
    cv::Scalar mean;
    cv::Scalar standardDeviation;
    cv::meanStdDev(response, mean, standardDeviation);
    const double minimumPeak = std::max(
        config.minContrast * 0.20,
        mean[0] + standardDeviation[0] * 0.30);

    std::vector<AxisSample> samples;
    std::array<int, 2> attemptsWithinRequired{};
    std::array<int, 2> validWithinRequired{};
    for (int side = 0; side < 2; ++side) {
        const double sign = side == 0 ? -1.0 : 1.0;
        int consecutiveMisses = 0;
        for (double distance = centerGap; distance <= maximumSpan; distance += step) {
            const cv::Point2d base = coarseCenter + direction * (sign * distance);
            if (base.x < normalHalfWidth + 1 || base.y < normalHalfWidth + 1
                || base.x >= response.cols - normalHalfWidth - 1
                || base.y >= response.rows - normalHalfWidth - 1) break;
            if (distance <= config.minArmLengthPixels + centerGap) attemptsWithinRequired[side]++;

            std::vector<double> profile;
            double peak = 0.0;
            for (int offset = -normalHalfWidth; offset <= normalHalfWidth; ++offset) {
                const cv::Point2d point = base + normal * offset;
                const double value = BilinearSample(response, point.x, point.y);
                profile.push_back(value);
                peak = std::max(peak, value);
            }
            if (peak < minimumPeak) {
                consecutiveMisses++;
                if (distance > config.minArmLengthPixels + centerGap && consecutiveMisses >= 5) break;
                continue;
            }
            const double weightThreshold = std::max(minimumPeak * 0.65, peak * 0.28);
            double weightedOffset = 0.0;
            double weightSum = 0.0;
            for (int offset = -normalHalfWidth; offset <= normalHalfWidth; ++offset) {
                const double weight = std::max(
                    0.0, profile[offset + normalHalfWidth] - weightThreshold);
                weightedOffset += offset * weight;
                weightSum += weight;
            }
            if (weightSum <= 1e-9) {
                consecutiveMisses++;
                continue;
            }
            consecutiveMisses = 0;
            if (distance <= config.minArmLengthPixels + centerGap) validWithinRequired[side]++;
            samples.push_back({
                base + normal * (weightedOffset / weightSum), peak, side
            });
        }
    }

    if (!FitAxisRobust(samples, direction, fit)) return false;
    for (int side = 0; side < 2; ++side) {
        PatternArmQuality& quality = fit.armQuality[side];
        quality.sampleCount = attemptsWithinRequired[side];
        quality.coverage = attemptsWithinRequired[side] > 0
            ? static_cast<double>(validWithinRequired[side]) / attemptsWithinRequired[side]
            : 0.0;
        std::vector<double> contrasts;
        double maximumProjection = 0.0;
        for (const AxisSample& sample : fit.inliers) {
            if (sample.armIndex != side) continue;
            quality.inlierCount++;
            contrasts.push_back(sample.contrast);
            maximumProjection = std::max(
                maximumProjection,
                std::abs((sample.point - coarseCenter).dot(fit.direction)));
        }
        quality.contrast = Percentile(contrasts, 0.25);
        quality.span = maximumProjection;
        quality.fitRms = fit.rms;
    }
    return true;
}

double WeightedAxisAverage(double firstAngle, double secondAngle, double firstWeight, double secondWeight)
{
    const double firstRadians = 2.0 * firstAngle * kDegreesToRadians;
    const double secondRadians = 2.0 * secondAngle * kDegreesToRadians;
    const double sine = firstWeight * std::sin(firstRadians)
        + secondWeight * std::sin(secondRadians);
    const double cosine = firstWeight * std::cos(firstRadians)
        + secondWeight * std::cos(secondRadians);
    return NormalizeAxisAngle(0.5 * std::atan2(sine, cosine) * kRadiansToDegrees);
}

PatternCrossResult RefineCandidate(
    const cv::Mat& original,
    const CoarseCandidate& coarse,
    const PatternCrossConfig& config,
    double coarseScale)
{
    PatternCrossResult result;
    result.polarityUsed = PolarityName(coarse.polarity);
    const cv::Point2d fullCenter = coarse.sourceCenter * (1.0 / coarseScale);
    const double halfExtent = std::min(
        std::max(config.minArmLengthPixels * 5.0,
            std::min(original.rows, original.cols) * 0.16),
        1600.0);
    const int left = std::max(0, static_cast<int>(std::floor(fullCenter.x - halfExtent)));
    const int top = std::max(0, static_cast<int>(std::floor(fullCenter.y - halfExtent)));
    const int right = std::min(original.cols, static_cast<int>(std::ceil(fullCenter.x + halfExtent)) + 1);
    const int bottom = std::min(original.rows, static_cast<int>(std::ceil(fullCenter.y + halfExtent)) + 1);
    if (right - left < config.minArmLengthPixels * 2.0
        || bottom - top < config.minArmLengthPixels * 2.0) {
        result.failureReason = "PatternClipped";
        return result;
    }

    const cv::Rect patchRect(left, top, right - left, bottom - top);
    const cv::Mat gray = ConvertToGrayFloat(original(patchRect));
    if (gray.empty()) {
        result.failureReason = "UnsupportedImage";
        return result;
    }
    const cv::Mat response = BuildSignedResponse(gray, coarse.polarity);
    const cv::Point2d patchCenter = fullCenter - cv::Point2d(left, top);
    const double angleRadians = coarse.angleDegrees * kDegreesToRadians;
    const cv::Point2d primaryDirection(std::cos(angleRadians), std::sin(angleRadians));
    const cv::Point2d secondaryDirection(-primaryDirection.y, primaryDirection.x);
    AxisFit primary;
    AxisFit secondary;
    if (!SampleAxis(response, patchCenter, primaryDirection, config, primary)
        || !SampleAxis(response, patchCenter, secondaryDirection, config, secondary)) {
        result.failureReason = "InsufficientFullResolutionInliers";
        return result;
    }

    cv::Point2d refinedCenter;
    if (!IntersectLines(
        primary.point, primary.direction, secondary.point, secondary.direction, refinedCenter)) {
        result.failureReason = "InvalidCenterGeometry";
        return result;
    }
    if (cv::norm(refinedCenter - patchCenter)
        > std::max(8.0, config.minArmLengthPixels * 0.45)) {
        result.failureReason = "UnstableRefinement";
        return result;
    }

    const double primaryAngle = NormalizeAxisAngle(
        std::atan2(primary.direction.y, primary.direction.x) * kRadiansToDegrees);
    const double secondaryAngle = NormalizeAxisAngle(
        std::atan2(secondary.direction.y, secondary.direction.x) * kRadiansToDegrees);
    double axisDifference = std::fmod(std::abs(secondaryAngle - primaryAngle), 180.0);
    if (axisDifference > 90.0) axisDifference = 180.0 - axisDifference;
    const double orthogonalityError = std::abs(90.0 - axisDifference);
    if (orthogonalityError > 3.0) {
        result.failureReason = "NonOrthogonalAxes";
        return result;
    }

    const std::array<PatternArmQuality, 4> quality{
        primary.armQuality[0], primary.armQuality[1],
        secondary.armQuality[0], secondary.armQuality[1]
    };
    double minimumCoverage = 1.0;
    double minimumContrast = std::numeric_limits<double>::infinity();
    double maximumRms = 0.0;
    for (const PatternArmQuality& arm : quality) {
        minimumCoverage = std::min(minimumCoverage, arm.coverage);
        minimumContrast = std::min(minimumContrast, arm.contrast);
        maximumRms = std::max(maximumRms, arm.fitRms);
        if (arm.coverage < config.minArmCoverage
            || arm.span < config.minArmLengthPixels * 0.90) {
            result.failureReason = "InsufficientArmSupport";
            return result;
        }
    }
    // Preserve the measured evidence on every quality rejection. Callers need
    // to distinguish a weak, poorly localized Pattern from a missing Pattern
    // without enabling debug-image output or adding detector tuning knobs.
    result.orthogonalityErrorDegrees = orthogonalityError;
    result.patternContrast = minimumContrast;
    result.armQuality = quality;

    // Coarse search deliberately uses a relaxed threshold because downsampling
    // attenuates a thin Pattern. At full resolution this setting is a real hard
    // acceptance limit: raising MinPatternContrast must never make a weaker
    // Pattern pass.
    if (minimumContrast < config.minContrast) {
        result.failureReason = "LowPatternContrast";
        return result;
    }
    // Blur and broad optical bloom can leave all four arms present while making
    // their centerlines unstable. Contrast alone cannot reject that case because
    // a genuinely dim but sharp Pattern has the same amplitude. The combination
    // of weak full-resolution response and poor robust line localization is the
    // quality evidence: it rejects an unreliable angle while preserving dim,
    // sharp Patterns and strong Patterns with isolated contamination.
    constexpr double kWeakPatternContrast = 0.03;
    constexpr double kWeakPatternMaximumFitRms = 0.45;
    if (minimumContrast < kWeakPatternContrast
        && maximumRms > kWeakPatternMaximumFitRms) {
        result.failureReason = "PoorPatternSharpness";
        return result;
    }
    if (maximumRms > 2.5) {
        result.failureReason = "PoorLineFit";
        return result;
    }

    const double selectedAngle = WeightedAxisAverage(
        primaryAngle,
        NormalizeAxisAngle(secondaryAngle - 90.0),
        std::max(1.0, static_cast<double>(primary.inliers.size())),
        std::max(1.0, static_cast<double>(secondary.inliers.size())));
    auto MakeEndpoint = [&](const AxisFit& axis, int armIndex) {
        double projection = armIndex == 0
            ? std::numeric_limits<double>::infinity()
            : -std::numeric_limits<double>::infinity();
        for (const AxisSample& sample : axis.inliers) {
            if (sample.armIndex != armIndex) continue;
            const double current = (sample.point - refinedCenter).dot(axis.direction);
            projection = armIndex == 0
                ? std::min(projection, current)
                : std::max(projection, current);
        }
        if (!std::isfinite(projection)) {
            projection = armIndex == 0
                ? -config.minArmLengthPixels
                : config.minArmLengthPixels;
        }
        return refinedCenter + axis.direction * projection + cv::Point2d(left, top);
    };

    const double contrastScore = std::clamp(
        minimumContrast / std::max(config.minContrast, 1e-9), 0.0, 1.0);
    const double fitScore = std::clamp(1.0 - maximumRms / 2.5, 0.0, 1.0);
    const double orthogonalityScore = std::clamp(1.0 - orthogonalityError / 3.0, 0.0, 1.0);
    result.confidence = std::clamp(
        0.35 * contrastScore + 0.30 * minimumCoverage
            + 0.20 * fitScore + 0.15 * orthogonalityScore,
        0.0, 1.0);
    result.center = refinedCenter + cv::Point2d(left, top);
    result.primaryAngleDegrees = primaryAngle;
    result.secondaryAngleDegrees = secondaryAngle;
    result.combinedAngleDegrees = selectedAngle;
    result.orthogonalityErrorDegrees = orthogonalityError;
    result.patternContrast = minimumContrast;
    result.armEndpoints = {
        MakeEndpoint(primary, 0), MakeEndpoint(primary, 1),
        MakeEndpoint(secondary, 0), MakeEndpoint(secondary, 1)
    };
    result.armQuality = quality;
    if (result.confidence < config.minConfidence) {
        result.failureReason = "LowConfidence";
        return result;
    }
    result.success = true;
    return result;
}

} // namespace

PatternCrossResult FindPatternCross(const cv::Mat& image, const PatternCrossConfig& config)
{
    PatternCrossResult result;
    if (image.empty()) {
        result.failureReason = "UnsupportedImage";
        return result;
    }

    const double scale = std::min(
        1.0,
        static_cast<double>(config.maxProcessingSize) / std::max(image.rows, image.cols));
    cv::Mat coarseInput;
    if (scale < 0.999) {
        cv::resize(image, coarseInput, cv::Size(), scale, scale, cv::INTER_AREA);
    }
    else {
        coarseInput = image;
    }
    const cv::Mat coarseGray = ConvertToGrayFloat(coarseInput);
    if (coarseGray.empty()) {
        result.failureReason = "UnsupportedImage";
        return result;
    }
    double minimum = 0.0;
    double maximum = 0.0;
    cv::minMaxLoc(coarseGray, &minimum, &maximum);
    if (!std::isfinite(minimum) || !std::isfinite(maximum)
        || maximum - minimum < config.minContrast * 0.5) {
        result.failureReason = "NoSignal";
        return result;
    }

    std::vector<CoarseCandidate> candidates;
    const double coarseStep = config.angleToleranceDegrees <= 1.0 ? 0.20 : 0.50;
    for (PatternPolarity polarity : ResolvePolarities(config.polarity)) {
        const cv::Mat response = BuildSignedResponse(coarseGray, polarity);
        const double firstAngle = config.expectedAngleDegrees - config.angleToleranceDegrees;
        const double lastAngle = config.expectedAngleDegrees + config.angleToleranceDegrees;
        for (double angle = firstAngle; angle <= lastAngle + 1e-9; angle += coarseStep) {
            const CoarseCandidatePair angleCandidates = EvaluateAngle(
                response, angle, polarity, config, scale);
            if (angleCandidates.best.valid) candidates.push_back(angleCandidates.best);
            if (angleCandidates.second.valid) candidates.push_back(angleCandidates.second);
        }
    }
    std::sort(candidates.begin(), candidates.end(), [](const auto& left, const auto& right) {
        return left.score > right.score;
    });
    CoarseCandidate best;
    CoarseCandidate second;
    if (!candidates.empty()) {
        best = candidates.front();
        const double minimumSeparation = std::max(
            4.0, config.minArmLengthPixels * scale);
        for (size_t index = 1; index < candidates.size(); ++index) {
            if (cv::norm(candidates[index].sourceCenter - best.sourceCenter)
                > minimumSeparation) {
                second = candidates[index];
                break;
            }
        }
    }
    if (!best.valid || best.score <= 0.0) {
        result.failureReason = "NoPatternCandidate";
        return result;
    }
    // Coarse scores are only a search ranking. Illumination gradients can make
    // a second, fully valid Pattern substantially dimmer than the strongest
    // one, so a relative score threshold is not a safe uniqueness gate. Refine
    // at most the two strongest spatial clusters and make the production
    // decision from the same full-resolution quality gates used for the result.
    PatternCrossResult bestRefined = RefineCandidate(image, best, config, scale);
    if (!second.valid) return bestRefined;

    PatternCrossResult secondRefined = RefineCandidate(image, second, config, scale);
    if (bestRefined.success && secondRefined.success) {
        result.failureReason = "AmbiguousPattern";
        return result;
    }
    if (bestRefined.success) return bestRefined;
    if (secondRefined.success) return secondRefined;
    return bestRefined;
}

} // namespace cvnative::find_cross
