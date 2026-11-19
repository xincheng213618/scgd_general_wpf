#include "calibration_core.h"

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>
#include <io.h>
#include <opencv2/core/utility.hpp>
#include <share.h>

#if defined(_M_X64) && !defined(_M_ARM64EC)
#include <emmintrin.h>
#include <smmintrin.h>
#include <tmmintrin.h>
#endif

#include <algorithm>
#include <bit>
#include <cctype>
#include <cerrno>
#include <cmath>
#include <cstddef>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <limits>
#include <stdexcept>
#include <string_view>
#include <system_error>
#include <type_traits>
#include <utility>
#include <vector>

namespace cvcore::calibration {
namespace {

constexpr std::uint64_t kLegacyMapHeaderBytes = 8;
constexpr std::uint64_t kCurrentMapHeaderBytes = 20;
constexpr std::uint32_t kMinimumParallelRows = 8;

template <typename T>
bool checkedMultiply(T left, T right, T& result)
{
    if (left != 0 && right > (std::numeric_limits<T>::max)() / left) {
        return false;
    }
    result = left * right;
    return true;
}

bool validDistinctRawOutput(
    const ImageView& source,
    const ImageView& destination,
    std::size_t& sampleCount,
    std::size_t& byteCount,
    std::string& error)
{
    if (source.data == nullptr || destination.data == nullptr
        || source.data == destination.data
        || source.width != destination.width
        || source.height != destination.height
        || source.bitsPerChannel != destination.bitsPerChannel
        || source.channels != destination.channels) {
        error = "Calibration source and destination layouts do not match";
        return false;
    }

    if (!checkedMultiply<std::size_t>(source.width, source.height, sampleCount)
        || !checkedMultiply<std::size_t>(sampleCount, source.channels, sampleCount)
        || !checkedMultiply<std::size_t>(
            sampleCount, source.bitsPerChannel / 8, byteCount)
        || source.dataLength < byteCount
        || destination.dataLength < byteCount) {
        error = "Calibration source or destination buffer is too small";
        return false;
    }
    return true;
}

bool checkedAdd(std::uint64_t left, std::uint64_t right, std::uint64_t& result)
{
    if (right > (std::numeric_limits<std::uint64_t>::max)() - left) {
        return false;
    }
    result = left + right;
    return true;
}

struct CloseBinaryFile {
    void operator()(std::FILE* file) const noexcept { std::fclose(file); }
};
using BinaryFile = std::unique_ptr<std::FILE, CloseBinaryFile>;

bool seekAndRead(
    const BinaryFile& stream,
    std::uint64_t offset,
    void* destination,
    std::uint64_t byteCount,
    std::string& error)
{
    if (offset > static_cast<std::uint64_t>((std::numeric_limits<std::int64_t>::max)())
        || byteCount > static_cast<std::uint64_t>((std::numeric_limits<std::ptrdiff_t>::max)())) {
        error = "Calibration file is too large for the native stream API";
        return false;
    }

    if (_fseeki64(stream.get(), static_cast<std::int64_t>(offset), SEEK_SET) != 0) {
        error = "Unable to seek in calibration file";
        return false;
    }

    // The MSVC binary file stream splits bulk reads into roughly 4 KiB calls.
    constexpr std::size_t blockBytes = 32 * 1024 * 1024;
    auto* bytes = static_cast<std::uint8_t*>(destination);
    for (auto remaining = static_cast<std::size_t>(byteCount); remaining != 0;) {
        const auto count = (std::min)(remaining, blockBytes);
        if (std::fread(bytes, 1, count, stream.get()) != count) {
            error = "Calibration file ended before all data was read";
            return false;
        }
        bytes += count;
        remaining -= count;
    }
    return true;
}

template <typename T>
bool readPod(const BinaryFile& stream, T& value, std::string& error)
{
    if (std::fread(&value, 1, sizeof(value), stream.get()) != sizeof(value)) {
        error = "Calibration file header is incomplete";
        return false;
    }
    return true;
}

bool openBinary(
    const std::filesystem::path& file,
    BinaryFile& stream,
    std::uint64_t& size,
    std::string& error)
{
    // Match the old binary stream's wide-path open and read/write sharing.
    stream.reset(_wfsopen(file.c_str(), L"rb", _SH_DENYNO));
    if (!stream) {
        error = "Unable to open calibration file";
        return false;
    }
    const auto handle = reinterpret_cast<HANDLE>(_get_osfhandle(_fileno(stream.get())));
    LARGE_INTEGER fileLength{};
    if (!GetFileSizeEx(handle, &fileLength)) {
        const auto code = GetLastError();
        error = "Unable to read calibration file size: " + std::system_category().message(static_cast<int>(code));
        return false;
    }
    size = static_cast<std::uint64_t>(fileLength.QuadPart);
    return true;
}

bool readTextFile(const std::filesystem::path& file, std::string& text, std::string& error)
{
    BinaryFile stream;
    std::uint64_t size = 0;
    if (!openBinary(file, stream, size, error)) {
        return false;
    }
    if (size > static_cast<std::uint64_t>((std::numeric_limits<std::size_t>::max)())) {
        error = "Calibration JSON is too large";
        return false;
    }

    text.resize(static_cast<std::size_t>(size));
    return seekAndRead(stream, 0, text.data(), size, error);
}

std::size_t skipWhitespace(std::string_view text, std::size_t position)
{
    while (position < text.size()
        && std::isspace(static_cast<unsigned char>(text[position])) != 0) {
        ++position;
    }
    return position;
}

bool looksLikeJsonObject(std::string_view text)
{
    const auto first = skipWhitespace(text, 0);
    if (first == text.size() || text[first] != '{') {
        return false;
    }

    auto last = text.size();
    while (last > first + 1
        && std::isspace(static_cast<unsigned char>(text[last - 1])) != 0) {
        --last;
    }
    return last > first + 1 && text[last - 1] == '}';
}

bool findJsonNumber(
    const std::string& text,
    std::string_view key,
    double& value,
    bool& representedAsReal)
{
    const std::string quotedKey = "\"" + std::string(key) + "\"";
    auto position = text.find(quotedKey);
    while (position != std::string::npos) {
        position = skipWhitespace(text, position + quotedKey.size());
        if (position >= text.size() || text[position] != ':') {
            position = text.find(quotedKey, position);
            continue;
        }
        position = skipWhitespace(text, position + 1);
        if (position >= text.size()) {
            return false;
        }

        const char* begin = text.c_str() + position;
        char* end = nullptr;
        errno = 0;
        const double parsed = std::strtod(begin, &end);
        if (end == begin || errno == ERANGE || !std::isfinite(parsed)) {
            return false;
        }

        const std::string_view token(begin, static_cast<std::size_t>(end - begin));
        representedAsReal = token.find_first_of(".eE") != std::string_view::npos;
        value = parsed;
        return true;
    }
    return false;
}

template <typename TAction>
void parallelRows(std::uint32_t rowCount, const TAction& action)
{
    if (rowCount == 0) {
        return;
    }
    if (rowCount < kMinimumParallelRows
        || rowCount > static_cast<std::uint32_t>((std::numeric_limits<int>::max)())) {
        action(0, rowCount);
        return;
    }

    cv::parallel_for_(cv::Range(0, static_cast<int>(rowCount)),
        [&action](const cv::Range& range) {
            action(static_cast<std::uint32_t>(range.start), static_cast<std::uint32_t>(range.end));
        });
}

bool hasLegacyRoi(
    const ExecutionOptions& options,
    std::uint32_t mapWidth,
    std::uint32_t mapHeight)
{
    const auto x = options.roi[0];
    const auto y = options.roi[1];
    const auto width = options.roi[2];
    const auto height = options.roi[3];

    // Keep the legacy strict comparison. An ROI ending exactly on the map's
    // right or bottom edge was not treated as an ROI by cvCamera.
    return width != 0 && height != 0
        && static_cast<std::uint64_t>(x) + width < mapWidth
        && static_cast<std::uint64_t>(y) + height < mapHeight;
}

template <typename T>
bool validateSpan(
    std::size_t totalCount,
    std::size_t base,
    std::size_t rowStride,
    std::size_t rowCount,
    std::size_t rowWidth,
    std::size_t elementStride,
    std::string_view name,
    std::string& error)
{
    static_assert(!std::is_void_v<T>);
    if (rowCount == 0 || rowWidth == 0) {
        return true;
    }
    if (base >= totalCount) {
        error = std::string(name) + " starts outside its buffer";
        return false;
    }

    std::size_t rowOffset = 0;
    std::size_t elementOffset = 0;
    if (!checkedMultiply(rowCount - 1, rowStride, rowOffset)
        || !checkedMultiply(rowWidth - 1, elementStride, elementOffset)) {
        error = std::string(name) + " span overflows address arithmetic";
        return false;
    }
    if (rowOffset > (std::numeric_limits<std::size_t>::max)() - base
        || elementOffset > (std::numeric_limits<std::size_t>::max)() - base - rowOffset) {
        error = std::string(name) + " span overflows address arithmetic";
        return false;
    }
    const auto last = base + rowOffset + elementOffset;
    if (last >= totalCount) {
        error = std::string(name) + " is smaller than the requested image/ROI";
        return false;
    }
    return true;
}

struct DsnuOperation {
    template <typename TPixel, typename TCorrection>
    static void apply(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t count,
        const TCorrection* __restrict correction,
        std::size_t correctionStride)
    {
        if (correctionStride == 1) {
            for (std::size_t index = 0; index < count; ++index) {
                const auto source = sourcePixels[index];
                const auto offset = correction[index];
                destinationPixels[index] = source < offset ? TPixel{} : static_cast<TPixel>(source - offset);
            }
            return;
        }
        for (std::size_t index = 0; index < count; ++index) {
            const auto source = sourcePixels[index];
            const auto offset = correction[index * correctionStride];
            destinationPixels[index] = source < offset ? TPixel{} : static_cast<TPixel>(source - offset);
        }
    }
};

bool hasFiniteNonnegativeGains(const std::vector<float>& gains)
{
    std::size_t index = 0;
#if defined(_M_X64) && !defined(_M_ARM64EC)
    const auto sign = _mm_set1_epi32(std::bit_cast<std::int32_t>(-0.0F));
    const auto maxFinite = _mm_set1_epi32(std::bit_cast<std::int32_t>((std::numeric_limits<float>::max)()));
    const auto limit = _mm_xor_si128(maxFinite, sign);
    for (; gains.size() - index >= 4; index += 4) {
        auto bits = _mm_loadu_si128(reinterpret_cast<const __m128i*>(gains.data() + index));
        // Treat -0 as +0, then compare the IEEE bits as unsigned integers.
        bits = _mm_andnot_si128(_mm_cmpeq_epi32(bits, sign), bits);
        const auto invalid = _mm_cmpgt_epi32(_mm_xor_si128(bits, sign), limit);
        if (_mm_movemask_epi8(invalid) != 0) return false;
    }
#endif
    return std::all_of(gains.cbegin() + index, gains.cend(), [](float gain) {
        return std::isfinite(gain) && gain >= 0.0F;
    });
}

struct UniformityOperation {
    template <typename TPixel>
    static void apply(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t count,
        const float* __restrict correction,
        std::size_t correctionStride)
    {
        constexpr auto maximum = (std::numeric_limits<TPixel>::max)();
        if (correctionStride == 1) {
            for (std::size_t index = 0; index < count; ++index) {
                const float corrected = correction[index] * sourcePixels[index];
                destinationPixels[index] = corrected > static_cast<float>(maximum)
                    ? maximum
                    : static_cast<TPixel>(corrected);
            }
            return;
        }
        for (std::size_t index = 0; index < count; ++index) {
            const float corrected = correction[index * correctionStride] * sourcePixels[index];
            destinationPixels[index] = corrected > static_cast<float>(maximum)
                ? maximum
                : static_cast<TPixel>(corrected);
        }
    }
};

#if defined(_M_X64) && !defined(_M_ARM64EC)
__m128i darkCorrectEight(const std::uint16_t* source, const std::uint8_t* offsets)
{
    const auto raw = _mm_loadu_si128(reinterpret_cast<const __m128i*>(source));
    const auto dark = _mm_loadl_epi64(reinterpret_cast<const __m128i*>(offsets));
    return _mm_subs_epu16(raw, _mm_unpacklo_epi8(dark, _mm_setzero_si128()));
}

bool hasSparseDarkCorrection(const std::uint16_t* source, const std::uint8_t* offsets, std::size_t count)
{
    constexpr std::size_t probes = 64;
    const auto step = (count - 8) / (probes - 1);
    std::size_t misses = 0;
    for (std::size_t probe = 0; probe < probes; ++probe) {
        const auto index = probe * step;
        const auto dark = darkCorrectEight(source + index, offsets + index);
        if (_mm_movemask_epi8(_mm_cmpeq_epi16(dark, _mm_setzero_si128())) != 0xFFFF
            && ++misses > probes / 4) {
            return false;
        }
    }
    return true;
}

bool hasSparseCroppedDarkCorrection(const std::uint16_t* source, const std::uint8_t* offsets,
    std::size_t rawRowSamples, std::size_t mapRowSamples, std::uint32_t rows)
{
    constexpr std::size_t probes = 64;
    constexpr std::size_t lanes = 8;
    const auto count = rawRowSamples * rows;
    // Keep the probe small compared with the crop and never cross a map row.
    if (rawRowSamples < lanes || count < probes * lanes * 8) return false;
    const auto step = (count - lanes) / (probes - 1);
    std::size_t misses = 0;
    for (std::size_t probe = 0; probe < probes; ++probe) {
        const auto index = probe * step;
        const auto row = index / rawRowSamples;
        const auto column = (std::min)(index % rawRowSamples, rawRowSamples - lanes);
        const auto dark = darkCorrectEight(source + row * rawRowSamples + column,
            offsets + row * mapRowSamples + column);
        if (_mm_movemask_epi8(_mm_cmpeq_epi16(dark, _mm_setzero_si128())) != 0xFFFF
            && ++misses > probes / 4) {
            return false;
        }
    }
    return true;
}
#endif

// Store exact IEEE float bits as 24-bit deltas within small blocks. Blocks with
// a wider bit range stay 32-bit; this never quantizes gains or changes arithmetic.
class PackedUniformity {
public:
    static std::unique_ptr<PackedUniformity> create(const std::vector<float>& values)
    {
        if (values.size() < 16ULL * 1024 * 1024
            || values.size() > (UINT32_MAX - sizeof(std::uint32_t)) / sizeof(float)) return nullptr;
        auto result = std::make_unique<PackedUniformity>();
        result->count_ = values.size();
        result->blocks_.resize((values.size() + blockSize - 1) / blockSize);
        const auto blockCount = static_cast<std::uint32_t>(result->blocks_.size());
#if defined(_M_X64) && !defined(_M_ARM64EC)
        const bool useSse41 = cv::checkHardwareSupport(CV_CPU_SSE4_1);
        const bool useSsse3 = cv::checkHardwareSupport(CV_CPU_SSSE3);
#else
        constexpr bool useSse41 = false, useSsse3 = false;
#endif
        parallelRows(blockCount, [&](std::uint32_t first, std::uint32_t end) {
            for (auto block = first; block < end; ++block) {
                const auto begin = static_cast<std::size_t>(block) * blockSize;
                const auto count = (std::min)(blockSize, values.size() - begin);
                result->blocks_[block].base = packedBase(values.data() + begin, count, useSse41);
            }
        });
        std::size_t bytes = 0;
        for (std::size_t block = 0; block < result->blocks_.size(); ++block) {
            auto& header = result->blocks_[block];
            header.offset = static_cast<std::uint32_t>(bytes);
            bytes += (std::min)(blockSize, values.size() - block * blockSize)
                * (header.base == UINT32_MAX ? sizeof(float) : 3);
        }
        const auto originalBytes = values.size() * sizeof(float);
        // Limit decoding to large maps with at least 20% less table traffic.
        if (bytes + result->blocks_.size() * sizeof(Block) > originalBytes - originalBytes / 5) return nullptr;
        result->byteCount_ = bytes + sizeof(std::uint32_t);
        if (cv::getNumThreads() >= 4) {
            // Parallel packing writes every payload byte. Initialize just the
            // padding for the last packed sample's unaligned 32-bit read.
            result->bytes_ = std::make_unique_for_overwrite<std::uint8_t[]>(result->byteCount_);
            std::memset(result->bytes_.get() + bytes, 0, sizeof(std::uint32_t));
        }
        else {
            // With few workers, pre-initializing pages is faster than making
            // those workers fault in the pages while packing.
            result->bytes_ = std::make_unique<std::uint8_t[]>(result->byteCount_);
        }
        parallelRows(blockCount, [&](std::uint32_t first, std::uint32_t end) {
            for (auto block = first; block < end; ++block) {
                const auto header = result->blocks_[block];
                const auto begin = static_cast<std::size_t>(block) * blockSize;
                const auto count = (std::min)(blockSize, values.size() - begin);
                auto* destination = result->bytes_.get() + header.offset;
                if (header.base == UINT32_MAX) {
                    std::memcpy(destination, values.data() + begin, count * sizeof(float));
                }
                else {
                    writeDeltas(values.data() + begin, count, header.base, destination, useSsse3);
                }
            }
        });
        return result;
    }

