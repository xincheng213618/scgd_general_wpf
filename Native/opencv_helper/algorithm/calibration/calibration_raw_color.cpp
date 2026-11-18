#include "../../../include/opencv_media_export.h"
#include "../poi/poi_batch.h"
#include "../../native_log.h"
#include <opencv2/core.hpp>
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <limits>
#include <type_traits>

#if defined(_M_X64) && !defined(_M_ARM64EC)
#include <xmmintrin.h>
#endif

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
};

template<class Action> void transformRows(int width, int height, const Action& action)
{
    cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
        const auto first = static_cast<std::size_t>(range.start) * width;
        const auto last = static_cast<std::size_t>(range.end) * width;
        action(first, last);
    });
}

template<int Stride, class Source>
void transformScaled(int width, int height, const Source* raw, double factor, float* output)
{
    transformRows(width, height, [=](std::size_t first, std::size_t last) {
        for (auto i = first; i < last; ++i) output[i] = static_cast<float>(factor * raw[i * Stride]);
    });
}

template<bool Interleaved, bool OneColor, class Source>
void transformColor(int width, int height, const Source* raw, const double* coefficients, int channel, float* output)
{
    const auto pixels = static_cast<std::size_t>(width) * height;
    const auto rgbAt = [=](std::size_t i) -> std::array<double, 3> {
        if constexpr (Interleaved) {
            const Source* bgr = raw + i * 3;
            return { static_cast<double>(bgr[2]), static_cast<double>(bgr[1]), static_cast<double>(bgr[0]) };
        }
        return { static_cast<double>(raw[i]), static_cast<double>(raw[pixels + i]), static_cast<double>(raw[pixels * 2 + i]) };
    };
    const auto firstPlane = [](const double* m, double red, double green, double blue) {
        if constexpr (OneColor) return static_cast<float>(m[0] * red + m[3] * blue);
        return static_cast<float>(m[0] * red + m[1] * green + m[2] * blue);
    };

    if (channel >= 0) {
        const double* m = coefficients + channel * 3;
        transformRows(width, height, [=](std::size_t first, std::size_t last) {
            for (auto i = first; i < last; ++i) {
                const auto [red, green, blue] = rgbAt(i);
                output[i] = firstPlane(m, red, green, blue);
            }
        });
        return;
    }

    // Load and convert RGB once for the three planar results. Keep each
    // expression in double precision, in the original operand order.
    float* const outY = output + pixels;
    float* const outZ = outY + pixels;
    const double* m = coefficients;
    transformRows(width, height, [=](std::size_t first, std::size_t last) {
        for (auto i = first; i < last; ++i) {
            const auto [red, green, blue] = rgbAt(i);
            output[i] = firstPlane(m, red, green, blue);
            if constexpr (OneColor) {
                outY[i] = static_cast<float>(m[1] * green);
                outZ[i] = static_cast<float>(m[2] * blue);
            }
            else {
                outY[i] = firstPlane(m + 3, red, green, blue);
                outZ[i] = firstPlane(m + 6, red, green, blue);
            }
        }
    });
}

