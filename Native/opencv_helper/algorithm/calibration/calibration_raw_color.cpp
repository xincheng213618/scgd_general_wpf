#include "../../../include/opencv_media_export.h"
#include "../poi/poi_batch.h"
#include "../../native_log.h"
#include <opencv2/core.hpp>
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <limits>

#pragma float_control(precise, on)
#pragma fp_contract(off)

namespace {
template<class Action> int guard(const char* operation, Action action) noexcept
{
    try { return action() ? M_POI_OK : M_POI_INVALID_ARGUMENT; }
    catch (const std::exception& ex) { cvnative::LogException("raw.color", operation, M_POI_INTERNAL_ERROR, "std::exception", ex.what()); }
    catch (...) { cvnative::LogException("raw.color", operation, M_POI_INTERNAL_ERROR, "unknown"); }
    return M_POI_INTERNAL_ERROR;
}

bool valid(int width, int height, int bpp, const void* raw, std::uint64_t bytes, const MRawColorTransformV1* t)
{
    if (width <= 0 || height <= 0 || (bpp != 8 && bpp != 16) || raw == nullptr || t == nullptr
        || t->structSize < sizeof(*t) || t->reserved != 0 || t->kind < 0 || t->kind > 2
        || (t->kind == 2 ? t->channels != 1 : t->channels != 3)
        || (t->interleavedBgr != 0 && t->interleavedBgr != 1)) return false;
    const auto pixels = static_cast<std::uint64_t>(width) * height;
    const auto stride = static_cast<std::uint64_t>(t->channels) * (bpp / 8);
    if (pixels > (std::numeric_limits<std::size_t>::max)() / stride || bytes < pixels * stride) return false;
    for (double coefficient : t->coefficients) if (!std::isfinite(coefficient)) return false;
    return true;
}

template<class Source> struct Reader {
    const Source* raw;
    const MRawColorTransformV1& transform;
    std::size_t pixels;
    float at(std::size_t i, int channel) const
    {
        const double* m = transform.coefficients;
        if (transform.kind == 2) return static_cast<float>(m[0] * raw[i]);
        double r, g, b;
        if (transform.interleavedBgr) { const Source* p = raw + i * 3; r = p[2]; g = p[1]; b = p[0]; }
        else { r = raw[i]; g = raw[pixels + i]; b = raw[pixels * 2 + i]; }
        if (transform.kind == 1) {
            if (channel == 0) return static_cast<float>(m[0] * r + m[3] * b);
            return static_cast<float>(channel == 1 ? m[1] * g : m[2] * b);
        }
        m += channel * 3;
        return static_cast<float>(m[0] * r + m[1] * g + m[2] * b);
    }
};

template<class Source> bool transformTyped(int width, int height, const void* raw, const MRawColorTransformV1& t, int channel, float* output)
{
    Reader<Source> reader{static_cast<const Source*>(raw), t, static_cast<std::size_t>(width) * height};
    cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
        for (int y = range.start; y < range.end; ++y) {
            const std::size_t first = static_cast<std::size_t>(y) * width, last = first + width;
            for (int c = channel < 0 ? 0 : channel; c < (channel < 0 ? t.channels : channel + 1); ++c) {
                float* plane = output + (channel < 0 ? reader.pixels * c : 0);
                for (std::size_t i = first; i < last; ++i) plane[i] = reader.at(i, c);
            }
        }
    });
    return true;
}

struct Sum {
    std::array<double, 3> values{};
    std::uint64_t count = 0;
    template<class Source> void add(const Reader<Source>& reader, std::size_t i)
    {
        // Load RGB once per pixel. Calling the single-channel accessor three times
        // repeatedly branches on layout/kind and doubles the cost of large POIs.
        const double* m = reader.transform.coefficients;
        if (reader.transform.kind == 2) values[0] += static_cast<float>(m[0] * reader.raw[i]);
        else {
            double r, g, b;
            if (reader.transform.interleavedBgr) {
                const Source* p = reader.raw + i * 3;
                r = p[2]; g = p[1]; b = p[0];
            }
            else { r = reader.raw[i]; g = reader.raw[reader.pixels + i]; b = reader.raw[reader.pixels * 2 + i]; }
            if (reader.transform.kind == 1) {
                values[0] += static_cast<float>(m[0] * r + m[3] * b);
                values[1] += static_cast<float>(m[1] * g);
                values[2] += static_cast<float>(m[2] * b);
            }
            else {
                values[0] += static_cast<float>(m[0] * r + m[1] * g + m[2] * b);
                values[1] += static_cast<float>(m[3] * r + m[4] * g + m[5] * b);
                values[2] += static_cast<float>(m[6] * r + m[7] * g + m[8] * b);
            }
        }
        ++count;
    }
    bool finish(int channels, const MPoiOptionsV2* options, MPoiResultV1* result) const
    {
        if (count == 0) return false;
        float average[3]{};
        for (int c = 0; c < channels; ++c) average[c] = static_cast<float>(values[c] / count);
        cvcore::poi::RequestV1 point{0, 0, 0, 1, 1};
        return cvcore::poi::calculateBatchV2(1, 1, 32, channels, average, channels, &point, 1,
            reinterpret_cast<const cvcore::poi::OptionsV2*>(options), reinterpret_cast<cvcore::poi::ResultV1*>(result));
    }
};