    std::size_t size() const noexcept { return count_; }
    std::uint64_t footprintBytes() const noexcept
    {
        return sizeof(*this) + byteCount_ + static_cast<std::uint64_t>(blocks_.capacity()) * sizeof(Block);
    }

    float at(std::size_t index) const noexcept
    {
        const auto header = blocks_[index / blockSize];
        const bool packed = header.base != UINT32_MAX;
        std::uint32_t bits;
        std::memcpy(&bits, bytes_.get() + header.offset + (index % blockSize) * (packed ? 3 : 4), sizeof(bits));
        if (packed) bits = (bits & 0xFFFFFFU) + header.base;
        return std::bit_cast<float>(bits);
    }

    template<bool Fused, bool SkipDarkZeros = false, typename Pixel, typename Offset = std::uint8_t>
    void apply(const Pixel* source, Pixel* destination, std::size_t count, std::size_t first,
        std::size_t stride, const Offset* offsets = nullptr) const
    {
        if (stride != 1) {
            constexpr auto maximum = (std::numeric_limits<Pixel>::max)();
            for (std::size_t index = 0; index < count; ++index) {
                auto value = source[index];
                if constexpr (Fused) value = value < offsets[index] ? Pixel{} : static_cast<Pixel>(value - offsets[index]);
                const float corrected = at(first + index * stride) * value;
                destination[index] = corrected > static_cast<float>(maximum) ? maximum : static_cast<Pixel>(corrected);
            }
            return;
        }
        for (std::size_t done = 0; done < count;) {
            const auto index = first + done;
            const auto header = blocks_[index / blockSize];
            const auto offset = index % blockSize;
            const auto length = (std::min)(blockSize - offset, count - done);
            const auto* dark = offsets;
            if constexpr (Fused) dark += done;
            if (header.base == UINT32_MAX) {
                applyBlock<Fused, false>(source + done, destination + done, length,
                    bytes_.get() + header.offset + offset * 4, 0, dark);
            }
            else {
                const auto* factors = bytes_.get() + header.offset + offset * 3;
#if defined(_M_X64) && !defined(_M_ARM64EC)
                if constexpr (SkipDarkZeros) {
                    static_assert(Fused && std::is_same_v<Pixel, std::uint16_t> && std::is_same_v<Offset, std::uint8_t>);
                    // The full packed range must represent finite nonnegative floats.
                    constexpr auto maximumGainBits = std::bit_cast<std::uint32_t>((std::numeric_limits<float>::max)());
                    if (header.base <= maximumGainBits - 0xFFFFFFU) {
                        applySparseDarkBlock(source + done, destination + done, length, factors, header.base, dark);
                        done += length;
                        continue;
                    }
                }
#endif
                applyBlock<Fused, true>(source + done, destination + done, length, factors, header.base, dark);
            }
            done += length;
        }
    }