template<class Source> bool transformTyped(int width, int height, const void* raw, const MRawColorTransformV1& t, int channel, float* output)
{
    const auto* input = static_cast<const Source*>(raw);
    if (t.kind == 2) {
        transformScaled<1>(width, height, input, t.coefficients[0], output);
    }
    else if (t.kind == 1 && channel > 0) {
        if (t.interleavedBgr) transformScaled<3>(width, height, input + 2 - channel, t.coefficients[channel], output);
        else {
            const auto plane = static_cast<std::size_t>(width) * height * channel;
            transformScaled<1>(width, height, input + plane, t.coefficients[channel], output);
        }
    }
    else if (t.interleavedBgr) {
        if (t.kind == 1) transformColor<true, true>(width, height, input, t.coefficients, channel, output);
        else transformColor<true, false>(width, height, input, t.coefficients, channel, output);
    }
    else {
        if (t.kind == 1) transformColor<false, true>(width, height, input, t.coefficients, channel, output);
        else transformColor<false, false>(width, height, input, t.coefficients, channel, output);
    }
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

#if defined(_M_X64) && !defined(_M_ARM64EC)
bool disjoint(
    const void* first, std::size_t firstBytes, const void* second, std::size_t secondBytes) noexcept
{
    const auto a = reinterpret_cast<std::uintptr_t>(first);
    const auto b = reinterpret_cast<std::uintptr_t>(second);
    if (firstBytes > UINTPTR_MAX - a || secondBytes > UINTPTR_MAX - b) return false;
    return a <= b ? b - a >= firstBytes : a - b >= secondBytes;
}

bool canParallelizeRawPoi(
    int width, int height, const void* raw, const MRawColorTransformV1& transform,
    const MPoiRequestV1* requests, std::uint32_t count, const MPoiOptionsV2* options, MPoiResultV1* results)
{
    constexpr std::uint64_t minimumEstimatedArea = 262'144;
    if (transform.kind != 0 || transform.interleavedBgr != 1
        || count > static_cast<std::uint32_t>((std::numeric_limits<int>::max)())
        || cv::getNumThreads() <= 1) return false;

    std::uint64_t area = 0;
    for (std::uint32_t index = 0; index < count && area < minimumEstimatedArea; ++index) {
        const auto& request = requests[index];
        if (request.type == 0) continue;
        // The rectangle scanner includes both ends of each integer half-size.
        const bool rectangle = request.type == 2;
        const auto columns = rectangle ? request.width / 2 * 2 + 1 : request.width;
        const auto rows = rectangle ? request.height / 2 * 2 + 1 : request.width;
        area += static_cast<std::uint64_t>((std::min)(width, columns)) * (std::min)(height, rows);
    }
    if (area < minimumEstimatedArea) return false;

    // The C export does not reject overlap. Retain sequential behavior for such callers.
    const auto resultBytes = static_cast<std::size_t>(count) * sizeof(*results);
    const auto rawBytes = static_cast<std::size_t>(width) * height * 3 * sizeof(std::uint16_t);
    return disjoint(results, resultBytes, raw, rawBytes)
        && disjoint(results, resultBytes, requests, static_cast<std::size_t>(count) * sizeof(*requests))
        && disjoint(results, resultBytes, &transform, sizeof(transform))
        && disjoint(results, resultBytes, options, sizeof(*options));
}
#endif

template<class Source> bool poiTyped(int width, int height, const void* raw, const MRawColorTransformV1& t,
    const MPoiRequestV1* requests, std::uint32_t count, const MPoiOptionsV2* options, MPoiResultV1* results)
{
    Reader<Source> reader{static_cast<const Source*>(raw), t, static_cast<std::size_t>(width) * height};
    const auto calculate = [&](std::uint32_t k) {
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
        return sum.finish(t.channels, options, results + k);
    };
#if defined(_M_X64) && !defined(_M_ARM64EC)
    if constexpr (std::is_same_v<Source, std::uint16_t>) {
        constexpr unsigned exceptionMask = 0x3fU;
        constexpr unsigned controlMask = 0xffc0U;
        constexpr unsigned defaultControls = 0x1f80U;
        constexpr unsigned retrySerial = 0x40U;
        const unsigned callerState = _mm_getcsr();
        if (count >= 12 && (callerState & controlMask) == defaultControls
            && canParallelizeRawPoi(width, height, raw, t, requests, count, options, results)) {
            std::atomic<unsigned> outcome{0};
            cv::parallel_for_(cv::Range(0, static_cast<int>(count)), [&](const cv::Range& range) {
                const unsigned workerState = _mm_getcsr();
                if ((workerState & controlMask) != defaultControls) {
                    outcome.fetch_or(retrySerial, std::memory_order_relaxed);
                    return;
                }

                // Forward the exception flags to the caller without modifying
                // the incoming floating point state of an OpenCV worker.
                _mm_setcsr(workerState & ~exceptionMask);
                unsigned retry = 0;
                try {
                    for (int index = range.start; index < range.end; ++index) {
                        if (!calculate(static_cast<std::uint32_t>(index))) {
                            retry = retrySerial;
                            break;
                        }
                    }
                }
                catch (...) {
                    _mm_setcsr(workerState);
                    throw;
                }
                const unsigned raised = _mm_getcsr() & exceptionMask;
                _mm_setcsr(workerState);
                outcome.fetch_or(raised | retry, std::memory_order_relaxed);
            }, count);
            const unsigned status = outcome.load(std::memory_order_relaxed);
            if ((status & retrySerial) == 0) {
                _mm_setcsr(callerState | status);
                return true;
            }
            // All requests use the same options. Run the original loop on an
            // invalid batch or a worker with a different floating point mode.
            _mm_setcsr(callerState);
        }
    }
#endif
    for (std::uint32_t k = 0; k < count; ++k) {
        if (!calculate(k)) return false;
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
