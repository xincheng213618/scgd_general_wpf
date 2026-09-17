#include "rgb_cross.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstring>
#include <numeric>
#include <stdexcept>
#include <vector>

namespace cvnative::rgb_cross
{
namespace
{
using json = nlohmann::json;
using Rect = Rectangle;
int Right(Rect r)
{
    return r.x + r.width;
}
int Bottom(Rect r)
{
    return r.y + r.height;
}
bool Intersects(Rect a, Rect b)
{
    return a.x < Right(b) && b.x < Right(a) && a.y < Bottom(b) && b.y < Bottom(a);
}
json Box(double x, double y, double w, double h)
{
    return {{"x", x}, {"y", y}, {"width", w}, {"height", h}};
}
json Box(Rect r)
{
    return Box(r.x, r.y, r.width, r.height);
}
void Require(bool condition, const char *error)
{
    if (!condition)
        throw std::invalid_argument(error);
}
double Sample(const Image &i, int x, int y, int c, double exponent)
{
    auto p =
        i.data + static_cast<std::size_t>(y) * i.stride + (static_cast<std::size_t>(x) * i.channels + c) * (i.bits / 8);
    double v;
    if (i.bits == 8)
        v = *p / 255.;
    else if (i.bits == 16)
        v = (static_cast<unsigned>(p[0]) + (static_cast<unsigned>(p[1]) << 8)) / 65535.;
    else
    {
        float f;
        std::memcpy(&f, p, 4);
        v = f;
    }
    Require(std::isfinite(v) && v >= 0 && v <= 1, "invalid_signal");
    Require(c != 3 || v == 1, "transparent_input");
    return exponent == 1 ? v : std::pow(v, exponent);
}
struct Slot
{
    Rect region, evidence;
    std::string reason;
};
std::array<Slot, 9> Locate(const Image &image, Rect search, const Options &p)
{
    int step = std::max(1, (std::max(search.width, search.height) + 1599) / 1600);
    int w = (search.width + step - 1) / step, h = (search.height + step - 1) / step;
    std::vector<float> signal(w * h, 0);
    for (int y = 0; y < search.height; y++)
        for (int x = 0; x < search.width; x++)
            for (int c = 0; c < image.channels; c++)
            {
                float v = static_cast<float>(Sample(image, search.x + x, search.y + y, c, p.decodeExponent));
                if (c < 3)
                    signal[(y / step) * w + x / step] = std::max(signal[(y / step) * w + x / step], v);
            }
    auto extrema = std::minmax_element(signal.begin(), signal.end());
    double low = *extrema.first, contrast = *extrema.second - low;
    auto reject = [](std::string reason) {
        std::array<Slot, 9> a{};
        for (auto &s : a)
            s.reason = reason;
        return a;
    };
    if (contrast < p.minimumContrast)
        return reject("low_contrast");
    std::vector<unsigned char> mask(w * h);
    for (int i = 0; i < w * h; i++)
        mask[i] = signal[i] >= low + contrast * p.targetThreshold;
    std::vector<Rect> candidates;
    std::vector<int> queue(w * h);
    int components = 0;
    for (int i = 0; i < w * h; i++)
    {
        if (!mask[i])
            continue;
        if (++components > 2048)
            throw std::runtime_error("component_budget_exceeded");
        int head = 0, tail = 1, minx = w, miny = h, maxx = 0, maxy = 0;
        queue[0] = i;
        mask[i] = 0;
        auto visit = [&](int at) {
            if (mask[at])
            {
                mask[at] = 0;
                queue[tail++] = at;
            }
        };
        while (head < tail)
        {
            int at = queue[head++], x = at % w, y = at / w;
            minx = std::min(minx, x);
            maxx = std::max(maxx, x);
            miny = std::min(miny, y);
            maxy = std::max(maxy, y);
            if (x > 0)
                visit(at - 1);
            if (x + 1 < w)
                visit(at + 1);
            if (y > 0)
                visit(at - w);
            if (y + 1 < h)
                visit(at + w);
        }
        if (tail >= 5 && maxx - minx + 1 >= 3 && maxy - miny + 1 >= 3)
            candidates.push_back({minx * step, miny * step,
                                  std::min((maxx - minx + 1) * step, search.width - minx * step),
                                  std::min((maxy - miny + 1) * step, search.height - miny * step)});
    }
    if (candidates.empty())
        return reject("cross_missing");
    auto group = [&](bool horizontal) {
        std::vector<int> order(candidates.size());
        std::iota(order.begin(), order.end(), 0);
        std::stable_sort(order.begin(), order.end(), [&](int a, int b) {
            return horizontal ? candidates[a].x < candidates[b].x : candidates[a].y < candidates[b].y;
        });
        std::vector<std::vector<int>> groups;
        int end = -1;
        for (int i : order)
        {
            Rect r = candidates[i];
            int start = horizontal ? r.x : r.y;
            if (start >= end)
                groups.push_back({});
            groups.back().push_back(i);
            end = std::max(end, horizontal ? Right(r) : Bottom(r));
        }
        return groups;
    };
    auto rows = group(false), cols = group(true);
    if (rows.size() != 3 || cols.size() != 3)
        return reject(candidates.size() > 9   ? "duplicate_or_extra_candidates"
                      : candidates.size() < 9 ? "missing_array_structure"
                                              : "array_order_ambiguous");
    std::array<Slot, 9> slots{};
    for (int r = 0; r < 3; r++)
        for (int c = 0; c < 3; c++)
        {
            std::vector<int> matches;
            for (int i : rows[r])
                if (std::find(cols[c].begin(), cols[c].end(), i) != cols[c].end())
                    matches.push_back(i);
            auto &slot = slots[r * 3 + c];
            if (matches.size() != 1)
            {
                slot.reason = matches.empty() ? "cross_missing" : "duplicate_crosses";
                continue;
            }
            int index = matches[0];
            Rect e = candidates[index];
            int padx = std::max(4 * step, e.width / 2), pady = std::max(4 * step, e.height / 2);
            int left = std::max(0, e.x - padx), top = std::max(0, e.y - pady),
                right = std::min(search.width, Right(e) + padx), bottom = std::min(search.height, Bottom(e) + pady);
            Rect roi{left, top, right - left, bottom - top};
            for (int i = 0; i < static_cast<int>(candidates.size()); i++)
                if (i != index && Intersects(roi, candidates[i]))
                    slot.reason = "target_roi_overlap";
            if (static_cast<std::int64_t>(roi.width) * roi.height > 8388608)
                slot.reason = "target_roi_budget_exceeded";
            if (e.x == 0 || e.y == 0 || Right(e) >= search.width || Bottom(e) >= search.height)
                slot.reason = "cross_clipped";
            roi.x += search.x;
            roi.y += search.y;
            e.x += search.x;
            e.y += search.y;
            slot.region = roi;
            slot.evidence = e;
        }
    return slots;
}
struct Band
{
    bool valid;
    int first, last;
};
Band FindBand(const std::vector<double> &scores, double fraction)
{
    auto peak = std::max_element(scores.begin(), scores.end());
    double maximum = *peak, threshold = maximum * fraction;
    int first = static_cast<int>(peak - scores.begin()), last = first, runs = 0;
    while (first > 0 && scores[first - 1] >= threshold)
        first--;
    while (last + 1 < static_cast<int>(scores.size()) && scores[last + 1] >= threshold)
        last++;
    for (int i = 0; i < static_cast<int>(scores.size()); i++)
        if (scores[i] >= threshold && (i == 0 || scores[i - 1] < threshold))
            runs++;
    return {maximum > 0 && runs == 1, first, last};
}
double Median(std::vector<double> values)
{
    std::sort(values.begin(), values.end());
    auto mid = values.size() / 2;
    return values.size() % 2 ? values[mid] : (values[mid - 1] + values[mid]) / 2;
}
struct Target
{
    bool valid = false;
    std::string reason;
    double left = 0, right = 0, top = 0, bottom = 0;
    bool saturated = false;
};
Target Detect(const Image &image, int channel, Slot slot, const Options &p)
{
    auto reject = [](std::string reason) {
        Target t;
        t.reason = reason;
        return t;
    };
    if (!slot.reason.empty())
        return reject(slot.reason);
    Rect roi = slot.region, e = slot.evidence;
    std::vector<float> values(roi.width * roi.height);
    bool saturated = false;
    for (int y = 0; y < roi.height; y++)
        for (int x = 0; x < roi.width; x++)
        {
            float v = static_cast<float>(Sample(image, roi.x + x, roi.y + y, channel, p.decodeExponent));
            values[y * roi.width + x] = v;
            saturated |= v >= 1;
        }
    auto extrema = std::minmax_element(values.begin(), values.end());
    double minimum = *extrema.first;
    if (*extrema.second - minimum < p.minimumContrast)
        return reject("low_contrast");
    std::vector<double> rows(roi.height), cols(roi.width);
    for (int y = 0; y < roi.height; y++)
        for (int x = 0; x < roi.width; x++)
        {
            double v = values[y * roi.width + x] - minimum;
            rows[y] += v;
            cols[x] += v;
        }
    auto h = FindBand(rows, p.axisBandThreshold), v = FindBand(cols, p.axisBandThreshold);
    if (!h.valid || !v.valid)
        return reject("ambiguous_axis_bands");
    if (h.last - h.first >= e.height / 2 || v.last - v.first >= e.width / 2)
        return reject("not_a_cross");
    if (e.width < roi.width * p.minimumArmSpanFraction || e.height < roi.height * p.minimumArmSpanFraction)
        return reject("cross_arm_too_short");
    struct Edge
    {
        bool valid = false;
        std::string reason;
        double first = 0, last = 0;
    };
    auto edges = [&](bool horizontal) {
        auto fail = [](std::string reason) {
            Edge edge;
            edge.reason = reason;
            return edge;
        };
        std::vector<double> firsts, lasts;
        int start = horizontal ? e.x - roi.x : e.y - roi.y, length = horizontal ? e.width : e.height,
            transverse = horizontal ? roi.height : roi.width;
        for (int side = 0; side < 2; side++)
        {
            int count = 0, attempted = 0, ambiguous = 0;
            int from = start + static_cast<int>(length * (side == 0 ? .10 : .75)),
                to = start + static_cast<int>(length * (side == 0 ? .25 : .90));
            for (int pos = from; pos < to; pos++)
            {
                attempted++;
                std::vector<double> profile(transverse), scores(transverse);
                for (int t = 0; t < transverse; t++)
                    profile[t] = horizontal ? values[t * roi.width + pos] : values[pos * roi.width + t];
                auto mm = std::minmax_element(profile.begin(), profile.end());
                double low = *mm.first, high = *mm.second;
                if (high - low < p.minimumContrast)
                    continue;
                for (int t = 0; t < transverse; t++)
                    scores[t] = profile[t] - low;
                auto band = FindBand(scores, p.targetThreshold);
                if (!band.valid)
                {
                    ambiguous++;
                    continue;
                }
                if (band.first == 0 || band.last == transverse - 1)
                    return fail("cross_clipped");
                if (band.last - band.first >= transverse / 2)
                    return fail("not_a_cross");
                double level = low + (high - low) * p.targetThreshold;
                firsts.push_back(band.first - 1 +
                                 (level - profile[band.first - 1]) / (profile[band.first] - profile[band.first - 1]));
                lasts.push_back(band.last +
                                (profile[band.last] - level) / (profile[band.last] - profile[band.last + 1]));
                count++;
            }
            if (attempted == 0 || count < std::max(1., attempted * p.minimumArmCoverage))
                return fail(ambiguous > 0 ? "ambiguous_arm_edges" : "arm_missing_or_low_contrast");
        }
        return Edge{true, "", Median(firsts), Median(lasts)};
    };
    auto he = edges(true), ve = edges(false);
    if (!he.valid || !ve.valid)
    {
        auto t = reject((he.valid ? "" : "horizontal:" + he.reason) + (!he.valid && !ve.valid ? ";" : "") +
                        (ve.valid ? "" : "vertical:" + ve.reason));
        t.saturated = saturated;
        return t;
    }
    return {true, "", roi.x + ve.first, roi.x + ve.last, roi.y + he.first, roi.y + he.last, saturated};
}
} // namespace
json Measure(const Image &image, Rectangle search, const Options &p, const std::string &measurementId,
             const std::string &imageId)
{
    Require(image.data && image.width >= 32 && image.height >= 32 &&
                static_cast<std::int64_t>(image.width) * image.height <= 67108864,
            "image_budget_exceeded");
    Require((image.bits == 8 || image.bits == 16 || image.bits == 32) && (image.channels == 3 || image.channels == 4),
            "unsupported_format");
    Require(image.stride >= static_cast<std::size_t>(image.width) * image.channels * (image.bits / 8) &&
                image.stride <= 512ULL * 1024 * 1024 / image.height && image.size >= image.stride * image.height,
            "invalid_buffer");
    if (search.width == 0 && search.height == 0 && search.x == 0 && search.y == 0)
        search = {0, 0, image.width, image.height};
    Require(search.x >= 0 && search.y >= 0 && search.width >= 32 && search.height >= 32 &&
                static_cast<std::int64_t>(search.x) + search.width <= image.width &&
                static_cast<std::int64_t>(search.y) + search.height <= image.height,
            "invalid_roi");
    auto range = [](double v, double lo, double hi) { return std::isfinite(v) && v >= lo && v <= hi; };
    Require(range(p.minimumContrast, .000001, 1) && range(p.targetThreshold, .1, .9) &&
                range(p.axisBandThreshold, .1, .9) && range(p.minimumArmSpanFraction, .1, .9) &&
                range(p.minimumArmCoverage, .1, 1) && range(p.decodeExponent, .1, 5),
            "invalid_parameters");
    Require(!measurementId.empty() && !imageId.empty(), "missing_identity");
    auto slots = Locate(image, search, p);
    json points = json::array();
    int validCount = 0;
    double maximum = 0;
    for (int i = 0; i < 9; i++)
    {
        std::array<Target, 3> targets{Detect(image, 2, slots[i], p), Detect(image, 1, slots[i], p),
                                      Detect(image, 0, slots[i], p)};
        const char *names[] = {"R", "G", "B"};
        json channels = json::object(), reasons = json::array(), warnings = json::array();
        bool valid = true, saturated = false;
        for (int c = 0; c < 3; c++)
        {
            auto t = targets[c];
            valid &= t.valid;
            saturated |= t.saturated;
            json codes = json::array();
            if (!t.valid)
            {
                codes.push_back(t.reason);
                reasons.push_back(std::string(names[c]) + ":" + t.reason);
            }
            channels[names[c]] = {
                {"status", t.valid ? "VALID" : "INVALID"},
                {"reasonCodes", codes},
                {"horizontalArm",
                 t.valid ? Box(slots[i].evidence.x, t.top, slots[i].evidence.width, t.bottom - t.top) : json(nullptr)},
                {"verticalArm", t.valid ? Box(t.left, slots[i].evidence.y, t.right - t.left, slots[i].evidence.height)
                                        : json(nullptr)}};
        }
        if (saturated)
            warnings.push_back("saturated_samples_threshold_edges_may_be_biased");
        json separation = nullptr;
        if (valid)
        {
            validCount++;
            auto spread = [&](auto member) {
                return std::max({targets[0].*member, targets[1].*member, targets[2].*member}) -
                       std::min({targets[0].*member, targets[1].*member, targets[2].*member});
            };
            double left = spread(&Target::left), right = spread(&Target::right), top = spread(&Target::top),
                   bottom = spread(&Target::bottom), m = std::max({left, right, top, bottom});
            maximum = std::max(maximum, m);
            separation = {{"leftEdgeSpreadPx", left},
                          {"rightEdgeSpreadPx", right},
                          {"topEdgeSpreadPx", top},
                          {"bottomEdgeSpreadPx", bottom},
                          {"maximumEdgeSeparationPx", m}};
        }
        points.push_back({{"id", "P" + std::to_string(i + 1)},
                          {"row", i / 3 + 1},
                          {"column", i % 3 + 1},
                          {"status", valid ? "VALID" : "INVALID"},
                          {"reasonCodes", reasons},
                          {"warnings", warnings},
                          {"region", slots[i].region.width > 0 ? Box(slots[i].region) : json(nullptr)},
                          {"channels", channels},
                          {"separation", separation}});
    }
    return {{"schemaId", "colorvision.rgb-cross-measurement"},
            {"schemaVersion", "1.0.0"},
            {"capabilityProfile", "rgb-cross.measurement.v1"},
            {"measurementId", measurementId},
            {"algorithm", {{"id", "colorvision.display.rgb-cross-registration"}, {"version", "1.2.0"}}},
            {"source", {{"imageId", imageId}, {"width", image.width}, {"height", image.height}, {"sha256", nullptr}}},
            {"coordinates",
             {{"space", "source-image"},
              {"unit", "px"},
              {"origin", "top-left"},
              {"pixelCenters", "integer"},
              {"axes", "x-right-y-down"}}},
            {"searchRegion", Box(search)},
            {"execution", {{"status", "SUCCEEDED"}, {"reasonCodes", json::array()}}},
            {"points", points},
            {"summary",
             {{"validPointCount", validCount},
              {"invalidPointCount", 9 - validCount},
              {"complete", validCount == 9},
              {"maximumEdgeSeparationPx", validCount > 0 ? json(maximum) : json(nullptr)}}}};
}
} // namespace cvnative::rgb_cross
