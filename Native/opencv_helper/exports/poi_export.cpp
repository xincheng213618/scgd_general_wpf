#include "../algorithm/poi/poi_batch.h"
#include "../../include/opencv_media_export.h"

#include <cstddef>
#include <cstdint>
#include <limits>

extern "C" COLORVISIONCORE_API int __cdecl M_CalculatePoiBatchV1(
    std::int32_t width,
    std::int32_t height,
    std::int32_t bitsPerChannel,
    std::int32_t channels,
    const float* cieData,
    std::uint64_t cieFloatCount,
    const MPoiRequestV1* requests,
    std::uint32_t requestCount,
    MPoiResultV1* results)
{
    if (cieFloatCount > (std::numeric_limits<std::size_t>::max)()) {
        return M_POI_INVALID_ARGUMENT;
    }
    const auto* nativeRequests = reinterpret_cast<const cvcore::poi::RequestV1*>(requests);
    auto* nativeResults = reinterpret_cast<cvcore::poi::ResultV1*>(results);
    return cvcore::poi::calculateBatchV1(
        width, height, bitsPerChannel, channels, cieData,
        static_cast<std::size_t>(cieFloatCount), nativeRequests, requestCount, nativeResults)
        ? M_POI_OK
        : M_POI_INVALID_ARGUMENT;
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalculatePoiBatchV2(
    std::int32_t width,
    std::int32_t height,
    std::int32_t bitsPerChannel,
    std::int32_t channels,
    const float* cieData,
    std::uint64_t cieFloatCount,
    const MPoiRequestV1* requests,
    std::uint32_t requestCount,
    const MPoiOptionsV2* options,
    MPoiResultV1* results)
{
    if (cieFloatCount > (std::numeric_limits<std::size_t>::max)()) {
        return M_POI_INVALID_ARGUMENT;
    }
    try {
        const auto* nativeRequests = reinterpret_cast<const cvcore::poi::RequestV1*>(requests);
        const auto* nativeOptions = reinterpret_cast<const cvcore::poi::OptionsV2*>(options);
        auto* nativeResults = reinterpret_cast<cvcore::poi::ResultV1*>(results);
        return cvcore::poi::calculateBatchV2(
            width, height, bitsPerChannel, channels, cieData,
            static_cast<std::size_t>(cieFloatCount), nativeRequests, requestCount, nativeOptions, nativeResults)
            ? M_POI_OK
            : M_POI_INVALID_ARGUMENT;
    }
    catch (...) {
        return M_POI_INTERNAL_ERROR;
    }
}