    template<bool Fused, typename Offset = std::uint8_t>
    void applyVertical(const ImageView& raw, const Offset* offsets = nullptr) const
    {
        auto* pixels = reinterpret_cast<std::uint16_t*>(raw.data);
        const auto rowSamples = static_cast<std::size_t>(raw.width) * raw.channels;
        parallelRows(raw.height / 2 + raw.height % 2, [&](std::uint32_t first, std::uint32_t end) {
            std::uint16_t topValues[blockSize];
            for (auto row = first; row < end; ++row) {
                const auto top = static_cast<std::size_t>(row) * rowSamples;
                const auto bottom = static_cast<std::size_t>(raw.height - 1 - row) * rowSamples;
                for (std::size_t column = 0; column < rowSamples; column += blockSize) {
                    const auto count = (std::min)(blockSize, rowSamples - column);
                    const auto* topOffsets = offsets;
                    const auto* bottomOffsets = offsets;
                    if constexpr (Fused) {
                        topOffsets += top + column;
                        bottomOffsets += bottom + column;
                    }
                    // Decode contiguous blocks once, preserving the top samples
                    // until the bottom samples have been corrected into their place.
                    apply<Fused>(pixels + top + column, topValues, count, top + column, 1, topOffsets);
                    if (top != bottom)
                        apply<Fused>(pixels + bottom + column, pixels + top + column, count, bottom + column, 1, bottomOffsets);
                    std::memcpy(pixels + bottom + column, topValues, count * sizeof(std::uint16_t));
                }
            }
        });
    }

private:
    static std::uint32_t packedBase(const float* values, std::size_t count, [[maybe_unused]] bool useSse41) noexcept
    {
        std::uint32_t low = UINT32_MAX, high = 0;
        std::size_t index = 0;
#if defined(_M_X64) && !defined(_M_ARM64EC)
        if (useSse41) {
            auto lows = _mm_set1_epi32(-1);
            auto highs = _mm_setzero_si128();
            for (; count - index >= 4; index += 4) {
                const auto bits = _mm_loadu_si128(reinterpret_cast<const __m128i*>(values + index));
                lows = _mm_min_epu32(lows, bits);
                highs = _mm_max_epu32(highs, bits);
            }
            lows = _mm_min_epu32(lows, _mm_shuffle_epi32(lows, _MM_SHUFFLE(1, 0, 3, 2)));
            highs = _mm_max_epu32(highs, _mm_shuffle_epi32(highs, _MM_SHUFFLE(1, 0, 3, 2)));
            low = static_cast<std::uint32_t>(_mm_cvtsi128_si32(
                _mm_min_epu32(lows, _mm_shuffle_epi32(lows, _MM_SHUFFLE(2, 3, 0, 1)))));
            high = static_cast<std::uint32_t>(_mm_cvtsi128_si32(
                _mm_max_epu32(highs, _mm_shuffle_epi32(highs, _MM_SHUFFLE(2, 3, 0, 1)))));
        }
#endif
        // The unsigned ordering includes every float bit pattern, even special gains.
        for (; index < count; ++index) {
            const auto bits = std::bit_cast<std::uint32_t>(values[index]);
            low = (std::min)(low, bits);
            high = (std::max)(high, bits);
        }
        return high - low <= 0xFFFFFFU && low != UINT32_MAX ? low : UINT32_MAX;
    }

    static void writeDeltas(const float* values, std::size_t count, std::uint32_t base,
        std::uint8_t* destination, [[maybe_unused]] bool useSsse3) noexcept
    {
        std::size_t index = 0;
#if defined(_M_X64) && !defined(_M_ARM64EC)
        if (useSsse3) {
            const auto bases = _mm_set1_epi32(std::bit_cast<std::int32_t>(base));
            const auto byteMask = _mm_setr_epi8(0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, -1, -1, -1, -1);
            const auto packFour = [&](const float* source) {
                const auto bits = _mm_loadu_si128(reinterpret_cast<const __m128i*>(source));
                return _mm_shuffle_epi8(_mm_sub_epi32(bits, bases), byteMask);
            };
            for (; count - index >= 16; index += 16) {
                const auto first = packFour(values + index);
                const auto second = packFour(values + index + 4);
                const auto third = packFour(values + index + 8);
                const auto fourth = packFour(values + index + 12);
                auto* output = destination + index * 3;
                // Emit exactly 48 bytes. Blocks can be written by different workers.
                _mm_storeu_si128(reinterpret_cast<__m128i*>(output), _mm_or_si128(first, _mm_slli_si128(second, 12)));
                _mm_storeu_si128(reinterpret_cast<__m128i*>(output + 16),
                    _mm_or_si128(_mm_srli_si128(second, 4), _mm_slli_si128(third, 8)));
                _mm_storeu_si128(reinterpret_cast<__m128i*>(output + 32),
                    _mm_or_si128(_mm_srli_si128(third, 8), _mm_slli_si128(fourth, 4)));
            }
        }
#endif
        for (; index < count; ++index) {
            const auto delta = std::bit_cast<std::uint32_t>(values[index]) - base;
            std::memcpy(destination + index * 3, &delta, 3);
        }
    }

    template<bool Fused, bool Packed, typename Pixel, typename Offset>
    static void applyBlock(const Pixel* source, Pixel* destination, std::size_t count,
        const std::uint8_t* factors, std::uint32_t base, const Offset* offsets)
    {
        constexpr auto maximum = (std::numeric_limits<Pixel>::max)();
        for (std::size_t index = 0; index < count; ++index) {
            std::uint32_t bits;
            std::memcpy(&bits, factors + index * (Packed ? 3 : 4), sizeof(bits));
            if constexpr (Packed) bits = (bits & 0xFFFFFFU) + base;
            auto value = source[index];
            if constexpr (Fused) value = value < offsets[index] ? Pixel{} : static_cast<Pixel>(value - offsets[index]);
            const float corrected = std::bit_cast<float>(bits) * value;
            destination[index] = corrected > static_cast<float>(maximum) ? maximum : static_cast<Pixel>(corrected);
        }
    }

#if defined(_M_X64) && !defined(_M_ARM64EC)
    static void applySparseDarkBlock(const std::uint16_t* source, std::uint16_t* destination, std::size_t count,
        const std::uint8_t* factors, std::uint32_t base, const std::uint8_t* offsets)
    {
        const auto zero = _mm_setzero_si128();
        std::size_t index = 0;
        for (; count - index >= 8; index += 8) {
            const auto dark = darkCorrectEight(source + index, offsets + index);
            // Select one bit per nonzero 16-bit lane.
            auto active = static_cast<unsigned>(~_mm_movemask_epi8(_mm_cmpeq_epi16(dark, zero))) & 0x5555U;
            auto* output = destination + index;
            if (active == 0) {
                _mm_storeu_si128(reinterpret_cast<__m128i*>(output), zero);
                continue;
            }
            const auto afterFirstLane = active & (active - 1);
            if ((afterFirstLane & (afterFirstLane - 1)) != 0) {
                // Dense groups use the ordinary loop before writing to aliased RAW.
                applyBlock<true, true>(source + index, output, 8, factors + index * 3, base, offsets + index);
                continue;
            }

            _mm_storeu_si128(reinterpret_cast<__m128i*>(output), zero);
            alignas(16) std::uint16_t darkSamples[8];
            _mm_store_si128(reinterpret_cast<__m128i*>(darkSamples), dark);
            for (; active != 0; active &= active - 1) {
                const auto lane = std::countr_zero(active) / 2;
                const auto sample = index + lane;
                applyBlock<false, true>(darkSamples + lane, destination + sample, 1,
                    factors + sample * 3, base, offsets + sample);
            }
        }
        applyBlock<true, true>(source + index, destination + index, count - index, factors + index * 3, base, offsets + index);
    }
#endif

