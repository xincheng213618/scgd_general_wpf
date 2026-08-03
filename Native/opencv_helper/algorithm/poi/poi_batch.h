#pragma once

#include <cstddef>
#include <cstdint>

namespace cvcore::poi {

struct RequestV1 {
    std::int32_t type = 0;
    std::int32_t x = 0;
    std::int32_t y = 0;
    std::int32_t width = 0;
    std::int32_t height = 0;
};

struct ResultV1 {
    float X = 0;
    float Y = 0;
    float Z = 0;
    float x = 0;
    float y = 0;
    float u = 0;
    float v = 0;
    float cct = 0;
    float wave = 0;
};

enum OptionsFlagsV2 : std::uint32_t {
    PercentThreshold = 1U,
    ApplyMnp = 2U,
};

struct OptionsV2 {
    std::uint32_t structSize = 0;
    std::uint32_t flags = 0;
    std::int32_t filterMode = 0;
    std::int32_t xyzChannel = 0;
    float threshold = 0;
    float maxPercent = 0;
    float scaleX = 1;
    float scaleY = 1;
    float scaleZ = 1;
    std::uint32_t reserved[3]{};
};

static_assert(sizeof(RequestV1) == 20, "POI request ABI changed");
static_assert(sizeof(ResultV1) == 36, "POI result ABI changed");
static_assert(sizeof(OptionsV2) == 48, "POI V2 options ABI changed");

bool calculateBatchV1(
    std::int32_t width,
    std::int32_t height,
    std::int32_t bitsPerChannel,
    std::int32_t channels,
    const float* cieData,
    std::size_t cieFloatCount,
    const RequestV1* requests,
    std::uint32_t requestCount,
    ResultV1* results) noexcept;

bool calculateBatchV2(
    std::int32_t width,
    std::int32_t height,
    std::int32_t bitsPerChannel,
    std::int32_t channels,
    const float* cieData,
    std::size_t cieFloatCount,
    const RequestV1* requests,
    std::uint32_t requestCount,
    const OptionsV2* options,
    ResultV1* results);

} // namespace cvcore::poi
