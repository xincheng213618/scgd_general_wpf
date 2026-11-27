#ifndef NOMINMAX
#define NOMINMAX
#endif
#include "../../Native/include/opencv_media_export.h"
#include <nlohmann/json.hpp>
#include <opencv2/opencv.hpp>
#include <opencv2/geometry.hpp>
#include <algorithm>
#include <array>
#include <cmath>
#include <iostream>
#include <limits>
#include <string>
#include <vector>

namespace {
using json = nlohmann::json;

struct Fixture {
    cv::Mat image;
    std::vector<cv::Point2f> centers;
};

Fixture makeGrid(int rows, int cols)
{
    Fixture f{ cv::Mat(1000, 1200, CV_8UC1, cv::Scalar(0)), {} };
    for (int r = 0; r < rows; ++r) for (int c = 0; c < cols; ++c) {
        const double u = 2.0 * c / (cols - 1) - 1, v = 2.0 * r / (rows - 1) - 1;
        // Curved rows and columns with independently known circle centers.
        cv::Point p(cvRound(600 + 440 * u * (1 + 0.06 * v * v)), cvRound(500 + 340 * v * (1 + 0.06 * u * u)));
        f.centers.emplace_back(p);
        cv::circle(f.image, p, 20, cv::Scalar(160), cv::FILLED, cv::LINE_8);
    }
    return f;
}

cv::Point regularCenter(int row, int col)
{
    return cv::Point(280 + 80 * col, 280 + 80 * row);
}

Fixture makeRegularGrid(int rows, int cols)
{
    Fixture f{ cv::Mat(1400, 1600, CV_8UC1, cv::Scalar(0)), {} };
    for (int r = 0; r < rows; ++r) for (int c = 0; c < cols; ++c) {
        const cv::Point p = regularCenter(r, c);
        f.centers.emplace_back(p);
        cv::circle(f.image, p, 14, cv::Scalar(160), cv::FILLED, cv::LINE_8);
    }
    return f;
}

json run(const cv::Mat& image, json config = json::object(), RoiRect roi = {})
{
    HImage h{};
    if (!image.empty()) {
        h.rows = image.rows; h.cols = image.cols; h.channels = image.channels();
        h.depth = CvDepthToHImageDepth(image.depth()); h.stride = static_cast<int>(image.step);
        h.isDispose = true; h.pData = image.data;
    }
    char* output = nullptr;
    const std::string payload = config.dump();
    const int status = M_CalDistortionGridV2(h, roi, payload.c_str(), &output);
    json result;
    if (status > 0 && output != nullptr) result = json::parse(output, nullptr, false);
    FreeResult(output);
    return result;
}

bool rejected(const json& result)
{
    return result.is_object() && !result.value("success", true) && result.contains("metrics") && result["metrics"].is_null();
}

bool usedHigherSegmentationThreshold(const json& result)
{
    return std::any_of(result["warnings"].begin(), result["warnings"].end(), [](const json& warning) {
        return warning.get_ref<const std::string&>().find("higher segmentation threshold") != std::string::npos;
    });
}

bool matches(const json& result, const std::vector<cv::Point2f>& expected, double tolerance = 0.75)
{
    if (!result.is_object() || !result.value("success", false) || result.value("selectedCount", 0) != expected.size()
        || !result.contains("metrics") || !result["metrics"].is_object()) {
        std::cerr << result.dump() << std::endl;
        return false;
    }
    for (size_t i = 0; i < expected.size(); ++i) {
        const auto& p = result["points"][i];
        if (std::hypot(p.value("x", 0.0) - expected[i].x, p.value("y", 0.0) - expected[i].y) > tolerance) {
            std::cerr << "Center mismatch " << i << ": " << p.dump() << std::endl;
            return false;
        }
    }
    return result["referencePointIds"].size() == 9;
}

cv::Mat addLeakage(const cv::Mat& input, int kind)
{
    cv::Mat output(input.size(), CV_8U);
    for (int y = 0; y < input.rows; ++y) for (int x = 0; x < input.cols; ++x) {
        double background = 0;
        if (kind == 0) background = 10 + 75.0 * x / input.cols + 15.0 * y / input.rows;
        if (kind == 1) background = 85 * std::exp(-std::pow((x - 600.0) / 95.0, 2) / 2);
        if (kind == 2) background = 80 * std::exp(-(std::pow((x - 600.0) / 200.0, 2) + std::pow((y - 500.0) / 190.0, 2)) / 2);
        output.at<uchar>(y, x) = cv::saturate_cast<uchar>(input.at<uchar>(y, x) + background);
    }
    return output;
}
} // namespace

