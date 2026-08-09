#pragma once

#include "calibration_core.h"

#include <cstddef>
#include <memory>
#include <mutex>
#include <cstdint>
#include <filesystem>
#include <vector>

namespace cvcore::calibration {

class CalibrationContext final {
public:
    bool clear();
    bool load(CalibrationType type, const std::filesystem::path& file);
    bool execute(const ImageView& raw, float* cieData, std::size_t cieElementCount, const ExecutionOptions& options);
    bool executeTo(
        const ImageView& sourceRaw,
        ImageView* correctedRaw,
        float* cieData,
        std::size_t cieElementCount,
        const ExecutionOptions& options);

    void recordError(std::string message);
    [[nodiscard]] std::string lastError() const;
    [[nodiscard]] std::size_t itemCount() const;

private:
    mutable std::mutex mutex_;
    // Each entry is a context-local lease over the process-wide per-file
    // cache. Stateless algorithms share their immutable item directly;
    // geometric algorithms use a private executor over shared full-frame maps.
    std::vector<std::shared_ptr<CalibrationItem>> items_;
    std::unique_ptr<std::uint8_t[]> rawScratch_;
    std::size_t rawScratchCapacity_ = 0;
    std::unique_ptr<std::uint8_t[]> rawScratch2_;
    std::size_t rawScratch2Capacity_ = 0;
    std::string lastError_;
};

struct CalibrationFileCacheStats {
    std::uint32_t entryCount = 0;
    std::uint64_t generation = 0;
    std::uint64_t estimatedMemoryBytes = 0;
    std::uint64_t budgetBytes = 0;
    std::uint64_t hitCount = 0;
    std::uint64_t missCount = 0;
};

struct CalibrationFileCacheEntry {
    CalibrationType type = CalibrationType::DarkNoise;
    std::uint32_t flags = 0;
    std::filesystem::path file;
    std::uint64_t generation = 0;
    std::uint64_t fileBytes = 0;
    std::uint64_t estimatedMemoryBytes = 0;
    std::uint64_t hitCount = 0;
    std::uint64_t lastAccessSequence = 0;
    std::uint32_t activeOwnerCount = 0;
};

struct CalibrationFileCacheReleaseResult {
    std::uint32_t releasedEntryCount = 0;
    std::uint64_t releasedEstimatedMemoryBytes = 0;
    std::uint32_t activeEntryCount = 0;
    std::uint32_t activeOwnerCount = 0;
    std::uint64_t activeEstimatedMemoryBytes = 0;
    std::uint64_t generation = 0;
};

inline constexpr std::uint32_t CalibrationFileCacheLoading = 1U;
inline constexpr std::uint32_t CalibrationFileCacheReady = 2U;

[[nodiscard]] CalibrationFileCacheStats calibrationFileCacheStats();
[[nodiscard]] bool calibrationFileCacheEntry(
    std::uint32_t index,
    CalibrationFileCacheEntry& entry);
[[nodiscard]] CalibrationFileCacheReleaseResult releaseCalibrationFileCache();

} // namespace cvcore::calibration