    static constexpr std::size_t blockSize = 256;
    struct Block { std::uint32_t base, offset; };
    std::size_t count_ = 0;
    std::vector<Block> blocks_;
    std::unique_ptr<std::uint8_t[]> bytes_;
    std::size_t byteCount_ = 0;
};

template <typename TCorrection, typename TOperation, typename TPixel>
bool applyRows(
    const TPixel* sourcePixels,
    TPixel* destinationPixels,
    std::size_t pixelCount,
    std::size_t pixelBase,
    std::size_t pixelRowStride,
    std::uint32_t rows,
    std::size_t rowWidth,
    const std::vector<TCorrection>& correction,
    std::size_t correctionBase,
    std::size_t correctionRowStride,
    std::size_t correctionElementStride,
    const PackedUniformity* packed,
    std::string& error)
{
    if (!validateSpan<TPixel>(
            pixelCount, pixelBase, pixelRowStride, rows, rowWidth, 1, "RAW source buffer", error)
        || !validateSpan<TPixel>(
            pixelCount, pixelBase, pixelRowStride, rows, rowWidth, 1, "RAW destination buffer", error)
        || !validateSpan<TCorrection>(
            packed ? packed->size() : correction.size(), correctionBase, correctionRowStride, rows, rowWidth,
            correctionElementStride, "Calibration map", error)) {
        return false;
    }

    parallelRows(rows, [&](std::uint32_t startRow, std::uint32_t endRow) {
        for (auto row = startRow; row < endRow; ++row) {
            const auto* sourceRow = sourcePixels + pixelBase + static_cast<std::size_t>(row) * pixelRowStride;
            auto* destinationRow = destinationPixels + pixelBase + static_cast<std::size_t>(row) * pixelRowStride;
            const auto correctionIndex = correctionBase + static_cast<std::size_t>(row) * correctionRowStride;
            if (packed) packed->apply<false>(sourceRow, destinationRow, rowWidth, correctionIndex, correctionElementStride);
            else TOperation::apply(sourceRow, destinationRow, rowWidth, correction.data() + correctionIndex, correctionElementStride);
        }
    });
    return true;
}

struct MapHeader {
    int version = -1;
    std::uint32_t height = 0;
    std::uint32_t width = 0;
    std::uint32_t sourceBitsPerChannel = 0;
    std::uint32_t channels = 0;
};

// Each worker owns a pair of rows. Read both corrected values before swapping
// them, including the middle row of an odd-height image. No scratch RAW is needed.
template <typename TCorrect>
void applyVerticalMapFlip(const ImageView& raw, const TCorrect& correct)
{
    auto* pixels = reinterpret_cast<std::uint16_t*>(raw.data);
    const auto rowSamples = static_cast<std::size_t>(raw.width) * raw.channels;
    parallelRows(raw.height / 2 + raw.height % 2, [&](std::uint32_t first, std::uint32_t end) {
        for (auto row = first; row < end; ++row) {
            const auto top = static_cast<std::size_t>(row) * rowSamples;
            const auto bottom = static_cast<std::size_t>(raw.height - 1 - row) * rowSamples;
            for (std::size_t column = 0; column < rowSamples; ++column) {
                const auto topValue = correct(top + column);
                const auto bottomValue = correct(bottom + column);
                pixels[top + column] = bottomValue;
                pixels[bottom + column] = topValue;
            }
        }
    });
}

template <typename TCorrection>
bool loadMap(
    const std::filesystem::path& file,
    std::uint32_t requiredDataBits,
    MapHeader& header,
    std::vector<TCorrection>& values,
    std::string& error)
{
    BinaryFile stream;
    std::uint64_t length = 0;
    if (!openBinary(file, stream, length, error)) {
        return false;
    }
    if (length <= 16) {
        error = "Calibration map is too short";
        return false;
    }

    std::uint32_t height = 0;
    std::uint32_t width = 0;
    std::uint32_t dataBits = 0;
    std::uint32_t channels = 0;
    std::uint32_t sourceBits = 0;
    if (!readPod(stream, height, error) || !readPod(stream, width, error)
        || !readPod(stream, dataBits, error) || !readPod(stream, channels, error)
        || !readPod(stream, sourceBits, error)) {
        return false;
    }
    if (height == 0 || width == 0) {
        error = "Calibration map dimensions must be positive";
        return false;
    }

    std::uint64_t pixelCount = 0;
    std::uint64_t legacyBytes = 0;
    if (!checkedMultiply<std::uint64_t>(height, width, pixelCount)
        || !checkedMultiply<std::uint64_t>(pixelCount, sizeof(TCorrection), legacyBytes)) {
        error = "Calibration map dimensions overflow";
        return false;
    }

    std::uint64_t legacyLength = 0;
    if (!checkedAdd(legacyBytes, kLegacyMapHeaderBytes, legacyLength)) {
        error = "Calibration map size overflows";
        return false;
    }

    std::uint64_t currentValueCount = 0;
    std::uint64_t currentBytes = 0;
    std::uint64_t currentLength = 0;
    const bool currentSizeValid = checkedMultiply<std::uint64_t>(pixelCount, channels, currentValueCount)
        && checkedMultiply<std::uint64_t>(currentValueCount, sizeof(TCorrection), currentBytes)
        && checkedAdd(currentBytes, kCurrentMapHeaderBytes, currentLength);

    std::uint64_t dataOffset = 0;
    std::uint64_t valueCount = 0;
    if (length == legacyLength) {
        header.version = 0;
        header.height = height;
        header.width = width;
        header.channels = 1;
        header.sourceBitsPerChannel = 0;
        dataOffset = kLegacyMapHeaderBytes;
        valueCount = pixelCount;
    }
    else if (currentSizeValid && length == currentLength && dataBits == requiredDataBits) {
        if (channels == 0 || (sourceBits != 8 && sourceBits != 16)) {
            error = "Calibration map has invalid channel count or source bit depth";
            return false;
        }
        header.version = 1;
        header.height = height;
        header.width = width;
        header.channels = channels;
        header.sourceBitsPerChannel = sourceBits;
        dataOffset = kCurrentMapHeaderBytes;
        valueCount = currentValueCount;
    }
    else {
        error = "Calibration map length/header does not match V0 or V1 format";
        return false;
    }

    if (valueCount > static_cast<std::uint64_t>((std::numeric_limits<std::size_t>::max)())) {
        error = "Calibration map is too large for this process";
        return false;
    }
    values.resize(static_cast<std::size_t>(valueCount));

    std::uint64_t byteCount = 0;
    if (!checkedMultiply<std::uint64_t>(valueCount, sizeof(TCorrection), byteCount)) {
        error = "Calibration map byte count overflows";
        return false;
    }
    return seekAndRead(stream, dataOffset, values.data(), byteCount, error);
}

template <typename TCorrection, typename TOperation>
class MapCalibration final : public CalibrationItem {
public:
    MapCalibration(CalibrationType type, MapHeader header, std::vector<TCorrection> correction)
        : type_(type)
        , header_(header)
        , correction_(std::move(correction))
    {
        if constexpr (std::is_same_v<TOperation, UniformityOperation>) {
            bool validateGains = header_.sourceBitsPerChannel == 8 && header_.channels == 3;
#if defined(_M_X64) && !defined(_M_ARM64EC)
            validateGains = validateGains || (header_.sourceBitsPerChannel == 16 && header_.channels == 1);
#endif
            if (header_.version == 1 && validateGains) {
                // Legacy maps can contain unusual gains. Keep their original execution path.
                hasSafeGains_ = hasFiniteNonnegativeGains(correction_);
            }
            if (header_.version == 1 && header_.sourceBitsPerChannel == 16 && header_.channels == 3) {
                // Zero gains at the optical field's edges always produce zero,
                // including after DSNU. Find their bounds once, not per frame.
                const auto rowSamples = static_cast<std::size_t>(header_.width) * header_.channels;
                std::vector<NonzeroSpan> spans(header_.height);
                bool hasZeroEdges = false;
                for (std::uint32_t row = 0; row < header_.height; ++row) {
                    const auto* factors = correction_.data() + row * rowSamples;
                    std::size_t first = 0, end = rowSamples;
                    while (first < end && factors[first] == 0.0F) ++first;
                    while (end > first && factors[end - 1] == 0.0F) --end;
                    spans[row] = {first, end};
                    hasZeroEdges = hasZeroEdges || first != 0 || end != rowSamples;
                }
                if (hasZeroEdges) nonzeroSpans_ = std::move(spans);
                packedUniformity_ = PackedUniformity::create(correction_);
                if (packedUniformity_) std::vector<TCorrection>().swap(correction_);
            }
        }
    }

    [[nodiscard]] CalibrationType type() const noexcept override { return type_; }
    [[nodiscard]] bool supportsDistinctOutput() const noexcept override { return true; }
    [[nodiscard]] bool shareInstanceAcrossContexts() const noexcept override { return true; }
    [[nodiscard]] std::uint64_t cacheFootprintBytes() const noexcept override
    {
        return sizeof(*this)
            + static_cast<std::uint64_t>(correction_.capacity()) * sizeof(TCorrection)
            + static_cast<std::uint64_t>(nonzeroSpans_.capacity()) * sizeof(NonzeroSpan)
            + (packedUniformity_ ? packedUniformity_->footprintBytes() : 0);
    }

    bool tryApplyWithNext(
        const CalibrationItem& next,
        const ImageView& raw,
        const ExecutionOptions& options) const override;

    bool tryApplyWithNextTo(const CalibrationItem& next, const ImageView& raw,
        const ImageView& destination, const ExecutionOptions& options) const override;

    bool tryApplyWithFlip(const ImageView& raw, const ExecutionOptions& options) const override
    {
        if (options.rawOutputFlip != 0 || !matchesFullBgr16(raw, options)) return false;
        const auto* pixels = reinterpret_cast<const std::uint16_t*>(raw.data);
        if (packedUniformity_) {
            packedUniformity_->applyVertical<false>(raw);
            return true;
        }
        applyVerticalMapFlip(raw, [&](std::size_t index) {
            std::uint16_t corrected;
            TOperation::apply(pixels + index, &corrected, 1, correction_.data() + index, 1);
            return corrected;
        });
        return true;
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (options.interleavedBgr
            && header_.version == 1
            && header_.sourceBitsPerChannel != raw.bitsPerChannel) {
            error = type_ == CalibrationType::Dsnu
                ? "DSNU source bit depth does not match its map"
                : "Uniformity source bit depth does not match its map";
            return false;
        }
        if (raw.bitsPerChannel == 8) {
            auto* pixels = reinterpret_cast<std::uint8_t*>(raw.data);
            return applyTyped(pixels, pixels, raw, options, error);
        }
        auto* pixels = reinterpret_cast<std::uint16_t*>(raw.data);
        return applyTyped(pixels, pixels, raw, options, error);
    }

    bool applyOutOfPlace(
        const ImageView& source,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (source.data == nullptr || destination.data == nullptr
            || source.width != destination.width
            || source.height != destination.height
            || source.bitsPerChannel != destination.bitsPerChannel
            || source.channels != destination.channels) {
            error = "Map calibration source and destination layouts do not match";
            return false;
        }
        if (options.interleavedBgr
            && header_.version == 1
            && header_.sourceBitsPerChannel != source.bitsPerChannel) {
            error = type_ == CalibrationType::Dsnu
                ? "DSNU source bit depth does not match its map"
                : "Uniformity source bit depth does not match its map";
            return false;
        }

        std::size_t sampleCount = 0;
        std::size_t requiredBytes = 0;
        if (!checkedMultiply<std::size_t>(source.width, source.height, sampleCount)
            || !checkedMultiply<std::size_t>(sampleCount, source.channels, sampleCount)
            || !checkedMultiply<std::size_t>(sampleCount, source.bitsPerChannel / 8, requiredBytes)
            || source.dataLength < requiredBytes
            || destination.dataLength < requiredBytes) {
            error = "Map calibration source or destination buffer is too small";
            return false;
        }

        // Legacy ROI modes intentionally touch only a subset of the RAW image.
        // Preserve all untouched bytes before applying that subset. The normal
        // full-frame V1 path writes every sample directly and performs no copy.
        if (hasLegacyRoi(options, header_.width, header_.height)) {
            std::memcpy(destination.data, source.data, requiredBytes);
        }
        if (source.bitsPerChannel == 8) {
            return applyTyped(
                reinterpret_cast<const std::uint8_t*>(source.data),
                reinterpret_cast<std::uint8_t*>(destination.data), source, options, error);
        }
        return applyTyped(
            reinterpret_cast<const std::uint16_t*>(source.data),
            reinterpret_cast<std::uint16_t*>(destination.data), source, options, error);
    }

private:
    template <typename, typename> friend class MapCalibration;

    template<bool distinctOutput>
    void applyFusedMapRows16(const ImageView& raw,
        const MapCalibration<float, UniformityOperation>& uniformity, std::uint16_t* pixels) const;

    template<bool distinctOutput>
    bool tryApplyCroppedBgr16(
        const MapCalibration<float, UniformityOperation>& uniformity,
        const ImageView& raw, std::uint16_t* pixels,
        const ExecutionOptions& options) const;

    bool matchesFullBgr8(const ImageView& raw, const ExecutionOptions& options) const
    {
        const bool fullFrame = options.roi == std::array<std::uint32_t, 4>{}
            || options.roi == std::array<std::uint32_t, 4>{0, 0, raw.width, raw.height};
        return raw.bitsPerChannel == 8 && raw.channels == 3
            && options.interleavedBgr && options.rgbType == 0 && fullFrame
            && header_.version == 1 && header_.sourceBitsPerChannel == 8 && header_.channels == 3
            && header_.width == raw.width && header_.height == raw.height;
    }

    bool matchesFullMap16(const ImageView& raw, const ExecutionOptions& options) const
    {
        // A configured ROI covering this entire map uses the full-frame path.
        const bool fullFrame = options.roi == std::array<std::uint32_t, 4>{}
            || options.roi == std::array<std::uint32_t, 4>{0, 0, raw.width, raw.height};
        return raw.bitsPerChannel == 16 && (raw.channels == 1 || raw.channels == 3)
            && options.interleavedBgr && options.rgbType == 0 && fullFrame
            && header_.version == 1 && header_.sourceBitsPerChannel == 16
            && header_.channels == raw.channels
            && header_.width == raw.width && header_.height == raw.height;
    }

    bool matchesFullBgr16(const ImageView& raw, const ExecutionOptions& options) const
    {
        return raw.channels == 3 && matchesFullMap16(raw, options);
    }