template<class Source> bool poiTyped(int width, int height, const void* raw, const MRawColorTransformV1& t,
    const MPoiRequestV1* requests, std::uint32_t count, const MPoiOptionsV2* options, MPoiResultV1* results)
{
    Reader<Source> reader{static_cast<const Source*>(raw), t, static_cast<std::size_t>(width) * height};
    for (std::uint32_t k = 0; k < count; ++k) {
        const auto& q = requests[k];
        Sum sum;
        if (q.type == 0) sum.add(reader, static_cast<std::size_t>(q.y) * width + q.x);
        else {
            const double rx = q.width / 2.0;
            // The legacy unfiltered rectangle intentionally uses integer half sizes and +0.5.
            const int firstY = static_cast<int>((std::max)(0.0, q.y - (q.type == 2 ? q.height / 2 : rx) + (q.type == 2 ? 0.5 : 0)));
            const int lastY = static_cast<int>((std::min)(static_cast<double>(height - 1), q.y + (q.type == 2 ? q.height / 2 : rx)));
            const int firstX = static_cast<int>((std::max)(0.0, q.x - (q.type == 2 ? q.width / 2 : rx) + (q.type == 2 ? 0.5 : 0)));
            const int lastX = static_cast<int>((std::min)(static_cast<double>(width - 1), q.x + (q.type == 2 ? q.width / 2 : rx)));
            for (int y = firstY; y <= lastY; ++y) for (int x = firstX; x <= lastX; ++x) {
                const double dx = static_cast<double>(x) - q.x, dy = static_cast<double>(y) - q.y;
                if (q.type == 2 || dx * dx + dy * dy < rx * rx) sum.add(reader, static_cast<std::size_t>(y) * width + x);
            }
            if (sum.count == 0) sum.add(reader, static_cast<std::size_t>(q.y) * width + q.x);
        }
        if (!sum.finish(t.channels, options, results + k)) return false;
    }
    return true;
}

template<class Source> bool regionTyped(int width, int height, const void* raw, const MRawColorTransformV1& t,
    const MRawPixelRunV1* runs, std::uint32_t count, const MPoiOptionsV2* options, MPoiResultV1* result)
{
    Reader<Source> reader{static_cast<const Source*>(raw), t, static_cast<std::size_t>(width) * height};
    Sum sum;
    for (std::uint32_t k = 0; k < count; ++k)
        for (int x = runs[k].startX; x < runs[k].endX; ++x) sum.add(reader, static_cast<std::size_t>(runs[k].y) * width + x);
    return sum.finish(t.channels, options, result);
}
}

extern "C" COLORVISIONCORE_API int __cdecl M_TransformRawColorV1(int width, int height, int bpp,
    const void* raw, std::uint64_t rawBytes, const MRawColorTransformV1* t, int channel, float* output, std::uint64_t floats)
{
    return guard(__func__, [&] {
        if (!valid(width, height, bpp, raw, rawBytes, t) || output == nullptr || channel < -1 || channel >= t->channels) return false;
        const std::uint64_t required = static_cast<std::uint64_t>(width) * height * (channel < 0 ? t->channels : 1);
        if (required > (std::numeric_limits<std::size_t>::max)() / sizeof(float) || floats < required) return false;
        const auto a = reinterpret_cast<std::uintptr_t>(raw), b = reinterpret_cast<std::uintptr_t>(output);
        const auto rawLength = static_cast<std::uint64_t>(width) * height * t->channels * (bpp / 8);
        const auto outputLength = required * sizeof(float);
        if (a > UINTPTR_MAX - rawLength || b > UINTPTR_MAX - outputLength || (a < b + outputLength && b < a + rawLength)) return false;
        return bpp == 8 ? transformTyped<std::uint8_t>(width, height, raw, *t, channel, output)
            : transformTyped<std::uint16_t>(width, height, raw, *t, channel, output);
    });
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalculateRawPoiBatchV1(int width, int height, int bpp,
    const void* raw, std::uint64_t rawBytes, const MRawColorTransformV1* t,
    const MPoiRequestV1* requests, std::uint32_t count, const MPoiOptionsV2* options, MPoiResultV1* results)
{
    return guard(__func__, [&] {
        if (!valid(width, height, bpp, raw, rawBytes, t) || requests == nullptr || count == 0 || results == nullptr
            || options == nullptr || options->structSize < sizeof(*options) || options->filterMode != 0) return false;
        for (std::uint32_t k = 0; k < count; ++k) {
            const auto& q = requests[k];
            if (q.type < 0 || q.type > 2 || q.width <= 0 || q.height <= 0 || q.x < 0 || q.x >= width || q.y < 0 || q.y >= height) return false;
        }
        return bpp == 8 ? poiTyped<std::uint8_t>(width, height, raw, *t, requests, count, options, results)
            : poiTyped<std::uint16_t>(width, height, raw, *t, requests, count, options, results);
    });
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalculateRawRegionV1(int width, int height, int bpp,
    const void* raw, std::uint64_t rawBytes, const MRawColorTransformV1* t,
    const MRawPixelRunV1* runs, std::uint32_t count, const MPoiOptionsV2* options, MPoiResultV1* result)
{
    return guard(__func__, [&] {
        if (!valid(width, height, bpp, raw, rawBytes, t) || runs == nullptr || count == 0 || result == nullptr
            || options == nullptr || options->structSize < sizeof(*options) || options->filterMode != 0) return false;
        for (std::uint32_t k = 0; k < count; ++k)
            if (runs[k].y < 0 || runs[k].y >= height || runs[k].startX < 0 || runs[k].endX > width || runs[k].endX <= runs[k].startX) return false;
        return bpp == 8 ? regionTyped<std::uint8_t>(width, height, raw, *t, runs, count, options, result)
            : regionTyped<std::uint16_t>(width, height, raw, *t, runs, count, options, result);
    });
}