bool RunGridDistortionV2Tests()
{
    int failures = 0;
    auto check = [&](bool ok, const char* name) {
        std::cout << "GridDistortionV2 " << name << ": " << (ok ? "PASS" : "FAIL") << std::endl;
        if (!ok) ++failures;
    };
    const Fixture p3 = makeGrid(3, 3), p7 = makeGrid(7, 7), p35 = makeGrid(3, 5);
    const json result3 = run(p3.image);
    check(matches(result3, p3.centers), "3x3 curved grid");
    check(result3.value("success", false)
        && std::abs(result3["metrics"].value("horizontalTvPercent", 0.0) - 5.909090909090909) < 1e-5
        && std::abs(result3["metrics"].value("verticalTvPercent", 0.0) - 5.882352941176471) < 1e-5
        && std::abs(result3["metrics"].value("topPercent", 0.0) - 2.777777777777778) < 1e-5
        && std::abs(result3["metrics"].value("leftPercent", 0.0) - 2.789699570815451) < 1e-5,
        "TV and opposite-edge-normalized bow exact values");
    const json result7 = run(p7.image, { {"expectedRows", 7}, {"expectedCols", 7} });
    check(matches(result7, p7.centers), "7x7 curved grid");
    check(matches(run(p35.image, {{"expectedRows", 3}, {"expectedCols", 5}}), p35.centers), "rectangular 3x5 grid");
    check(rejected(run(p7.image)), "7x7 cannot masquerade as 3x3");

    cv::Mat half;
    cv::resize(p3.image, half, cv::Size(), 0.5, 0.5, cv::INTER_AREA);
    auto halfCenters = p3.centers;
    for (auto& p : halfCenters) p = (p + cv::Point2f(0.5F, 0.5F)) * 0.5F - cv::Point2f(0.5F, 0.5F);
    check(matches(run(half), halfCenters), "scale 0.5");
    cv::Mat weak = p3.image.clone();
    const cv::Point center(p3.centers[4]);
    weak(cv::Rect(center.x - 30, center.y - 30, 61, 61)) /= 10;
    check(matches(run(weak), p3.centers), "one weak point");
    check(matches(run(p3.image / 10), p3.centers), "global low brightness");
    cv::Mat noisy; p3.image.convertTo(noisy, CV_32F, 1, 6);
    cv::Mat noise(noisy.size(), CV_32F);
    cv::RNG noiseGenerator(71357);
    noiseGenerator.fill(noise, cv::RNG::NORMAL, 0, 5);
    noisy += noise; noisy.convertTo(noisy, CV_8U);
    check(matches(run(noisy), p3.centers), "seeded sensor noise with tiny bright components");

    const cv::Mat rotation = cv::getRotationMatrix2D(cv::Point2f(600, 500), 15, 1);
    cv::Mat rotated;
    cv::warpAffine(p3.image, rotated, rotation, p3.image.size(), cv::INTER_LINEAR);
    std::vector<cv::Point2f> rotatedCenters;
    cv::transform(p3.centers, rotatedCenters, rotation);
    check(matches(run(rotated), rotatedCenters), "rotation 15 degrees");
    cv::Mat reflection = p3.image.clone();
    cv::rectangle(reflection, cv::Rect(480, 240, 180, 100), cv::Scalar(255), cv::FILLED);
    check(matches(run(reflection), p3.centers), "large rectangular reflection excluded");
    for (int kind = 0; kind < 3; ++kind) {
        const std::array<const char*, 3> names = { "gradient leakage", "broad light band", "diffuse halo" };
        check(matches(run(addLeakage(p3.image, kind)), p3.centers), names[kind]);
    }
    cv::Mat missing = p3.image.clone();
    cv::circle(missing, cv::Point(p3.centers.back()), 30, cv::Scalar(0), cv::FILLED);
    const RoiRect missingRoi{100, 75, missing.cols, missing.rows};
    cv::Mat paddedMissing(missing.rows + 150, missing.cols + 200, CV_8U, cv::Scalar(0));
    missing.copyTo(paddedMissing(cv::Rect(missingRoi.x, missingRoi.y, missingRoi.width, missingRoi.height)));
    const json rejectedMissing = run(paddedMissing, {{"maxProcessingSize", 800}}, missingRoi);
    bool candidatesMatch = rejected(rejectedMissing)
        && rejectedMissing.value("candidateCount", 0) == 8 && rejectedMissing.value("selectedCount", -1) == 0
        && rejectedMissing["points"].empty() && rejectedMissing.contains("candidatePoints")
        && rejectedMissing["candidatePoints"].size() == 8;
    if (candidatesMatch) {
        const auto& points = rejectedMissing["candidatePoints"];
        for (size_t i = 0; i < 8; ++i) {
            const cv::Point2f expected = p3.centers[i] + cv::Point2f(100, 75);
            candidatesMatch &= std::any_of(points.begin(), points.end(), [&](const json& point) {
                return std::hypot(point.at("x").get<double>() - expected.x, point.at("y").get<double>() - expected.y) < 2;
            });
        }
    }
    check(candidatesMatch, "missing point rejected and detected candidates mapped through resize and ROI");
    cv::Mat saturated = p3.image.clone();
    cv::rectangle(saturated, cv::Rect(center.x - 55, center.y - 55, 111, 111), cv::Scalar(255), cv::FILLED);
    check(rejected(run(saturated)), "saturated occlusion rejected");
    check(rejected(run(cv::Mat())), "empty image rejected");
    check(rejected(run(cv::Mat(500, 500, CV_8U, cv::Scalar(128)))), "uniform image rejected");
    check(rejected(run(p3.image, {{"expectedRows", 4}})), "even rows rejected");
    check(rejected(run(p3.image, {{"expectedRows", 3.5}})), "fractional rows rejected");
    check(rejected(run(p3.image, {{"expectedRows", 4294967299LL}})), "overflowing rows rejected");
    check(rejected(run(p3.image, {{"tvCalcWay", 0.5}})), "fractional TV mode rejected");
    check(rejected(run(p3.image, {{"maxProcessingSize", 128}})), "invalid processing size rejected");
    check(rejected(run(p3.image, json::object(), {-1, 0, 300, 300})), "invalid ROI rejected");

    cv::Mat padded(1200, 1500, CV_8U, cv::Scalar(0));
    p3.image.copyTo(padded(cv::Rect(100, 75, p3.image.cols, p3.image.rows)));
    auto fullCenters = p3.centers;
    for (auto& p : fullCenters) p += cv::Point2f(100, 75);
    check(matches(run(padded, json::object(), {100, 75, p3.image.cols, p3.image.rows}), fullCenters), "ROI coordinates are full-image");
    for (const int depth : { CV_16U, CV_32F, CV_64F }) {
        cv::Mat typed; p3.image.convertTo(typed, depth, depth == CV_16U ? 200.0 : 1.0 / 255.0);
        check(matches(run(typed), p3.centers), depth == CV_16U ? "16U" : depth == CV_32F ? "32F" : "64F");
    }
    cv::Mat bgr, bgra;
    cv::cvtColor(p3.image, bgr, cv::COLOR_GRAY2BGR); cv::cvtColor(p3.image, bgra, cv::COLOR_GRAY2BGRA);
    check(matches(run(bgr), p3.centers), "BGR8");
    check(matches(run(bgra), p3.centers), "BGRA8");
    check(matches(run(255 - p3.image, {{"brightTarget", false}}), p3.centers), "dark circles");
    cv::Mat hdr(1000, 1600, CV_16U, cv::Scalar(0));
    std::vector<cv::Point2f> hdrCenters;
    for (int y : {200, 500, 800}) for (int x : {300, 800, 1300}) {
        hdrCenters.emplace_back(static_cast<float>(x), static_cast<float>(y));
        cv::circle(hdr, cv::Point(x, y), 24, cv::Scalar(200), cv::FILLED);
    }
    cv::rectangle(hdr, cv::Rect(50, 50, 100, 100), cv::Scalar(65535), cv::FILLED);
    check(matches(run(hdr), hdrCenters), "16U dim points survive unrelated saturated reflection");
    cv::Mat nonfinite; p3.image.convertTo(nonfinite, CV_32F);
    nonfinite.at<float>(500, 600) = std::numeric_limits<float>::quiet_NaN();
    check(rejected(run(nonfinite)), "non-finite intensities rejected");

    // Independent closed-form targets establish axis naming and exact formula identity.
    cv::Mat vertical(1100, 1200, CV_8U, cv::Scalar(0));
    const std::array<cv::Point, 9> verticalPoints = { cv::Point(200,100), {600,100}, {1000,100}, {150,450}, {600,450}, {1050,450}, {100,800}, {600,800}, {1100,800} };
    for (const auto& p : verticalPoints) cv::circle(vertical, p, 20, cv::Scalar(180), cv::FILLED);
    const json v = run(vertical);
    check(v.value("success", false) && std::abs(v["metrics"].value("keystoneVerticalPercent", 0.0) + 22.2222222222222) < 1e-5
        && std::abs(v["metrics"].value("keystoneHorizontalPercent", 1.0)) < 1e-5, "vertical Keystone exact formula");
    cv::Mat horizontal(1100, 1200, CV_8U, cv::Scalar(0));
    const std::array<cv::Point, 9> horizontalPoints = { cv::Point(200,100), {600,150}, {1000,200}, {200,500}, {600,500}, {1000,500}, {200,900}, {600,850}, {1000,800} };
    for (const auto& p : horizontalPoints) cv::circle(horizontal, p, 20, cv::Scalar(180), cv::FILLED);
    const json h = run(horizontal);
    check(h.value("success", false) && std::abs(h["metrics"].value("keystoneHorizontalPercent", 0.0) - 28.5714285714286) < 1e-5
        && std::abs(h["metrics"].value("keystoneVerticalPercent", 1.0)) < 1e-5, "horizontal Keystone exact formula");
    const json halved = run(p3.image, {{"tvCalcWay", 1}});
    check(halved.value("success", false) && result3.value("success", false)
        && std::abs(halved["metrics"].value("horizontalTvPercent", 0.0) * 2 - result3["metrics"].value("horizontalTvPercent", 1.0)) < 1e-7
        && std::abs(halved["metrics"].value("leftPercent", 0.0) - result3["metrics"].value("leftPercent", 1.0)) < 1e-7, "tvCalcWay only halves TV");

    // Foreground components remain measurable inside contour ancestors. The
    // fixture coordinates are independent of contour traversal and grid fitting.
    for (const int dimension : {3, 7}) {
        const Fixture grid = makeRegularGrid(dimension, dimension);
        const json config = {{"expectedRows", dimension}, {"expectedCols", dimension}};
        const cv::Point first = regularCenter(0, 0), last = regularCenter(dimension - 1, dimension - 1);
        cv::Mat framed = grid.image.clone();
        cv::rectangle(framed, first - cv::Point(45, 45), last + cv::Point(45, 45), cv::Scalar(160), 1, cv::LINE_8);
        check(matches(run(framed, config), grid.centers), dimension == 3 ? "3x3 inside one-pixel bright frame" : "7x7 inside one-pixel bright frame");
        json darkConfig = config;
        darkConfig["brightTarget"] = false;
        check(matches(run(255 - framed, darkConfig), grid.centers), dimension == 3 ? "3x3 inside one-pixel dark frame" : "7x7 inside one-pixel dark frame");

        cv::rectangle(framed, first - cv::Point(85, 85), last + cv::Point(85, 85), cv::Scalar(160), 5, cv::LINE_8);
        check(matches(run(framed, config), grid.centers), dimension == 3 ? "3x3 inside nested foreground frames" : "7x7 inside nested foreground frames");

        cv::Mat surrounded = grid.image.clone();
        const cv::Point gridCenter = regularCenter(dimension / 2, dimension / 2);
        const int ringRadius = cvCeil(cv::norm(last - first) * 0.5) + 55;
        cv::circle(surrounded, gridCenter, ringRadius, cv::Scalar(160), 9, cv::LINE_8);
        check(matches(run(surrounded, config), grid.centers), dimension == 3 ? "3x3 inside foreground ring" : "7x7 inside foreground ring");
    }

    {
        Fixture grid{ cv::Mat(1000, 1400, CV_16U, cv::Scalar(3000)), {} };
        const cv::Point first(440, 300);
        const cv::Point tailStart = first + cv::Point(0, 400);
        cv::line(grid.image, tailStart, tailStart + cv::Point(-80, 65), cv::Scalar(14000), 11, cv::LINE_8);
        for (int row = 0; row < 3; ++row) for (int col = 0; col < 3; ++col) {
            const cv::Point point = first + cv::Point(200 * col, 200 * row);
            grid.centers.emplace_back(point);
            cv::circle(grid.image, point, 18, cv::Scalar(46000), cv::FILLED, cv::LINE_8);
        }
        const json recovered = run(grid.image);
        check(matches(recovered, grid.centers, 3.0) && usedHigherSegmentationThreshold(recovered),
            "bright 16U 3x3 with a tailed corner recovers at a higher threshold");

        const cv::Point last(grid.centers.back());
        cv::circle(grid.image, last, 18, cv::Scalar(3000), cv::FILLED, cv::LINE_8);
        cv::circle(grid.image, last + cv::Point(6, -35), 18, cv::Scalar(46000), cv::FILLED, cv::LINE_8);
        const json shifted = run(grid.image);
        check(rejected(shifted) && shifted.value("statusCode", "") == "grid_residual_too_large",
            "higher threshold rejects a shifted bright replacement for a missing 3x3 corner");
    }

    {
        Fixture grid{ cv::Mat(1000, 1400, CV_16U, cv::Scalar(3000)), {} };
        const cv::Point first(440, 300);
        for (int row = 0; row < 5; ++row) for (int col = 0; col < 5; ++col) {
            const cv::Point point = first + cv::Point(100 * col, 100 * row);
            grid.centers.emplace_back(point);
            const bool alternatingCore = row % 2 == 0 && col % 2 == 0;
            cv::circle(grid.image, point, 18, cv::Scalar(alternatingCore ? 46000 : 3500), cv::FILLED, cv::LINE_8);
        }
        const json config5 = {{"expectedRows", 5}, {"expectedCols", 5}};
        check(matches(run(grid.image, config5), grid.centers, 3.0), "correctly configured 5x5 measures all bright and dim cores");

        const cv::Point tailStart = first + cv::Point(0, 400);
        cv::line(grid.image, tailStart, tailStart + cv::Point(-80, 65), cv::Scalar(14000), 11, cv::LINE_8);
        cv::circle(grid.image, tailStart, 18, cv::Scalar(46000), cv::FILLED, cv::LINE_8);
        const json wrongGrid = run(grid.image);
        check(rejected(wrongGrid) && wrongGrid.value("statusCode", "") == "ambiguous_grid",
            "higher threshold rejects an alternating 3x3 subset of a finer 5x5 grid");
    }

    const Fixture regular7 = makeRegularGrid(7, 7);
    const json config7 = {{"expectedRows", 7}, {"expectedCols", 7}};
    cv::Mat tailed(regular7.image.size(), CV_16U, cv::Scalar(3000));
    const cv::Point corner = regularCenter(6, 0);
    cv::line(tailed, corner, corner + cv::Point(-80, 80), cv::Scalar(12000), 8, cv::LINE_8);
    for (const auto& center : regular7.centers)
        cv::circle(tailed, cv::Point(center), 14, cv::Scalar(25000), cv::FILLED, cv::LINE_8);
    const json recovered = run(tailed, config7);
    check(matches(recovered, regular7.centers, 3.0) && usedHigherSegmentationThreshold(recovered),
        "bright 16U 7x7 with a tailed corner recovers at a higher threshold");
    cv::Mat dimExtension = tailed.clone();
    for (int col = 0; col < 7; ++col)
        cv::circle(dimExtension, regularCenter(7, col), 14, cv::Scalar(4000), cv::FILLED, cv::LINE_8);
    const json rejectedExtension = run(dimExtension, config7);
    check(rejected(rejectedExtension) && rejectedExtension.value("statusCode", "") == std::string("ambiguous_grid"),
        "higher threshold recovery rejects a dim extra row seen at the normal threshold");
    cv::circle(tailed, corner, 14, cv::Scalar(3000), cv::FILLED, cv::LINE_8);
    check(rejected(run(tailed, config7)), "a tail without a dot core still leaves the 7x7 incomplete");

    cv::Mat distant = regular7.image.clone();
    cv::circle(distant, regularCenter(3, 12), 14, cv::Scalar(160), cv::FILLED, cv::LINE_8);
    check(matches(run(distant, config7), regular7.centers), "remote isolated integer-lattice point is not a grid extension");

    cv::Mat adjacent = regular7.image.clone();
    cv::circle(adjacent, regularCenter(3, 7), 14, cv::Scalar(160), cv::FILLED, cv::LINE_8);
    check(matches(run(adjacent, config7), regular7.centers), "isolated adjacent point is not a complete extra column");

    cv::Mat extraRow = regular7.image.clone(), extraColumn = regular7.image.clone();
    for (int i = 0; i < 7; ++i) {
        cv::circle(extraRow, regularCenter(7, i), 14, cv::Scalar(160), cv::FILLED, cv::LINE_8);
        cv::circle(extraColumn, regularCenter(i, 7), 14, cv::Scalar(160), cv::FILLED, cv::LINE_8);
    }
    check(rejected(run(extraRow, config7)), "complete adjacent extra row rejected");
    check(rejected(run(extraColumn, config7)), "complete adjacent extra column rejected");
    cv::Mat partialRow = regular7.image.clone(), sparseColumn = regular7.image.clone();
    for (const int col : {2, 3, 4})
        cv::circle(partialRow, regularCenter(7, col), 14, cv::Scalar(160), cv::FILLED, cv::LINE_8);
    for (const int row : {0, 3, 6})
        cv::circle(sparseColumn, regularCenter(row, 7), 14, cv::Scalar(160), cv::FILLED, cv::LINE_8);
    check(rejected(run(partialRow, config7)), "three consecutive points in adjacent extra row rejected");
    check(rejected(run(sparseColumn, config7)), "three separated points in adjacent extra column rejected");
    check(rejected(run(regular7.image)), "regular 7x7 cannot masquerade as 3x3");

    cv::Mat ringClutter = regular7.image.clone();
    const cv::Point clutterCenter(1241, 997);
    cv::circle(ringClutter, clutterCenter, 20, cv::Scalar(160), cv::FILLED, cv::LINE_8);
    cv::circle(ringClutter, clutterCenter, 12, cv::Scalar(0), cv::FILLED, cv::LINE_8);
    const json withRingClutter = run(ringClutter, config7);
    check(matches(withRingClutter, regular7.centers) && withRingClutter.value("candidateCount", 0) == 50,
        "foreground ring hole is not a duplicate candidate");

    {
        // At 1501 rows the seven processing bands have uneven boundaries.
        // Six grid rows straddle those boundaries and contain tied contour rows.
        Fixture crossing{cv::Mat(1501, 1600, CV_16U, cv::Scalar(500)), {}};
        for (int row = 0; row < 7; ++row) for (int col = 0; col < 7; ++col) {
            const cv::Point center(180 + 205 * col, (row + 1) * crossing.image.rows / 7 - 23);
            const int radius = row == 6 ? 20 : 27;
            crossing.centers.emplace_back(center);
            cv::circle(crossing.image, center, radius, cv::Scalar(45000), cv::FILLED, cv::LINE_8);
        }
        cv::Mat missing = crossing.image.clone();
        cv::circle(missing, cv::Point(crossing.centers[24]), 27, cv::Scalar(500), cv::FILLED, cv::LINE_8);

        struct ThreadCountScope {
            int previous = cv::getNumThreads();
            ~ThreadCountScope() { cv::setNumThreads(previous); }
        } threadCount;
        const auto measure = [&](const cv::Mat& image) {
            json result = run(image, config7);
            if (result.is_object()) result.erase("timings");
            return result;
        };
        cv::setNumThreads(1);
        const json serial = measure(crossing.image), serialMissing = measure(missing);
        cv::setNumThreads(8);
        const json parallel = measure(crossing.image), parallelMissing = measure(missing);
        check(matches(parallel, crossing.centers) && parallel == serial
            && rejected(parallelMissing) && parallelMissing.value("candidateCount", 0) == 48
            && parallelMissing == serialMissing, "cross-band points and rejected candidate order match serial");
    }
    return failures == 0;
}