    template <typename TPixel>
    bool applyTyped(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        const ImageView& raw,
        const ExecutionOptions& options,
        std::string& error)
    {
        std::size_t pixelCount = 0;
        std::size_t pixelsPerPlane = 0;
        if (!checkedMultiply<std::size_t>(raw.width, raw.height, pixelsPerPlane)
            || !checkedMultiply<std::size_t>(pixelsPerPlane, raw.channels, pixelCount)) {
            error = "RAW dimensions overflow";
            return false;
        }

        const bool roi = hasLegacyRoi(options, header_.width, header_.height);
        if (options.interleavedBgr) {
            if (header_.version == 0) {
                if (raw.channels != 1) {
                    error = "V0 scalar calibration maps require one-channel RAW data";
                    return false;
                }
                return applyScalar(sourcePixels, destinationPixels, pixelCount, raw, options, roi, error);
            }

            switch (options.rgbType) {
            case 0:
                if (raw.channels == 1
                    && (type_ == CalibrationType::Dsnu || header_.channels == 1)) {
                    return applyScalar(sourcePixels, destinationPixels, pixelCount, raw, options, roi, error);
                }
                return applyInterleaved(sourcePixels, destinationPixels, pixelCount, raw, options, roi, error);
            case 1:
            case 2:
            case 3:
                if (raw.channels != 1) {
                    error = "Single R/G/B calibration mode requires one-channel RAW data";
                    return false;
                }
                return applySingleColor(
                    sourcePixels, destinationPixels, pixelCount, raw, options, roi, options.rgbType, error);
            default:
                error = "RGB calibration type must be 0, 1, 2, or 3";
                return false;
            }
        }

        if (raw.channels != 3) {
            error = "Planar calibration requires three RAW planes";
            return false;
        }
        return applyPlanar(sourcePixels, destinationPixels, pixelCount, pixelsPerPlane, raw, options, roi, error);
    }

    template <typename TPixel>
    bool applyScalar(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t pixelCount,
        const ImageView& raw,
        const ExecutionOptions& options,
        bool roi,
        std::string& error)
    {
        if (!roi && (raw.width != header_.width || raw.height != header_.height)) {
            error = "RAW dimensions do not match the scalar calibration map";
            return false;
        }

        // DSNU V0/V1 used roi_w/roi_h here; Uniformity historically used the
        // actual input width/height. Preserve that difference for byte parity.
        const bool dsnu = type_ == CalibrationType::Dsnu;
        const auto rows = roi && dsnu ? options.roi[3] : raw.height;
        const auto rowWidth = static_cast<std::size_t>(roi && dsnu ? options.roi[2] : raw.width);
        const auto pixelRowStride = rowWidth;
        const auto correctionBase = roi
            ? static_cast<std::size_t>(options.roi[1]) * header_.width + options.roi[0]
            : 0;

        return applyRows<TCorrection, TOperation>(
            sourcePixels, destinationPixels, pixelCount, 0, pixelRowStride, rows, rowWidth, correction_,
            correctionBase, header_.width, 1, packedUniformity_.get(), error);
    }

    template <typename TPixel>
    bool applyInterleaved(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t pixelCount,
        const ImageView& raw,
        const ExecutionOptions& options,
        bool roi,
        std::string& error)
    {
        if (!roi && (raw.width != header_.width || raw.height != header_.height)) {
            error = "RAW dimensions do not match the calibration map";
            return false;
        }

        const auto rowWidth = static_cast<std::size_t>(raw.width) * raw.channels;
        const auto correctionRowStride = static_cast<std::size_t>(header_.width) * raw.channels;
        const auto correctionBase = roi
            ? static_cast<std::size_t>(options.roi[1]) * correctionRowStride
                + static_cast<std::size_t>(options.roi[0]) * raw.channels
            : 0;

        if constexpr (std::is_same_v<TOperation, UniformityOperation> && std::is_same_v<TPixel, std::uint16_t>) {
            if (matchesFullBgr16(raw, options) && !nonzeroSpans_.empty()) {
                parallelRows(raw.height, [&](std::uint32_t first, std::uint32_t end) {
                    for (auto row = first; row < end; ++row) {
                        const auto base = static_cast<std::size_t>(row) * rowWidth;
                        const auto span = nonzeroSpans_[row];
                        auto* destination = destinationPixels + base;
                        std::memset(destination, 0, span.first * sizeof(TPixel));
                        std::memset(destination + span.end, 0, (rowWidth - span.end) * sizeof(TPixel));
                        if (packedUniformity_) {
                            packedUniformity_->apply<false>(sourcePixels + base + span.first, destination + span.first,
                                span.end - span.first, base + span.first, 1);
                        }
                        else {
                            TOperation::apply(sourcePixels + base + span.first, destination + span.first,
                                span.end - span.first, correction_.data() + base + span.first, 1);
                        }
                    }
                });
                return true;
            }
        }

        return applyRows<TCorrection, TOperation>(
            sourcePixels, destinationPixels, pixelCount, 0, rowWidth, raw.height, rowWidth, correction_,
            correctionBase, correctionRowStride, 1, packedUniformity_.get(), error);
    }

    template <typename TPixel>
    bool applySingleColor(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t pixelCount,
        const ImageView& raw,
        const ExecutionOptions& options,
        bool roi,
        std::int32_t rgbType,
        std::string& error)
    {
        if (!roi && (raw.width != header_.width || raw.height != header_.height)) {
            error = "RAW dimensions do not match the calibration map";
            return false;
        }

        const auto rows = roi ? options.roi[3] : raw.height;
        const auto rowWidth = static_cast<std::size_t>(roi ? options.roi[2] : raw.width);
        const auto correctionRowStride = static_cast<std::size_t>(header_.width) * 3;
        const auto channelOffset = static_cast<std::size_t>(3 - rgbType); // map is B, G, R
        const auto correctionBase = (roi
            ? static_cast<std::size_t>(options.roi[1]) * header_.width + options.roi[0]
            : 0) * 3 + channelOffset;

        return applyRows<TCorrection, TOperation>(
            sourcePixels, destinationPixels, pixelCount, 0, rowWidth, rows, rowWidth, correction_,
            correctionBase, correctionRowStride, 3, packedUniformity_.get(), error);
    }

    template <typename TPixel>
    bool applyPlanar(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t pixelCount,
        std::size_t pixelsPerPlane,
        const ImageView& raw,
        const ExecutionOptions& options,
        bool roi,
        std::string& error)
    {
        if (!roi && (raw.width != header_.width || raw.height != header_.height)) {
            error = "RAW dimensions do not match the calibration map";
            return false;
        }

        const auto rows = roi ? options.roi[3] : raw.height;
        const auto rowWidth = static_cast<std::size_t>(roi ? options.roi[2] : raw.width);
        const auto correctionRowStride = static_cast<std::size_t>(header_.width) * 3;
        const auto correctionPixelBase = (roi
            ? static_cast<std::size_t>(options.roi[1]) * header_.width + options.roi[0]
            : 0) * 3;

        // Legacy planar memory is R, G, B while the calibration map is B, G, R.
        for (std::size_t plane = 0; plane < 3; ++plane) {
            const auto correctionChannel = 2 - plane;
            if (!applyRows<TCorrection, TOperation>(
                    sourcePixels, destinationPixels, pixelCount, plane * pixelsPerPlane, rowWidth, rows, rowWidth,
                    correction_, correctionPixelBase + correctionChannel,
                    correctionRowStride, 3, packedUniformity_.get(), error)) {
                return false;
            }
        }
        return true;
    }

