#include "sfr_bmw4.h"
#include <opencv2/imgproc.hpp>
#include <algorithm>
#include <cmath>

namespace cvcore::sfr {
namespace {
double cross(cv::Point2d a, cv::Point2d b) { return a.x * b.y - a.y * b.x; }

// Detection-only intensity samples. Measurement always uses the original image.
double sample(const cv::Mat& gray, cv::Point2d p)
{
    int x = cvRound(p.x), y = cvRound(p.y);
    if (x < 1 || y < 1 || x >= gray.cols - 1 || y >= gray.rows - 1) return -1;
    return cv::mean(gray(cv::Rect(x - 1, y - 1, 3, 3)))[0];
}

cv::Rect insideBox(cv::Point2d center, cv::Point2d axis, double along, double across)
{
    bool horizontal = std::abs(axis.x) >= std::abs(axis.y);
    double hx = (horizontal ? along : across) / 2, hy = (horizontal ? across : along) / 2;
    // Inscribed axis-aligned box, entirely inside the local branch support.
    double scale = std::min(along / (2 * (std::abs(axis.x) * hx + std::abs(axis.y) * hy)),
        across / (2 * (std::abs(axis.y) * hx + std::abs(axis.x) * hy)));
    hx *= scale; hy *= scale;
    int x = static_cast<int>(std::ceil(center.x - hx)), y = static_cast<int>(std::ceil(center.y - hy));
    return {x, y, std::max(0, static_cast<int>(std::floor(center.x + hx)) - x),
        std::max(0, static_cast<int>(std::floor(center.y + hy)) - y)};
}
}

BmwLocatedTarget locateCheckerboardTarget(const cv::Mat& crop)
{
    BmwLocatedTarget failure;
    failure.chartType = "checkerboard";
    failure.reason = "checkerboard_corner_not_found";
    if (crop.empty() || !cv::checkRange(crop)) return failure;
    cv::Mat gray, converted = crop;
    if (crop.depth() == CV_64F) crop.convertTo(converted, CV_32F);
    if (converted.channels() == 1) gray = converted;
    else if (converted.channels() == 3) cv::cvtColor(converted, gray, cv::COLOR_BGR2GRAY);
    else return failure;
    cv::Mat gray8;
    cv::normalize(gray, gray8, 0, 255, cv::NORM_MINMAX, CV_8U);
    const double scale = std::min(1.0, 1024.0 / std::max(gray.cols, gray.rows));
    cv::Mat detection;
    cv::resize(gray8, detection, {}, scale, scale, cv::INTER_AREA);
    const double sx = detection.cols / static_cast<double>(crop.cols), sy = detection.rows / static_cast<double>(crop.rows);
    auto original = [&](cv::Point2d p) { return cv::Point2d((p.x + .5) / sx - .5, (p.y + .5) / sy - .5); };
    std::vector<cv::Point2f> corners;
    cv::goodFeaturesToTrack(detection, corners, 512, .025, 8, cv::noArray(), 5, true);
    if (corners.empty()) return failure;
    cv::cornerSubPix(detection, corners, {5,5}, {-1,-1}, {cv::TermCriteria::COUNT | cv::TermCriteria::EPS, 30, .01});
    cv::Mat edges;
    cv::Canny(detection, edges, 40, 100);
    std::vector<cv::Vec4i> lines;
    cv::HoughLinesP(edges, lines, 1, CV_PI / 720, 20, 20, 8);
    const cv::Point2d searchCenter((detection.cols - 1) * .5, (detection.rows - 1) * .5);
    std::sort(corners.begin(), corners.end(), [&](auto a, auto b) { return cv::norm(cv::Point2d(a) - searchCenter) < cv::norm(cv::Point2d(b) - searchCenter); });
    struct Candidate { BmwLocatedTarget target; double distance, spacing; };
    std::vector<Candidate> candidates;
    for (const auto& corner : corners) {
        cv::Point2d axes[2], origins[2];
        double best[2] = {1e30, 1e30};
        for (auto line : lines) {
            cv::Point2d p(line[0], line[1]), d(line[2] - line[0], line[3] - line[1]);
            double length = cv::norm(d); d /= length;
            int axis = std::abs(d.x) > std::abs(d.y) ? 0 : 1;
            if (std::min(std::abs(d.x), std::abs(d.y)) > .27) continue;
            cv::Point2d delta = cv::Point2d(corner) - p;
            double distance = std::abs(cross(delta, d));
            // Hough segments often stop before a blurred junction. Extrapolate
            // their axes to the independently detected corner, then validate
            // all four local quadrants and continuous branches below.
            if (distance > 4) continue;
            if (distance < best[axis]) { best[axis] = distance; axes[axis] = d; origins[axis] = p; }
        }
        if (best[0] == 1e30 || best[1] == 1e30 || std::abs(axes[0].dot(axes[1])) > .15) continue;
        auto u = axes[0], v = axes[1];
        if (u.x < 0) u = -u;
        if (v.y < 0) v = -v;
        auto center = origins[0] + u * (cross(origins[1] - origins[0], v) / cross(u, v));
        if (cv::norm(center - cv::Point2d(corner)) > 5) continue;
        const double normal = std::max(4.0, 12.0 * scale);
        double q[4] = { sample(detection, center + (u + v) * normal), sample(detection, center + (u - v) * normal),
            sample(detection, center - (u + v) * normal), sample(detection, center - (u - v) * normal) };
        double contrast = (q[0] + q[2] - q[1] - q[3]) * .5;
        if (*std::min_element(q, q + 4) < 0 || std::abs(contrast) < 25
            || std::abs(q[0] - q[2]) > std::abs(contrast) * .35 || std::abs(q[1] - q[3]) > std::abs(contrast) * .35) continue;
        // Stop before the next junction, chart boundary, or missing branch.
        // This defines a local four-edge cell without requiring board rows/columns.
        std::array<cv::Point2d,4> directions{-u, -v, u, v};
        std::array<double,4> runs{};
        for (int id = 0; id < 4; ++id) {
            auto along = directions[id], transverse = id % 2 == 0 ? v : u;
            double initial = sample(detection, center + along * (2 * normal) + transverse * normal)
                - sample(detection, center + along * (2 * normal) - transverse * normal);
            if (std::abs(initial) < std::abs(contrast) * .5) continue;
            for (double t = 2 * normal; t < std::max(detection.cols, detection.rows); t += 1) {
                double a = sample(detection, center + along * t + transverse * normal),
                    b = sample(detection, center + along * t - transverse * normal);
                if (a < 0 || b < 0 || (a - b) * initial < initial * initial * .4) break;
                runs[id] = t;
            }
        }
        double shortest = *std::min_element(runs.begin(), runs.end());
        if (shortest <= 0) continue;
        BmwLocatedTarget target;
        target.chartType = "checkerboard"; target.center = original(center);
        for (int id = 0; id < 4; ++id) {
            auto direction = directions[id];
            auto originalDirection = cv::Point2d(direction.x / sx, direction.y / sy);
            double length = runs[id] * cv::norm(originalDirection); originalDirection /= cv::norm(originalDirection);
            auto roiCenter = original(center + direction * (runs[id] * .5));
            // The transverse limits come from the two perpendicular branches,
            // not the shortest of all four. Stop halfway toward either adjacent
            // parallel edge; retain the 16-pixel guard along the measured branch.
            double transverseRun = id % 2 == 0 ? std::min(runs[1], runs[3]) : std::min(runs[0], runs[2]);
            double across = transverseRun / scale;
            if (length <= 32 || across <= 0) {
                target.edgeReasons[id] = "checkerboard_insufficient_edge_support";
                continue;
            }
            target.supports[id] = insideBox(roiCenter, originalDirection, length - 32, across);
            target.supports[id] &= cv::Rect(0, 0, crop.cols, crop.rows);
            // SFR requires 32 samples along the edge and 40 across it. Decide
            // from the actual safe rectangle instead of a fixed 100-pixel run.
            int alongSize = std::max(32, static_cast<int>(length * .45)), acrossSize = std::max(40, static_cast<int>(shortest / scale * .32));
            int w = id % 2 == 0 ? alongSize : acrossSize, h = id % 2 == 0 ? acrossSize : alongSize;
            target.edges[id] = {cvRound(roiCenter.x - w * .5), cvRound(roiCenter.y - h * .5), w, h};
            if ((target.edges[id] & target.supports[id]) != target.edges[id]) {
                target.edges[id] = {};
                target.edgeReasons[id] = "checkerboard_insufficient_edge_support";
            }
        }
        // Locating a junction and having enough samples on every edge are
        // separate outcomes. A short branch must not hide the other three.
        target.target = cv::Rect(0, 0, crop.cols, crop.rows);
        target.located = true; target.reason.clear();
        // A single physical junction may produce multiple nearby corner responses.
        if (std::any_of(candidates.begin(), candidates.end(), [&](const auto& item) { return cv::norm(item.target.center - target.center) < 8 / scale; })) continue;
        candidates.push_back({target, cv::norm(center - searchCenter), shortest});
    }
    if (candidates.empty()) return failure;
    std::sort(candidates.begin(), candidates.end(), [](const auto& a, const auto& b) { return a.distance < b.distance; });
    if (candidates.size() > 1 && candidates[1].distance - candidates[0].distance < .1 * std::min(candidates[0].spacing, candidates[1].spacing)) {
        failure.reason = "ambiguous_checkerboard_corners";
        return failure;
    }
    return candidates.front().target;
}
}