    CalibrationType type_;
    MapHeader header_;
    std::vector<TCorrection> correction_;
    struct NonzeroSpan { std::size_t first, end; };
    std::vector<NonzeroSpan> nonzeroSpans_;
    std::unique_ptr<PackedUniformity> packedUniformity_;
    bool hasSafeGains_ = false;
};

template <typename TCorrection>
void applyFusedBgr8(
    std::uint8_t* pixels,
    const TCorrection* __restrict offsets,
    const float* __restrict factors,
    std::size_t count)
{
    static_assert(std::is_same_v<TCorrection, std::uint8_t> || std::is_same_v<TCorrection, std::uint16_t>);
    std::size_t index = 0;
#if defined(_M_X64) && !defined(_M_ARM64EC)
    const auto zero = _mm_setzero_si128();
    const auto maximum = _mm_set1_ps(255.0F);
    const auto applyGains = [maximum](const __m128i samples, const float* gains) {
        const auto scaled = _mm_mul_ps(_mm_cvtepi32_ps(samples), _mm_loadu_ps(gains));
        // Clamp before truncation, including when a finite gain overflows to +infinity.
        return _mm_cvttps_epi32(_mm_min_ps(scaled, maximum));
    };
    const auto correctEight = [&](const __m128i samples, const float* gains) {
        return _mm_packs_epi32(
            applyGains(_mm_unpacklo_epi16(samples, zero), gains),
            applyGains(_mm_unpackhi_epi16(samples, zero), gains + 4));
    };
    for (; count - index >= 16; index += 16) {
        const auto raw = _mm_loadu_si128(reinterpret_cast<const __m128i*>(pixels + index));
        __m128i low;
        __m128i high;
        if constexpr (std::is_same_v<TCorrection, std::uint8_t>) {
            const auto dark = _mm_subs_epu8(raw,
                _mm_loadu_si128(reinterpret_cast<const __m128i*>(offsets + index)));
            low = _mm_unpacklo_epi8(dark, zero);
            high = _mm_unpackhi_epi8(dark, zero);
        }
        else {
            low = _mm_subs_epu16(_mm_unpacklo_epi8(raw, zero),
                _mm_loadu_si128(reinterpret_cast<const __m128i*>(offsets + index)));
            high = _mm_subs_epu16(_mm_unpackhi_epi8(raw, zero),
                _mm_loadu_si128(reinterpret_cast<const __m128i*>(offsets + index + 8)));
        }
        const auto corrected = _mm_packus_epi16(
            correctEight(low, factors + index), correctEight(high, factors + index + 8));
        _mm_storeu_si128(reinterpret_cast<__m128i*>(pixels + index), corrected);
    }
#endif
    for (; index < count; ++index) {
        const int darkCorrected = (std::max)(0, static_cast<int>(pixels[index]) - offsets[index]);
        const float corrected = factors[index] * static_cast<float>(darkCorrected);
        pixels[index] = static_cast<std::uint8_t>((std::min)(255.0F, corrected));
    }
}

#if defined(_M_X64) && !defined(_M_ARM64EC)
template <typename TOffset>
void applyFusedMono16(const ImageView& raw, const TOffset* offsets, const float* gains, bool vertical)
{
    auto* pixels = reinterpret_cast<std::uint16_t*>(raw.data);
    const auto width = static_cast<std::size_t>(raw.width);
    const auto zero = _mm_setzero_si128();
    const auto maximum = _mm_set1_ps(65535.0F);
    const auto bias = _mm_set1_epi32(32768);
    const auto sign = _mm_set1_epi16(-32768);
    const auto scale = [&](__m128i samples, const float* factors) {
        const auto scaled = _mm_mul_ps(_mm_cvtepi32_ps(samples), _mm_loadu_ps(factors));
        // Bias into the signed range for the SSE2 pack, then restore the unsigned bits.
        return _mm_sub_epi32(_mm_cvttps_epi32(_mm_min_ps(scaled, maximum)), bias);
    };
    const auto correctEight = [&](std::size_t index) {
        __m128i dark;
        if constexpr (std::is_same_v<TOffset, std::uint8_t>) {
            dark = darkCorrectEight(pixels + index, offsets + index);
        }
        else {
            dark = _mm_subs_epu16(_mm_loadu_si128(reinterpret_cast<const __m128i*>(pixels + index)),
                _mm_loadu_si128(reinterpret_cast<const __m128i*>(offsets + index)));
        }
        const auto low = scale(_mm_unpacklo_epi16(dark, zero), gains + index);
        const auto high = scale(_mm_unpackhi_epi16(dark, zero), gains + index + 4);
        return _mm_xor_si128(_mm_packs_epi32(low, high), sign);
    };
    const auto correctOne = [&](std::size_t index) {
        const int dark = (std::max)(0, static_cast<int>(pixels[index]) - offsets[index]);
        return static_cast<std::uint16_t>((std::min)(65535.0F, gains[index] * dark));
    };
    const auto correctRun = [&](std::size_t index, std::size_t end) {
        for (; end - index >= 8; index += 8)
            _mm_storeu_si128(reinterpret_cast<__m128i*>(pixels + index), correctEight(index));
        for (; index < end; ++index) pixels[index] = correctOne(index);
    };

    if (!vertical) {
        parallelRows(raw.height, [&](std::uint32_t first, std::uint32_t end) {
            correctRun(first * width, end * width);
        });
        return;
    }

    parallelRows(raw.height / 2 + raw.height % 2, [&](std::uint32_t first, std::uint32_t end) {
        for (auto row = first; row < end; ++row) {
            const auto top = row * width;
            const auto bottom = (raw.height - 1 - row) * width;
            if (top == bottom) {
                correctRun(top, top + width);
                continue;
            }
            // Read both original row segments before writing either result.
            std::size_t column = 0;
            for (; width - column >= 8; column += 8) {
                const auto topValue = correctEight(top + column);
                const auto bottomValue = correctEight(bottom + column);
                _mm_storeu_si128(reinterpret_cast<__m128i*>(pixels + top + column), bottomValue);
                _mm_storeu_si128(reinterpret_cast<__m128i*>(pixels + bottom + column), topValue);
            }
            for (; column < width; ++column) {
                const auto topValue = correctOne(top + column);
                pixels[top + column] = correctOne(bottom + column);
                pixels[bottom + column] = topValue;
            }
        }
    });
}
#endif

template <typename TCorrection, typename TOperation>
template<bool distinctOutput>
bool MapCalibration<TCorrection, TOperation>::tryApplyCroppedBgr16(
    const MapCalibration<float, UniformityOperation>& uniformity,
    const ImageView& raw, std::uint16_t* pixels,
    const ExecutionOptions& options) const
{
    if (raw.bitsPerChannel != 16 || raw.channels != 3 || !options.interleavedBgr
        || options.rgbType != 0 || options.rawOutputFlip != -99
        || raw.width != options.roi[2] || raw.height != options.roi[3]
        || header_.version != 1 || header_.sourceBitsPerChannel != 16 || header_.channels != 3
        || uniformity.header_.version != 1 || uniformity.header_.sourceBitsPerChannel != 16
        || uniformity.header_.channels != 3
        || header_.width != uniformity.header_.width || header_.height != uniformity.header_.height
        || !hasLegacyRoi(options, header_.width, header_.height)) {
        return false;
    }

    const auto rawRowSamples = static_cast<std::size_t>(raw.width) * raw.channels;
    const auto mapRowSamples = static_cast<std::size_t>(header_.width) * raw.channels;
    const auto cropStart = static_cast<std::size_t>(options.roi[0]) * raw.channels;
    const auto cropEnd = cropStart + rawRowSamples;
    const auto* input = distinctOutput ? reinterpret_cast<const std::uint16_t*>(raw.data) : pixels;
    const auto* offsets = correction_.data();
    const auto* factors = uniformity.correction_.data();
    const auto* packed = uniformity.packedUniformity_.get();
    const auto& spans = uniformity.nonzeroSpans_;
#if defined(_M_X64) && !defined(_M_ARM64EC)
    bool skipDarkZeros = false;
    if constexpr (std::is_same_v<TCorrection, std::uint8_t>) {
        if (packed) {
            const auto firstMapSample = static_cast<std::size_t>(options.roi[1]) * mapRowSamples + cropStart;
            skipDarkZeros = hasSparseCroppedDarkCorrection(input, offsets + firstMapSample,
                rawRowSamples, mapRowSamples, raw.height);
        }
    }
#endif

    parallelRows(raw.height, [&](std::uint32_t first, std::uint32_t end) {
        for (auto row = first; row < end; ++row) {
            const auto mapRow = static_cast<std::size_t>(options.roi[1]) + row;
            const auto mapBase = mapRow * mapRowSamples + cropStart;
            const auto rawBase = row * rawRowSamples;
            const auto* rowInput = input + rawBase;
            auto* rowPixels = pixels + rawBase;
            std::size_t start = 0, limit = rawRowSamples;
            if (!spans.empty()) {
                // Intersect the full map's nonzero span with the crop in sample coordinates.
                const auto span = spans[mapRow];
                start = std::clamp(span.first, cropStart, cropEnd) - cropStart;
                limit = std::clamp(span.end, cropStart, cropEnd) - cropStart;
                std::memset(rowPixels, 0, start * sizeof(std::uint16_t));
                std::memset(rowPixels + limit, 0, (rawRowSamples - limit) * sizeof(std::uint16_t));
            }
            if (start == limit) continue;
            if (packed) {
#if defined(_M_X64) && !defined(_M_ARM64EC)
                if constexpr (std::is_same_v<TCorrection, std::uint8_t>) {
                    if (skipDarkZeros) {
                        packed->apply<true, true>(rowInput + start, rowPixels + start, limit - start,
                            mapBase + start, 1, offsets + mapBase + start);
                        continue;
                    }
                }
#endif
                packed->apply<true>(rowInput + start, rowPixels + start, limit - start,
                    mapBase + start, 1, offsets + mapBase + start);
                continue;
            }
            const auto* rowOffsets = offsets + mapBase;
            const auto* rowFactors = factors + mapBase;
            for (auto column = start; column < limit; ++column) {
                const auto pixel = rowInput[column];
                const auto offset = rowOffsets[column];
                const auto darkCorrected = pixel < offset ? std::uint16_t{0} : static_cast<std::uint16_t>(pixel - offset);
                const float corrected = rowFactors[column] * darkCorrected;
                rowPixels[column] = corrected > 65535.0F
                    ? std::uint16_t{65535} : static_cast<std::uint16_t>(corrected);
            }
        }
    });
    return true;
}

template <typename TCorrection, typename TOperation>
bool MapCalibration<TCorrection, TOperation>::tryApplyWithNext(
    const CalibrationItem& next,
    const ImageView& raw,
    const ExecutionOptions& options) const
{
    if constexpr (!std::is_same_v<TOperation, DsnuOperation>) {
        return false;
    }
    else {
        const auto* uniformity = dynamic_cast<const MapCalibration<float, UniformityOperation>*>(&next);
        if (raw.bitsPerChannel == 8) {
            if (uniformity == nullptr || options.rawOutputFlip != -99
                || !matchesFullBgr8(raw, options) || !uniformity->matchesFullBgr8(raw, options)
                || !uniformity->hasSafeGains_) {
                return false;
            }

            const auto rowSamples = static_cast<std::size_t>(raw.width) * raw.channels;
            auto* pixels = reinterpret_cast<std::uint8_t*>(raw.data);
            const auto* __restrict offsets = correction_.data();
            const auto* __restrict factors = uniformity->correction_.data();
            parallelRows(raw.height, [&](std::uint32_t first, std::uint32_t end) {
                const auto start = first * rowSamples;
                applyFusedBgr8(pixels + start, offsets + start, factors + start, (end - first) * rowSamples);
            });
            return true;
        }

        if (uniformity == nullptr) return false;
        if (!matchesFullMap16(raw, options) || !uniformity->matchesFullMap16(raw, options)) {
            return tryApplyCroppedBgr16<false>(*uniformity, raw,
                reinterpret_cast<std::uint16_t*>(raw.data), options);
        }
        if (options.rawOutputFlip != -99 && options.rawOutputFlip != 0) return false;
        if (raw.channels == 1) {
#if defined(_M_X64) && !defined(_M_ARM64EC)
            if (uniformity->hasSafeGains_) {
                applyFusedMono16(raw, correction_.data(), uniformity->correction_.data(), options.rawOutputFlip == 0);
                return true;
            }
#endif
            // The fused scalar loop saves memory traffic on large frames, but
            // the independent passes vectorize better with fewer workers.
            constexpr std::uint64_t minimumMonoPixels = 5'000'000;
            constexpr int minimumMonoThreads = 8;
            const auto rowCount = options.rawOutputFlip == 0 ? raw.height / 2 + raw.height % 2 : raw.height;
            if (static_cast<std::uint64_t>(raw.width) * raw.height < minimumMonoPixels
                || rowCount < kMinimumParallelRows || cv::getNumThreads() < minimumMonoThreads) {
                return false;
            }
        }

        auto* pixels = reinterpret_cast<std::uint16_t*>(raw.data);
        if (options.rawOutputFlip == 0) {
            const auto* offsets = correction_.data();
            const auto* factors = uniformity->correction_.data();
            const auto* packed = uniformity->packedUniformity_.get();
            if (packed) {
                packed->applyVertical<true>(raw, offsets);
                return true;
            }
            applyVerticalMapFlip(raw, [&](std::size_t index) {
                const auto source = pixels[index];
                const auto offset = offsets[index];
                const std::uint16_t darkCorrected = source < offset
                    ? 0 : static_cast<std::uint16_t>(source - offset);
                const float corrected = factors[index] * darkCorrected;
                return corrected > 65535.0F ? std::uint16_t{65535} : static_cast<std::uint16_t>(corrected);
            });
            return true;
        }
        applyFusedMapRows16<false>(raw, *uniformity, pixels);
        return true;
    }
}

template <typename TCorrection, typename TOperation>
bool MapCalibration<TCorrection, TOperation>::tryApplyWithNextTo(
    const CalibrationItem& next, const ImageView& raw, const ImageView& destination,
    const ExecutionOptions& options) const
{
    if constexpr (!std::is_same_v<TOperation, DsnuOperation>) {
        return false;
    }
    else {
        const auto* uniformity = dynamic_cast<const MapCalibration<float, UniformityOperation>*>(&next);
        if (uniformity == nullptr || options.rawOutputFlip != -99
            || reinterpret_cast<std::uintptr_t>(raw.data) % alignof(std::uint16_t) != 0) {
            return false;
        }

        auto* pixels = reinterpret_cast<std::uint16_t*>(destination.data);
        if (!matchesFullBgr16(raw, options) || !uniformity->matchesFullBgr16(raw, options)) {
            return tryApplyCroppedBgr16<true>(*uniformity, raw, pixels, options);
        }
        applyFusedMapRows16<true>(raw, *uniformity, pixels);
        return true;
    }
}

template <typename TCorrection, typename TOperation>
template<bool distinctOutput>
void MapCalibration<TCorrection, TOperation>::applyFusedMapRows16(
    const ImageView& raw, const MapCalibration<float, UniformityOperation>& uniformity,
    std::uint16_t* pixels) const
{
    const auto* input = distinctOutput ? reinterpret_cast<const std::uint16_t*>(raw.data) : pixels;
    const std::size_t rowSamples = static_cast<std::size_t>(raw.width) * raw.channels;
    const auto* offsets = correction_.data();
    const auto* factors = uniformity.correction_.data();
    const auto* packed = uniformity.packedUniformity_.get();
#if defined(_M_X64) && !defined(_M_ARM64EC)
    bool skipDarkZeros = false;
    if constexpr (std::is_same_v<TCorrection, std::uint8_t>) {
        skipDarkZeros = packed && hasSparseDarkCorrection(input, offsets, raw.height * rowSamples);
    }
#endif
    // Keep the zero clamp, float multiply, saturation, and truncation in order.
    parallelRows(raw.height, [&](std::uint32_t first, std::uint32_t end) {
        for (auto row = first; row < end; ++row) {
            const auto base = static_cast<std::size_t>(row) * rowSamples;
            const auto span = uniformity.nonzeroSpans_.empty()
                ? MapCalibration<float, UniformityOperation>::NonzeroSpan{0, rowSamples}
                : uniformity.nonzeroSpans_[row];
            std::memset(pixels + base, 0, span.first * sizeof(std::uint16_t));
            std::memset(pixels + base + span.end, 0, (rowSamples - span.end) * sizeof(std::uint16_t));
            if (packed) {
#if defined(_M_X64) && !defined(_M_ARM64EC)
                if constexpr (std::is_same_v<TCorrection, std::uint8_t>) {
                    if (skipDarkZeros) {
                        packed->apply<true, true>(input + base + span.first, pixels + base + span.first,
                            span.end - span.first, base + span.first, 1, offsets + base + span.first);
                        continue;
                    }
                }
#endif
                packed->apply<true>(input + base + span.first, pixels + base + span.first,
                    span.end - span.first, base + span.first, 1, offsets + base + span.first);
                continue;
            }
            for (std::size_t column = span.first; column < span.end; ++column) {
                const auto index = base + column;
                const auto source = input[index];
                const auto offset = offsets[index];
                const std::uint16_t darkCorrected = source < offset
                    ? 0 : static_cast<std::uint16_t>(source - offset);
                const float corrected = factors[index] * darkCorrected;
                pixels[index] = corrected > 65535.0F
                    ? 65535 : static_cast<std::uint16_t>(corrected);
            }
        }
    });
}

class DarkNoiseCalibration final : public CalibrationItem {
public:
    explicit DarkNoiseCalibration(double ratio)
        : ratio_(static_cast<float>(ratio))
    {
    }

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::DarkNoise;
    }

    [[nodiscard]] bool shareInstanceAcrossContexts() const noexcept override { return true; }
    [[nodiscard]] bool supportsDistinctOutput() const noexcept override { return true; }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string&) override
    {
        // This intentionally retains cvCamera's production behavior: the loop
        // is indexed only by height (not width/channels), and both 8/16-bit
        // paths clamp to 255. Changing it would break byte-for-byte parity.
        const auto start = (std::min)(options.roi[2], raw.height);
        const auto bottom = (std::min)(options.roi[3], raw.height);
        const auto end = raw.height - bottom;
        if (start >= end) {
            return true;
        }

        if (raw.bitsPerChannel == 8) {
            auto* data = reinterpret_cast<std::uint8_t*>(raw.data);
            for (auto index = start; index < end; ++index) {
                const float corrected = data[index] * ratio_;
                data[index] = corrected > 255.0F
                    ? std::uint8_t{ 255 }
                    : static_cast<std::uint8_t>(corrected);
            }
        }
        else {
            auto* data = reinterpret_cast<std::uint16_t*>(raw.data);
            for (auto index = start; index < end; ++index) {
                const float corrected = data[index] * ratio_;
                data[index] = corrected > 255.0F
                    ? std::uint16_t{ 255 }
                    : static_cast<std::uint16_t>(corrected);
            }
        }
        return true;
    }

    bool applyOutOfPlace(
        const ImageView& source,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        std::size_t sampleCount = 0;
        std::size_t byteCount = 0;
        if (!validDistinctRawOutput(
                source, destination, sampleCount, byteCount, error)) {
            return false;
        }
        if (sampleCount < source.height) {
            error = "DarkNoise RAW layout is invalid";
            return false;
        }

        if (source.bitsPerChannel == 8) {
            correctTo(
                reinterpret_cast<const std::uint8_t*>(source.data),
                reinterpret_cast<std::uint8_t*>(destination.data),
                sampleCount,
                source.height,
                options);
        }
        else {
            correctTo(
                reinterpret_cast<const std::uint16_t*>(source.data),
                reinterpret_cast<std::uint16_t*>(destination.data),
                sampleCount,
                source.height,
                options);
        }
        return true;
    }

private:
    template <typename TPixel>
    void correctTo(
        const TPixel* source,
        TPixel* destination,
        std::size_t sampleCount,
        std::uint32_t height,
        const ExecutionOptions& options) const
    {
        const auto start = (std::min)(options.roi[2], height);
        const auto bottom = (std::min)(options.roi[3], height);
        const auto end = height - bottom;

        // DarkNoise's historical loop changes only a tiny height-indexed
        // prefix. The rest still has to be materialized for the next stage,
        // but copying the two untouched spans and producing the corrected
        // span directly avoids a separate copy-then-mutate pass.
        std::memcpy(destination, source, static_cast<std::size_t>(start) * sizeof(TPixel));
        for (auto index = start; index < end; ++index) {
            const float corrected = source[index] * ratio_;
            destination[index] = corrected > 255.0F
                ? static_cast<TPixel>(255)
                : static_cast<TPixel>(corrected);
        }
        const auto suffix = static_cast<std::size_t>((std::max)(start, end));
        std::memcpy(
            destination + suffix,
            source + suffix,
            (sampleCount - suffix) * sizeof(TPixel));
    }

    float ratio_ = 0.0F;
};

struct DefectPoint {
    std::uint32_t row = 0;
    std::uint32_t column = 0;
};

class DefectPointCalibration final : public CalibrationItem {
public:
    DefectPointCalibration(CalibrationType type, std::vector<DefectPoint> points)
        : type_(type)
        , points_(std::move(points))
    {
    }

    [[nodiscard]] CalibrationType type() const noexcept override { return type_; }

    [[nodiscard]] bool shareInstanceAcrossContexts() const noexcept override { return true; }
    [[nodiscard]] bool supportsDistinctOutput() const noexcept override { return true; }

    [[nodiscard]] std::uint64_t cacheFootprintBytes() const noexcept override
    {
        return sizeof(*this)
            + static_cast<std::uint64_t>(points_.capacity()) * sizeof(DefectPoint);
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string&) override
    {
        if (raw.bitsPerChannel == 8) {
            correct(reinterpret_cast<std::uint8_t*>(raw.data), raw, options);
        }
        else {
            correct(reinterpret_cast<std::uint16_t*>(raw.data), raw, options);
        }
        return true;
    }

    bool applyOutOfPlace(
        const ImageView& source,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        std::size_t sampleCount = 0;
        std::size_t byteCount = 0;
        if (!validDistinctRawOutput(
                source, destination, sampleCount, byteCount, error)) {
            return false;
        }

        // Defect points are sparse, while their neighborhoods may overlap and
        // therefore intentionally observe earlier corrections. A complete
        // source copy is required before running the unchanged in-place logic.
        std::memcpy(destination.data, source.data, byteCount);
        return apply(destination, nullptr, options, error);
    }

private:
    template <typename TPixel>
    void correct(TPixel* data, const ImageView& raw, const ExecutionOptions& options)
    {
        std::vector<TPixel> neighborhood;
        neighborhood.reserve(49);

        for (const auto& point : points_) {
            // Unlike the legacy object, use a local coordinate. cvCamera
            // subtracted ROI offsets from its stored point on every call,
            // causing repeated non-zero-ROI executions to drift.
            const auto row = static_cast<std::int64_t>(point.row) - options.roi[1];
            const auto column = static_cast<std::int64_t>(point.column) - options.roi[0];
            if (row < 0 || column < 0
                || row >= raw.height || column >= raw.width) {
                continue;
            }

            const auto y1 = static_cast<std::uint32_t>((std::max<std::int64_t>)(0, row - 3));
            const auto x1 = static_cast<std::uint32_t>((std::max<std::int64_t>)(0, column - 3));
            const auto y2 = static_cast<std::uint32_t>((std::min<std::int64_t>)(raw.height - 1, row + 3));
            const auto x2 = static_cast<std::uint32_t>((std::min<std::int64_t>)(raw.width - 1, column + 3));

            const auto destinationPixel =
                (static_cast<std::size_t>(row) * raw.width + static_cast<std::size_t>(column))
                * raw.channels;
            for (std::uint32_t channel = 0; channel < raw.channels; ++channel) {
                neighborhood.clear();
                for (auto y = y1; y <= y2; ++y) {
                    for (auto x = x1; x <= x2; ++x) {
                        const auto index = (static_cast<std::size_t>(y) * raw.width + x)
                            * raw.channels + channel;
                        neighborhood.push_back(data[index]);
                    }
                }
                std::sort(neighborhood.begin(), neighborhood.end());
                const auto middle = neighborhood.size() / 2;
                data[destinationPixel + channel] = neighborhood.size() % 2 == 0
                    ? static_cast<TPixel>((static_cast<unsigned int>(neighborhood[middle - 1])
                        + neighborhood[middle]) / 2)
                    : neighborhood[middle];
            }
        }
    }

    CalibrationType type_;
    std::vector<DefectPoint> points_;
};

class LineArityCalibration final : public CalibrationItem {
public:
    explicit LineArityCalibration(std::vector<float> factors)
        : factors_(std::move(factors))
    {
    }

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::LineArity;
    }

    [[nodiscard]] bool shareInstanceAcrossContexts() const noexcept override { return true; }
    [[nodiscard]] bool supportsDistinctOutput() const noexcept override { return true; }

    [[nodiscard]] std::uint64_t cacheFootprintBytes() const noexcept override
    {
        return sizeof(*this)
            + static_cast<std::uint64_t>(factors_.capacity()) * sizeof(float);
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions&,
        std::string& error) override
    {
        if (raw.bitsPerChannel != 16) {
            error = "LineArity supports only 16-bit RAW data";
            return false;
        }

        std::size_t count = 0;
        if (!checkedMultiply<std::size_t>(raw.width, raw.height, count)) {
            error = "LineArity RAW dimensions overflow";
            return false;
        }
        if (factors_.size() < count) {
            error = "LineArity table is smaller than the RAW image";
            return false;
        }

        // cvCamera intentionally processed width*height values and ignored
        // channels. Keep that first-plane behavior, but parallelize by row.
        auto* data = reinterpret_cast<std::uint16_t*>(raw.data);
        parallelRows(raw.height, [&](std::uint32_t startRow, std::uint32_t endRow) {
            for (auto row = startRow; row < endRow; ++row) {
                const auto base = static_cast<std::size_t>(row) * raw.width;
                for (std::uint32_t column = 0; column < raw.width; ++column) {
                    const auto index = base + column;
                    data[index] = static_cast<std::uint16_t>(data[index] * factors_[index]);
                }
            }
        });
        return true;
    }

    bool applyOutOfPlace(
        const ImageView& source,
        const ImageView& destination,
        const ExecutionOptions&,
        std::string& error) override
    {
        if (source.bitsPerChannel != 16) {
            error = "LineArity supports only 16-bit RAW data";
            return false;
        }

        std::size_t sampleCount = 0;
        std::size_t byteCount = 0;
        if (!validDistinctRawOutput(
                source, destination, sampleCount, byteCount, error)) {
            return false;
        }

        std::size_t count = 0;
        if (!checkedMultiply<std::size_t>(source.width, source.height, count)) {
            error = "LineArity RAW dimensions overflow";
            return false;
        }
        if (factors_.size() < count || sampleCount < count) {
            error = "LineArity table is smaller than the RAW image";
            return false;
        }

        const auto* sourceData = reinterpret_cast<const std::uint16_t*>(source.data);
        auto* destinationData = reinterpret_cast<std::uint16_t*>(destination.data);
        parallelRows(source.height, [&](std::uint32_t startRow, std::uint32_t endRow) {
            for (auto row = startRow; row < endRow; ++row) {
                const auto base = static_cast<std::size_t>(row) * source.width;
                for (std::uint32_t column = 0; column < source.width; ++column) {
                    const auto index = base + column;
                    destinationData[index] = static_cast<std::uint16_t>(
                        sourceData[index] * factors_[index]);
                }
            }
        });

        // The legacy algorithm changes only width*height values even for a
        // multi-channel buffer. Preserve the untouched tail without first
        // copying values that the correction overwrites.
        std::memcpy(
            destinationData + count,
            sourceData + count,
            (sampleCount - count) * sizeof(std::uint16_t));
        return true;
    }

private:
    std::vector<float> factors_;
};

std::unique_ptr<CalibrationItem> loadDarkNoise(
    const std::filesystem::path& file,
    std::string& error)
{
    std::string json;
    if (!readTextFile(file, json, error)) {
        return nullptr;
    }
    if (!looksLikeJsonObject(json)) {
        error = "DarkNoise calibration is not a JSON object";
        return nullptr;
    }

    double ratio = 0.0;
    double exposure = 0.0;
    bool exposureIsReal = false;
    bool ratioIsReal = false;
    // JsonCpp's old loader assigned DarkNoiseRatio only when Texp_x was a
    // JSON real. Missing/integer Texp_x therefore leaves the zero default.
    if (findJsonNumber(json, "Texp_x", exposure, exposureIsReal) && exposureIsReal) {
        findJsonNumber(json, "DarkNoiseRatio", ratio, ratioIsReal);
    }
    (void)ratioIsReal;
    return std::make_unique<DarkNoiseCalibration>(ratio);
}

std::unique_ptr<CalibrationItem> loadDefectPoints(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error)
{
    BinaryFile stream;
    std::uint64_t length = 0;
    if (!openBinary(file, stream, length, error)) {
        return nullptr;
    }
    if (length < sizeof(std::uint32_t)) {
        error = "Defect-point file is too short";
        return nullptr;
    }

    std::uint32_t count = 0;
    if (!readPod(stream, count, error)) {
        return nullptr;
    }
    std::uint64_t pointBytes = 0;
    std::uint64_t requiredLength = 0;
    if (!checkedMultiply<std::uint64_t>(count, sizeof(std::uint32_t) * 2, pointBytes)
        || !checkedAdd(sizeof(std::uint32_t), pointBytes, requiredLength)
        || requiredLength > length) {
        error = "Defect-point count exceeds the calibration file length";
        return nullptr;
    }

    std::vector<DefectPoint> points;
    points.reserve(count);
    for (std::uint32_t index = 0; index < count; ++index) {
        DefectPoint point;
        if (!readPod(stream, point.row, error) || !readPod(stream, point.column, error)) {
            return nullptr;
        }
        points.push_back(point);
    }
    // The file loader historically tolerated trailing bytes; retain that V0
    // compatibility rather than requiring exact length equality.
    return std::make_unique<DefectPointCalibration>(type, std::move(points));
}

bool offsetsFitByte(const std::vector<std::uint16_t>& values)
{
    std::size_t index = 0;
#if defined(_M_X64) && !defined(_M_ARM64EC)
    const auto highBytes = _mm_set1_epi16(-256);
    const auto zero = _mm_setzero_si128();
    for (; values.size() - index >= 32; index += 32) {
        const auto* samples = values.data() + index;
        const auto first = _mm_or_si128(
            _mm_loadu_si128(reinterpret_cast<const __m128i*>(samples)),
            _mm_loadu_si128(reinterpret_cast<const __m128i*>(samples + 8)));
        const auto second = _mm_or_si128(
            _mm_loadu_si128(reinterpret_cast<const __m128i*>(samples + 16)),
            _mm_loadu_si128(reinterpret_cast<const __m128i*>(samples + 24)));
        // Values above 255 have at least one bit set in their high byte.
        const auto high = _mm_and_si128(_mm_or_si128(first, second), highBytes);
        if (_mm_movemask_epi8(_mm_cmpeq_epi8(high, zero)) != 0xFFFF) return false;
    }
#endif
    return std::all_of(values.cbegin() + index, values.cend(), [](auto value) { return value <= 255; });
}

std::unique_ptr<CalibrationItem> loadDsnu(
    const std::filesystem::path& file,
    std::string& error)
{
    MapHeader header;
    std::vector<std::uint16_t> values;
    if (!loadMap(file, 16, header, values, error)) {
        return nullptr;
    }
    // Keep the file format and every offset unchanged. Small dark offsets need
    // only one byte in the shared cache and in the per-frame memory stream.
    if (offsetsFitByte(values)) {
        std::vector<std::uint8_t> compact(values.size());
        std::size_t index = 0;
#if defined(_M_X64) && !defined(_M_ARM64EC)
        for (; values.size() - index >= 16; index += 16) {
            const auto* offsets = values.data() + index;
            const auto low = _mm_loadu_si128(reinterpret_cast<const __m128i*>(offsets));
            const auto high = _mm_loadu_si128(reinterpret_cast<const __m128i*>(offsets + 8));
            _mm_storeu_si128(reinterpret_cast<__m128i*>(compact.data() + index), _mm_packus_epi16(low, high));
        }
#endif
        std::transform(values.cbegin() + index, values.cend(), compact.begin() + index, [](auto value) {
            return static_cast<std::uint8_t>(value);
        });
        return std::make_unique<MapCalibration<std::uint8_t, DsnuOperation>>(
            CalibrationType::Dsnu, header, std::move(compact));
    }
    return std::make_unique<MapCalibration<std::uint16_t, DsnuOperation>>(
        CalibrationType::Dsnu, header, std::move(values));
}

std::unique_ptr<CalibrationItem> loadUniformity(
    const std::filesystem::path& file,
    std::string& error)
{
    MapHeader header;
    std::vector<float> values;
    if (!loadMap(file, 32, header, values, error)) {
        return nullptr;
    }
    return std::make_unique<MapCalibration<float, UniformityOperation>>(
        CalibrationType::Uniformity, header, std::move(values));
}

std::unique_ptr<CalibrationItem> loadLineArity(
    const std::filesystem::path& file,
    std::string& error)
{
    BinaryFile stream;
    std::uint64_t length = 0;
    if (!openBinary(file, stream, length, error)) {
        return nullptr;
    }
    if (length <= 16) {
        error = "LineArity calibration is too short";
        return nullptr;
    }

    std::uint32_t count = 0;
    if (!readPod(stream, count, error)) {
        return nullptr;
    }
    std::uint64_t dataBytes = 0;
    std::uint64_t expectedLength = 0;
    if (!checkedMultiply<std::uint64_t>(count, sizeof(float), dataBytes)
        || !checkedAdd(sizeof(std::uint32_t), dataBytes, expectedLength)
        || expectedLength != length) {
        error = "LineArity file length does not match its element count";
        return nullptr;
    }

    std::vector<float> factors(count);
    if (!seekAndRead(stream, sizeof(std::uint32_t), factors.data(), dataBytes, error)) {
        return nullptr;
    }
    return std::make_unique<LineArityCalibration>(std::move(factors));
}

} // namespace

std::unique_ptr<CalibrationItem> loadBasicCalibration(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error)
{
    error.clear();
    try {
        switch (type) {
        case CalibrationType::DarkNoise:
            return loadDarkNoise(file, error);
        case CalibrationType::DefectWPoint:
        case CalibrationType::DefectBPoint:
        case CalibrationType::DefectPoint:
            return loadDefectPoints(type, file, error);
        case CalibrationType::Dsnu:
            return loadDsnu(file, error);
        case CalibrationType::Uniformity:
            return loadUniformity(file, error);
        case CalibrationType::LineArity:
            return loadLineArity(file, error);
        default:
            return nullptr;
        }
    }
    catch (const std::bad_alloc&) {
        error = "Not enough memory to load calibration data";
    }
    catch (const std::exception& exception) {
        error = "Unable to load calibration: " + std::string(exception.what());
    }
    return nullptr;
}

} // namespace cvcore::calibration
