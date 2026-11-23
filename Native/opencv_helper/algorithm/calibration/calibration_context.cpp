#include "calibration_context.h"

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>
#include <opencv2/core.hpp>

#include <algorithm>
#include <atomic>
#include <charconv>
#include <cmath>
#include <condition_variable>
#include <cstdlib>
#include <cstring>
#include <cwctype>
#include <filesystem>
#include <limits>
#include <new>
#include <optional>
#include <unordered_map>
#include <utility>

namespace cvcore::calibration {
namespace {

bool checkedMultiply(std::uint64_t left, std::uint64_t right, std::uint64_t& result) noexcept
{
    if (right != 0 && left > (std::numeric_limits<std::uint64_t>::max)() / right) {
        return false;
    }
    result = left * right;
    return true;
}

bool validImage(const ImageView& raw, std::size_t& requiredBytes, std::string& error)
{
    if (raw.data == nullptr) {
        error = "RAW data pointer is null";
        return false;
    }
    if (raw.width == 0 || raw.height == 0) {
        error = "RAW dimensions must be positive";
        return false;
    }
    if (raw.bitsPerChannel != 8 && raw.bitsPerChannel != 16) {
        error = "Only 8-bit and 16-bit RAW data are supported";
        return false;
    }
    if (raw.channels != 1 && raw.channels != 3) {
        error = "Only one-channel and three-channel RAW data are supported";
        return false;
    }

    std::uint64_t pixels = 0;
    std::uint64_t samples = 0;
    std::uint64_t bytes = 0;
    if (!checkedMultiply(raw.width, raw.height, pixels)
        || !checkedMultiply(pixels, raw.channels, samples)
        || !checkedMultiply(samples, raw.bitsPerChannel / 8, bytes)) {
        error = "RAW byte count overflows 64-bit arithmetic";
        return false;
    }
    if (bytes > (std::numeric_limits<std::size_t>::max)()) {
        error = "RAW byte count overflows the process address space";
        return false;
    }
    if (raw.dataLength < static_cast<std::size_t>(bytes)) {
        error = "RAW buffer is smaller than the declared image layout";
        return false;
    }
    requiredBytes = static_cast<std::size_t>(bytes);
    return true;
}

bool rangesOverlap(
    const void* first,
    std::size_t firstLength,
    const void* second,
    std::size_t secondLength,
    bool& valid) noexcept
{
    valid = false;
    if (first == nullptr || second == nullptr || firstLength == 0 || secondLength == 0) {
        valid = true;
        return false;
    }
    const auto firstBegin = reinterpret_cast<std::uintptr_t>(first);
    const auto secondBegin = reinterpret_cast<std::uintptr_t>(second);
    if (firstLength > (std::numeric_limits<std::uintptr_t>::max)() - firstBegin
        || secondLength > (std::numeric_limits<std::uintptr_t>::max)() - secondBegin) {
        return false;
    }
    const auto firstEnd = firstBegin + firstLength;
    const auto secondEnd = secondBegin + secondLength;
    valid = true;
    return firstBegin < secondEnd && secondBegin < firstEnd;
}

bool validColorExposure(const ExecutionOptions& options, std::string& error) noexcept
{
    for (const float exposure : options.exposure) {
        if (!std::isfinite(exposure) || exposure <= 0.0F) {
            error = "Color-calibration exposure values must be finite and positive";
            return false;
        }
    }
    return true;
}

struct FileMetadata {
    std::uintmax_t size = 0;
    std::filesystem::file_time_type lastWriteTime{};
    std::uint64_t fileIndex = 0;
    DWORD volumeSerial = 0;
    LONGLONG changeTime = 0;

    bool hasRevision() const noexcept { return fileIndex != 0 && changeTime != 0; }
    bool operator==(const FileMetadata&) const noexcept = default;
};

struct CalibrationFileKey {
    CalibrationType type = CalibrationType::Dsnu;
    std::filesystem::path file;

    bool operator==(const CalibrationFileKey&) const noexcept = default;
};

struct CalibrationFileKeyHash {
    std::size_t operator()(const CalibrationFileKey& key) const noexcept
    {
        const auto pathHash = std::filesystem::hash_value(key.file);
        const auto typeHash = std::hash<std::int32_t>{}(static_cast<std::int32_t>(key.type));
        return pathHash ^ (typeHash + static_cast<std::size_t>(0x9e3779b9U)
            + (pathHash << 6) + (pathHash >> 2));
    }
};

struct CalibrationAssetLease {
    std::atomic<std::uint32_t> activeOwners{ 0 };
};

struct CalibrationFileEntry {
    CalibrationFileEntry(
        FileMetadata value,
        std::filesystem::path canonicalPath,
        std::uint64_t cacheReleaseEpoch)
        : metadata(value)
        , canonicalFile(std::move(canonicalPath))
        , lease(std::make_shared<CalibrationAssetLease>())
        , estimatedMemoryBytes(static_cast<std::uint64_t>(value.size))
        , releaseEpoch(cacheReleaseEpoch)
    {
    }

    FileMetadata metadata;
    std::filesystem::path canonicalFile;
    std::atomic<bool> loading{ true };
    std::atomic<bool> retry{ false };
    std::shared_ptr<CalibrationItem> prototype;
    std::optional<CalibrationFileDigest> digest;
    std::shared_ptr<CalibrationAssetLease> lease;
    std::uint64_t estimatedMemoryBytes = 0;
    std::uint64_t hitCount = 0;
    std::uint64_t lastAccessSequence = 0;
    std::uint64_t releaseEpoch = 0;
    bool retained = false;
    bool canceled = false;
    std::string error;
    std::condition_variable ready;
};

std::filesystem::path normalizedCachePath(const std::filesystem::path& file)
{
    const std::wstring source = file.native();
    const int sourceLength = source.size() > static_cast<std::size_t>((std::numeric_limits<int>::max)())
        ? 0
        : static_cast<int>(source.size());
    if (sourceLength != 0) {
        const int required = LCMapStringEx(
            LOCALE_NAME_INVARIANT, LCMAP_UPPERCASE,
            source.data(), sourceLength, nullptr, 0, nullptr, nullptr, 0);
        if (required > 0) {
            std::wstring normalized(static_cast<std::size_t>(required), L'\0');
            if (LCMapStringEx(
                    LOCALE_NAME_INVARIANT, LCMAP_UPPERCASE,
                    source.data(), sourceLength, normalized.data(), required,
                    nullptr, nullptr, 0) == required) {
                return std::filesystem::path(std::move(normalized));
            }
        }
    }

    // The invariant Windows mapping above is the normal path. Retain a
    // no-throw-ish lexical fallback for unusually long paths or API failure.
    std::wstring normalized = source;
    std::transform(normalized.begin(), normalized.end(), normalized.begin(), [](wchar_t value) {
        return static_cast<wchar_t>(std::towupper(static_cast<std::wint_t>(value)));
    });
    return std::filesystem::path(std::move(normalized));
}

constexpr std::uint64_t DefaultCalibrationFileCacheBytes = 4ULL * 1024 * 1024 * 1024;
constexpr std::uint64_t Megabyte = 1024ULL * 1024;

std::uint64_t configuredCalibrationFileCacheBytes() noexcept
{
    static const std::uint64_t value = []() noexcept {
        const char* configured = std::getenv("COLORVISION_CALIBRATION_CACHE_MB");
        if (configured == nullptr || *configured == '\0') {
            return DefaultCalibrationFileCacheBytes;
        }
        std::uint64_t megabytes = 0;
        const char* end = configured + std::strlen(configured);
        const auto parsed = std::from_chars(configured, end, megabytes);
        if (parsed.ec != std::errc{} || parsed.ptr != end
            || megabytes > (std::numeric_limits<std::uint64_t>::max)() / Megabyte) {
            return DefaultCalibrationFileCacheBytes;
        }
        return megabytes * Megabyte;
    }();
    return value;
}

std::mutex calibrationFileCacheMutex;
std::unordered_map<CalibrationFileKey, std::shared_ptr<CalibrationFileEntry>, CalibrationFileKeyHash>
    calibrationFileCache;
std::uint64_t calibrationFileCacheBytes = 0;
std::uint64_t calibrationFileCacheGeneration = 1;
std::uint64_t calibrationFileCacheAccessSequence = 0;
std::uint64_t calibrationFileCacheHits = 0;
std::uint64_t calibrationFileCacheMisses = 0;
std::uint64_t calibrationFileCacheReleaseEpoch = 1;

void incrementCacheGeneration() noexcept
{
    ++calibrationFileCacheGeneration;
    if (calibrationFileCacheGeneration == 0) {
        calibrationFileCacheGeneration = 1;
    }
}

void incrementCacheReleaseEpoch() noexcept
{
    ++calibrationFileCacheReleaseEpoch;
    if (calibrationFileCacheReleaseEpoch == 0) {
        calibrationFileCacheReleaseEpoch = 1;
    }
}

void removeCacheAccounting(CalibrationFileEntry& entry) noexcept
{
    if (!entry.retained) return;
    calibrationFileCacheBytes = entry.estimatedMemoryBytes > calibrationFileCacheBytes
        ? 0
        : calibrationFileCacheBytes - entry.estimatedMemoryBytes;
    entry.retained = false;
}

void eraseCacheEntry(
    std::unordered_map<CalibrationFileKey, std::shared_ptr<CalibrationFileEntry>, CalibrationFileKeyHash>::iterator entry)
{
    removeCacheAccounting(*entry->second);
    calibrationFileCache.erase(entry);
    incrementCacheGeneration();
}

void evictCalibrationFiles(CalibrationFileEntry* mostRecent)
{
    const std::uint64_t budget = configuredCalibrationFileCacheBytes();
    while (calibrationFileCacheBytes > budget) {
        auto oldest = calibrationFileCache.end();
        for (auto iterator = calibrationFileCache.begin();
             iterator != calibrationFileCache.end(); ++iterator) {
            CalibrationFileEntry& candidate = *iterator->second;
            if (&candidate == mostRecent
                || candidate.loading.load(std::memory_order_acquire)
                || candidate.lease->activeOwners.load(std::memory_order_relaxed) != 0
                || !candidate.retained) {
                continue;
            }
            if (oldest == calibrationFileCache.end()
                || candidate.lastAccessSequence < oldest->second->lastAccessSequence) {
                oldest = iterator;
            }
        }
        // A single over-budget MRU is retained until the next insertion or an
        // explicit release, avoiding an immediate reload loop for large maps.
        if (oldest == calibrationFileCache.end()) break;
        eraseCacheEntry(oldest);
    }
}

void trimCalibrationFileCacheAfterOwnerRelease() noexcept
{
    try {
        std::lock_guard lock(calibrationFileCacheMutex);
        evictCalibrationFiles(nullptr);
    }
    catch (...) {
        // Calibration-item destruction is noexcept. A failed best-effort trim
        // must never turn context teardown into process termination; a later
        // cache operation or explicit release will retry the trim.
    }
}

void releaseCalibrationAssetOwner(
    const std::shared_ptr<CalibrationAssetLease>& lease) noexcept
{
    const std::uint32_t previous = lease->activeOwners.fetch_sub(
        1, std::memory_order_acq_rel);
    if (previous == 1) {
        trimCalibrationFileCacheAfterOwnerRelease();
    }
}

class CalibrationAssetOwnerReservation final {
public:
    CalibrationAssetOwnerReservation() = default;
    CalibrationAssetOwnerReservation(const CalibrationAssetOwnerReservation&) = delete;
    CalibrationAssetOwnerReservation& operator=(const CalibrationAssetOwnerReservation&) = delete;
    ~CalibrationAssetOwnerReservation() { reset(); }

    void reserve(const std::shared_ptr<CalibrationAssetLease>& lease) noexcept
    {
        lease_ = lease;
        lease_->activeOwners.fetch_add(1, std::memory_order_relaxed);
    }

    void handOff() noexcept { lease_.reset(); }

    void reset() noexcept
    {
        const auto lease = std::move(lease_);
        if (lease) releaseCalibrationAssetOwner(lease);
    }

private:
    std::shared_ptr<CalibrationAssetLease> lease_;
};

class LeasedCalibrationItem final : public CalibrationItem {
public:
    LeasedCalibrationItem(
        std::shared_ptr<CalibrationItem> implementation,
        std::shared_ptr<CalibrationAssetLease> lease)
        : implementation_(std::move(implementation))
        , lease_(std::move(lease))
    {
    }

    ~LeasedCalibrationItem() override
    {
        // Keep the owner count nonzero until this context has actually dropped
        // its algorithm/maps, so release accounting cannot observe zero while
        // the corresponding memory is still alive in this destructor.
        implementation_.reset();
        releaseCalibrationAssetOwner(lease_);
    }

    [[nodiscard]] CalibrationType type() const noexcept override { return implementation_->type(); }
    [[nodiscard]] bool isColorTransform() const noexcept override { return implementation_->isColorTransform(); }
    [[nodiscard]] std::optional<std::int32_t> requiredInputDepth() const noexcept override
    {
        return implementation_->requiredInputDepth();
    }
    [[nodiscard]] bool colorTransform(const ExecutionOptions& options, ColorTransform& transform) const override
    {
        return implementation_->colorTransform(options, transform);
    }
    [[nodiscard]] bool prepareColorTransform(
        const ImageView& raw, const ExecutionOptions& options, ColorTransform& transform, std::string& error) const override
    {
        return implementation_->prepareColorTransform(raw, options, transform, error);
    }
    [[nodiscard]] bool requiresDistinctOutput() const noexcept override
    {
        return implementation_->requiresDistinctOutput();
    }
    [[nodiscard]] bool supportsDistinctOutput() const noexcept override
    {
        return implementation_->supportsDistinctOutput();
    }

    bool apply(
        const ImageView& raw,
        float* cieData,
        const ExecutionOptions& options,
        std::string& error) override
    {
        return implementation_->apply(raw, cieData, options, error);
    }

    bool applyOutOfPlace(
        const ImageView& source,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        return implementation_->applyOutOfPlace(source, destination, options, error);
    }

    bool tryApplyWithFlip(const ImageView& raw, const ExecutionOptions& options) const override
    {
        return implementation_->tryApplyWithFlip(raw, options);
    }

    bool tryApplyWithNext(
        const CalibrationItem& next,
        const ImageView& raw,
        const ExecutionOptions& options) const override
    {
        const auto* leasedNext = dynamic_cast<const LeasedCalibrationItem*>(&next);
        return implementation_->tryApplyWithNext(
            leasedNext != nullptr ? *leasedNext->implementation_ : next, raw, options);
    }

    bool tryApplyWithNextTo(const CalibrationItem& next, const ImageView& raw,
        const ImageView& destination, const ExecutionOptions& options) const override
    {
        const auto* leasedNext = dynamic_cast<const LeasedCalibrationItem*>(&next);
        return implementation_->tryApplyWithNextTo(
            leasedNext != nullptr ? *leasedNext->implementation_ : next, raw, destination, options);
    }

    BgrNonzeroRows nonzeroBgrRows(const ImageView& raw, const ExecutionOptions& options) const noexcept override
    {
        return implementation_->nonzeroBgrRows(raw, options);
    }

    bool canFuseRawFlip(const ImageView& source, const ImageView& destination,
        const ExecutionOptions& options) const override
    {
        return implementation_->canFuseRawFlip(source, destination, options);
    }

private:
    std::shared_ptr<CalibrationItem> implementation_;
    std::shared_ptr<CalibrationAssetLease> lease_;
};

std::shared_ptr<CalibrationItem> createContextCalibrationItem(
    const std::shared_ptr<CalibrationItem>& prototype,
    const std::shared_ptr<CalibrationAssetLease>& lease,
    bool ownerReserved,
    std::string& error)
{
    if (!ownerReserved) {
        lease->activeOwners.fetch_add(1, std::memory_order_relaxed);
    }
    try {
        std::shared_ptr<CalibrationItem> implementation;
        if (prototype->shareInstanceAcrossContexts()) {
            implementation = prototype;
        }
        else {
            std::unique_ptr<CalibrationItem> cloned = prototype->cloneForContext();
            if (!cloned) {
                error = "Cached calibration item cannot create context-local execution state";
                releaseCalibrationAssetOwner(lease);
                return nullptr;
            }
            implementation = std::move(cloned);
        }
        return std::make_shared<LeasedCalibrationItem>(
            std::move(implementation), lease);
    }
    catch (...) {
        releaseCalibrationAssetOwner(lease);
        throw;
    }
}

// Once an entry is visible in calibrationFileCache, every exit path must publish a
// terminal state.  In particular, constructing a shared_ptr control block and
// filesystem/string operations below may throw.  This guard turns stack
// unwinding into a retryable failure and wakes all threads waiting on the same
// generation instead of leaving loading=true forever.
class PendingCalibrationFileLoad final {
public:
    PendingCalibrationFileLoad(const CalibrationFileKey& key, CalibrationFileEntry& entry) noexcept
        : key_(&key)
        , entry_(&entry)
    {
    }

    PendingCalibrationFileLoad(const PendingCalibrationFileLoad&) = delete;
    PendingCalibrationFileLoad& operator=(const PendingCalibrationFileLoad&) = delete;

    ~PendingCalibrationFileLoad() noexcept
    {
        if (active_) {
            abandon();
        }
    }

    bool succeed(
        const std::shared_ptr<CalibrationItem>& prototype,
        std::uint64_t estimatedMemoryBytes,
        std::string& error,
        const CalibrationFileDigest* digest = nullptr)
    {
        bool published = false;
        {
            std::lock_guard lock(calibrationFileCacheMutex);
            const auto current = calibrationFileCache.find(*key_);
            const bool currentGeneration = current != calibrationFileCache.end()
                && current->second.get() == entry_;
            const bool released = entry_->canceled
                || entry_->releaseEpoch != calibrationFileCacheReleaseEpoch;
            if (released || !currentGeneration) {
                error = released
                    ? "Calibration file load was canceled by cache release"
                    : "Calibration file load was superseded by a newer generation";
                entry_->canceled = released;
                entry_->retry.store(!released, std::memory_order_relaxed);
                entry_->error = error;
                entry_->loading.store(false, std::memory_order_release);
                eraseCurrentGeneration();
            }
            else {
                entry_->prototype = prototype;
                if (digest != nullptr) entry_->digest = *digest;
                entry_->estimatedMemoryBytes = estimatedMemoryBytes;
                entry_->lastAccessSequence = ++calibrationFileCacheAccessSequence;
                // Reserve the producer's context owner before publishing the
                // ready state. This closes the release-between-success-and-
                // lease window: an explicit release either cancels this load,
                // or reports the reserved owner as deferred active memory.
                entry_->lease->activeOwners.fetch_add(1, std::memory_order_relaxed);
                const bool disableRetention = configuredCalibrationFileCacheBytes() == 0;
                if (!disableRetention) {
                    entry_->retained = true;
                    calibrationFileCacheBytes = estimatedMemoryBytes
                            > (std::numeric_limits<std::uint64_t>::max)() - calibrationFileCacheBytes
                        ? (std::numeric_limits<std::uint64_t>::max)()
                        : calibrationFileCacheBytes + estimatedMemoryBytes;
                }
                entry_->loading.store(false, std::memory_order_release);
                incrementCacheGeneration();
                if (disableRetention) {
                    eraseCacheEntry(current);
                }
                else {
                    evictCalibrationFiles(entry_);
                }
                published = true;
            }
            active_ = false;
        }
        entry_->ready.notify_all();
        return published;
    }

    bool fail(std::string message, bool retry)
    {
        bool published = true;
        {
            std::lock_guard lock(calibrationFileCacheMutex);
            const bool released = entry_->canceled
                || entry_->releaseEpoch != calibrationFileCacheReleaseEpoch;
            if (released) {
                published = false;
                retry = false;
                message = "Calibration file load was canceled by cache release";
                entry_->canceled = true;
            }
            entry_->retry.store(retry, std::memory_order_relaxed);
            entry_->error = std::move(message);
            entry_->loading.store(false, std::memory_order_release);
            eraseCurrentGeneration();
            active_ = false;
        }
        entry_->ready.notify_all();
        return published;
    }

private:
    void eraseCurrentGeneration()
    {
        const auto current = calibrationFileCache.find(*key_);
        if (current != calibrationFileCache.end() && current->second.get() == entry_) {
            eraseCacheEntry(current);
        }
    }

    void abandon() noexcept
    {
        try {
            {
                std::lock_guard lock(calibrationFileCacheMutex);
                const bool released = entry_->canceled
                    || entry_->releaseEpoch != calibrationFileCacheReleaseEpoch;
                entry_->retry.store(!released, std::memory_order_relaxed);
                if (released) {
                    entry_->canceled = true;
                    entry_->error = "Calibration file load was canceled by cache release";
                }
                entry_->loading.store(false, std::memory_order_release);
                eraseCurrentGeneration();
                active_ = false;
            }
            entry_->ready.notify_all();
        }
        catch (...) {
            // std::mutex::lock can only fail for a broken synchronization
            // primitive.  Atomics still provide a last-resort terminal state
            // so a waiter predicate cannot remain true indefinitely.
            entry_->retry.store(true, std::memory_order_relaxed);
            entry_->loading.store(false, std::memory_order_release);
            entry_->ready.notify_all();
        }
    }

    const CalibrationFileKey* key_ = nullptr;
    CalibrationFileEntry* entry_ = nullptr;
    bool active_ = true;
};

bool inspectCalibrationFile(
    const std::filesystem::path& file,
    std::filesystem::path& canonicalFile,
    FileMetadata& metadata,
    std::string& error)
{
    std::error_code fileError;
    canonicalFile = std::filesystem::weakly_canonical(file, fileError);
    if (fileError) {
        error = "Unable to resolve calibration file path: " + fileError.message();
        return false;
    }

    metadata.size = std::filesystem::file_size(canonicalFile, fileError);
    if (fileError) {
        error = "Unable to read calibration file size: " + fileError.message();
        return false;
    }
    metadata.lastWriteTime = std::filesystem::last_write_time(canonicalFile, fileError);
    if (fileError) {
        error = "Unable to read calibration file modification time: " + fileError.message();
        return false;
    }
    // Installers may preserve file size and last-write time when replacing coefficients.
    const HANDLE handle = CreateFileW(canonicalFile.c_str(), FILE_READ_ATTRIBUTES,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (handle != INVALID_HANDLE_VALUE) {
        BY_HANDLE_FILE_INFORMATION info{};
        FILE_BASIC_INFO basic{};
        if (GetFileInformationByHandle(handle, &info)
            && GetFileInformationByHandleEx(handle, FileBasicInfo, &basic, sizeof(basic))) {
            metadata.fileIndex = (static_cast<std::uint64_t>(info.nFileIndexHigh) << 32) | info.nFileIndexLow;
            metadata.volumeSerial = info.dwVolumeSerialNumber;
            metadata.changeTime = basic.ChangeTime.QuadPart;
        }
        CloseHandle(handle);
    }
    return true;
}

enum class CalibrationFileLoadResult {
    Success,
    Failure,
    Retry,
};

CalibrationFileLoadResult loadCalibrationFileOnce(
    CalibrationType type,
    const std::filesystem::path& file,
    std::shared_ptr<CalibrationItem>& result,
    std::string& error,
    CalibrationFileDigest* digest)
{
    std::filesystem::path canonicalFile;
    FileMetadata metadata;
    if (!inspectCalibrationFile(file, canonicalFile, metadata, error)) {
        return CalibrationFileLoadResult::Failure;
    }

    CalibrationFileKey key{ type, normalizedCachePath(canonicalFile) };
    const auto pathStillCurrent = [&](const std::filesystem::path& path, std::string& inspectionError) {
        FileMetadata metadataAfter;
        std::filesystem::path canonicalAfter;
        if (!inspectCalibrationFile(path, canonicalAfter, metadataAfter, inspectionError)) return false;
        if (normalizedCachePath(canonicalAfter) != key.file || !(metadataAfter == metadata)) {
            inspectionError = "Calibration file changed while it was being loaded";
            return false;
        }
        return true;
    };
    std::unique_ptr<VerifiedMapSource> verifiedSource;
    if (digest != nullptr) {
        verifiedSource = openVerifiedMapSource(type, canonicalFile, error);
        if (!verifiedSource) return CalibrationFileLoadResult::Failure;
        if (!pathStillCurrent(file, error)) return CalibrationFileLoadResult::Retry;
    }

    CalibrationAssetOwnerReservation comparisonOwner;
    std::shared_ptr<CalibrationFileEntry> entry;
    {
        std::unique_lock lock(calibrationFileCacheMutex);
        const auto existing = calibrationFileCache.find(key);
        if (existing != calibrationFileCache.end() && existing->second->metadata == metadata
            && (metadata.hasRevision() || verifiedSource)) {
            entry = existing->second;
            const bool waitedForProducer = entry->loading.load(std::memory_order_acquire);
            if (waitedForProducer) {
                entry->ready.wait(lock, [&entry] {
                    return !entry->loading.load(std::memory_order_acquire);
                });
            }
            if (entry->canceled
                || entry->releaseEpoch != calibrationFileCacheReleaseEpoch) {
                error = entry->error.empty()
                    ? "Calibration file load was canceled by cache release"
                    : entry->error;
                return CalibrationFileLoadResult::Failure;
            }
            if (entry->prototype && (!verifiedSource || entry->digest.has_value())) {
                const auto prototype = entry->prototype;
                const auto lease = entry->lease;
                auto match = MapSourceMatch::Equal;
                if (verifiedSource) {
                    const auto checkCurrentEntry = [&] {
                        const auto current = calibrationFileCache.find(key);
                        // A zero-budget producer can hand its ready prototype
                        // to waiters without retaining a cache entry.
                        const bool canUse = current == calibrationFileCache.end()
                            ? configuredCalibrationFileCacheBytes() == 0
                            : current->second == entry;
                        if (!canUse) error = "Calibration file load was superseded by a newer generation";
                        return canUse;
                    };
                    // An over-budget prototype may have been evicted while
                    // the waiter was resuming after the producer published it.
                    if (waitedForProducer && !checkCurrentEntry()) return CalibrationFileLoadResult::Retry;
                    // A cache release must account for the prototype while it
                    // is being compared, without blocking other file loads.
                    comparisonOwner.reserve(lease);
                    lock.unlock();
                    match = verifiedSource->compare(*prototype, error);
                    const bool fileUnchanged = pathStillCurrent(file, error);
                    lock.lock();
                    if (entry->canceled || entry->releaseEpoch != calibrationFileCacheReleaseEpoch) {
                        error = "Calibration file load was canceled by cache release";
                        return CalibrationFileLoadResult::Failure;
                    }
                    if (!fileUnchanged) return CalibrationFileLoadResult::Retry;
                    if (!checkCurrentEntry()) return CalibrationFileLoadResult::Retry;
                    if (match == MapSourceMatch::Error) return CalibrationFileLoadResult::Failure;
                }
                if (match == MapSourceMatch::Equal) {
                    ++entry->hitCount;
                    ++calibrationFileCacheHits;
                    entry->lastAccessSequence = ++calibrationFileCacheAccessSequence;
                    // Reserve ownership before unlocking so a concurrent
                    // release cannot miss an about-to-be-created lease.
                    if (!verifiedSource) lease->activeOwners.fetch_add(1, std::memory_order_relaxed);
                    incrementCacheGeneration();
                    evictCalibrationFiles(entry.get());
                    const auto verifiedDigest = entry->digest;
                    lock.unlock();
                    if (verifiedSource) comparisonOwner.handOff();
                    result = createContextCalibrationItem(prototype, lease, true, error);
                    if (!result) return CalibrationFileLoadResult::Failure;
                    if (digest != nullptr) *digest = *verifiedDigest;
                    return CalibrationFileLoadResult::Success;
                }
            }
            if (entry->retry.load(std::memory_order_relaxed)) {
                error = entry->error;
                return CalibrationFileLoadResult::Retry;
            }
            if (!entry->error.empty()) {
                error = entry->error;
                return CalibrationFileLoadResult::Failure;
            }
            // Waiting can release the lock, so do not replace a newer entry
            // when the observed one had no digest and needs to be reloaded.
            const auto current = calibrationFileCache.find(key);
            if (verifiedSource && current != calibrationFileCache.end() && current->second != entry) {
                error = "Calibration file load was superseded by a newer generation";
                return CalibrationFileLoadResult::Retry;
            }
        }

        // A different snapshot, or a verified mismatch, supersedes the old
        // entry. Existing contexts still own their immutable version.
        entry = std::make_shared<CalibrationFileEntry>(
            metadata, std::move(canonicalFile), calibrationFileCacheReleaseEpoch);
        const auto current = calibrationFileCache.find(key);
        const auto replaced = current == calibrationFileCache.end()
            ? std::shared_ptr<CalibrationFileEntry>{}
            : current->second;
        calibrationFileCache.insert_or_assign(key, entry);
        if (replaced) removeCacheAccounting(*replaced);
        ++calibrationFileCacheMisses;
        incrementCacheGeneration();
    }
    PendingCalibrationFileLoad pendingLoad(key, *entry);
    // Releasing an owner may lock the cache to trim its retained entries.
    comparisonOwner.reset();

    std::string loadError;
    std::unique_ptr<CalibrationItem> loaded;
    CalibrationFileDigest loadedDigest{};
    if (verifiedSource) {
        loaded = verifiedSource->load(loadedDigest, loadError);
    }
    else if (isColorCalibration(type)) {
        loaded = loadColorCalibration(type, entry->canonicalFile, loadError);
    }
    else {
        loaded = loadBasicCalibration(type, entry->canonicalFile, loadError);
        if (!loaded && loadError.empty()) {
            loaded = loadGeometricCalibration(type, entry->canonicalFile, loadError);
        }
    }
    std::shared_ptr<CalibrationItem> loadedShared;
    if (loaded) {
        loadedShared = std::move(loaded);
    }

    std::string inspectionError;
    if (!pathStillCurrent(verifiedSource ? file : entry->canonicalFile, inspectionError)) {
        error = std::move(inspectionError);
        return pendingLoad.fail(error, true)
            ? CalibrationFileLoadResult::Retry
            : CalibrationFileLoadResult::Failure;
    }
    if (!loadedShared) {
        error = loadError.empty()
            ? "Unable to load calibration file"
            : std::move(loadError);
        pendingLoad.fail(error, false);
        return CalibrationFileLoadResult::Failure;
    }
    const auto footprint = loadedShared->cacheFootprintBytes();
    // Binary maps report all storage they retain after lossless packing.
    const std::uint64_t estimatedMemoryBytes = (type == CalibrationType::Dsnu || type == CalibrationType::Uniformity)
        ? footprint : (std::max)(static_cast<std::uint64_t>(metadata.size), footprint);
    if (!pendingLoad.succeed(loadedShared, estimatedMemoryBytes, error, verifiedSource ? &loadedDigest : nullptr)) {
        return entry->retry.load(std::memory_order_relaxed)
            ? CalibrationFileLoadResult::Retry
            : CalibrationFileLoadResult::Failure;
    }
    result = createContextCalibrationItem(
        loadedShared, entry->lease, true, error);
    if (!result) return CalibrationFileLoadResult::Failure;
    if (digest != nullptr) *digest = loadedDigest;
    return CalibrationFileLoadResult::Success;
}

std::shared_ptr<CalibrationItem> loadCachedCalibrationFile(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error,
    CalibrationFileDigest* digest)
{
    // A producer validates metadata again after reading.  Retry a bounded
    // number of times if the file is atomically replaced during that window.
    for (int attempt = 0; attempt < 3; ++attempt) {
        std::shared_ptr<CalibrationItem> item;
        const auto loadResult = loadCalibrationFileOnce(type, file, item, error, digest);
        if (loadResult == CalibrationFileLoadResult::Success) {
            return item;
        }
        if (loadResult == CalibrationFileLoadResult::Failure) {
            return nullptr;
        }
    }
    error = "Calibration file changed repeatedly while it was being loaded";
    return nullptr;
}

} // namespace

CalibrationFileCacheStats calibrationFileCacheStats()
{
    std::lock_guard lock(calibrationFileCacheMutex);
    CalibrationFileCacheStats result;
    result.entryCount = calibrationFileCache.size() > (std::numeric_limits<std::uint32_t>::max)()
        ? (std::numeric_limits<std::uint32_t>::max)()
        : static_cast<std::uint32_t>(calibrationFileCache.size());
    result.generation = calibrationFileCacheGeneration;
    result.estimatedMemoryBytes = calibrationFileCacheBytes;
    result.budgetBytes = configuredCalibrationFileCacheBytes();
    result.hitCount = calibrationFileCacheHits;
    result.missCount = calibrationFileCacheMisses;
    return result;
}

bool calibrationFileCacheEntry(
    std::uint32_t index,
    CalibrationFileCacheEntry& result)
{
    std::lock_guard lock(calibrationFileCacheMutex);
    if (index >= calibrationFileCache.size()) return false;

    using Snapshot = std::pair<CalibrationFileKey, std::shared_ptr<CalibrationFileEntry>>;
    std::vector<Snapshot> entries;
    entries.reserve(calibrationFileCache.size());
    for (const auto& entry : calibrationFileCache) {
        entries.emplace_back(entry.first, entry.second);
    }
    std::sort(entries.begin(), entries.end(), [](const Snapshot& left, const Snapshot& right) {
        if (left.second->lastAccessSequence != right.second->lastAccessSequence) {
            return left.second->lastAccessSequence > right.second->lastAccessSequence;
        }
        if (left.first.type != right.first.type) {
            return static_cast<std::int32_t>(left.first.type)
                < static_cast<std::int32_t>(right.first.type);
        }
        return left.first.file.native() < right.first.file.native();
    });

    const auto& selected = entries[index];
    const CalibrationFileEntry& entry = *selected.second;
    result.type = selected.first.type;
    result.flags = entry.loading.load(std::memory_order_acquire)
        ? CalibrationFileCacheLoading
        : CalibrationFileCacheReady;
    result.file = entry.canonicalFile;
    result.generation = calibrationFileCacheGeneration;
    result.fileBytes = static_cast<std::uint64_t>(entry.metadata.size);
    result.estimatedMemoryBytes = entry.estimatedMemoryBytes;
    result.hitCount = entry.hitCount;
    result.lastAccessSequence = entry.lastAccessSequence;
    result.activeOwnerCount = entry.lease->activeOwners.load(std::memory_order_relaxed);
    return true;
}

CalibrationFileCacheReleaseResult releaseCalibrationFileCache()
{
    CalibrationFileCacheReleaseResult result;
    std::vector<std::shared_ptr<CalibrationFileEntry>> notifyCanceledLoads;
    {
        std::lock_guard lock(calibrationFileCacheMutex);
        incrementCacheReleaseEpoch();
        result.releasedEntryCount = calibrationFileCache.size() > (std::numeric_limits<std::uint32_t>::max)()
            ? (std::numeric_limits<std::uint32_t>::max)()
            : static_cast<std::uint32_t>(calibrationFileCache.size());

        std::uint64_t activeOwners = 0;
        for (auto& cached : calibrationFileCache) {
            CalibrationFileEntry& entry = *cached.second;
            if (entry.retained) {
                result.releasedEstimatedMemoryBytes += entry.estimatedMemoryBytes;
            }

            const bool wasLoading = entry.loading.exchange(false, std::memory_order_acq_rel);
            entry.canceled = true;
            entry.retry.store(false, std::memory_order_relaxed);
            if (wasLoading) {
                entry.error = "Calibration file load was canceled by cache release";
                notifyCanceledLoads.push_back(cached.second);
            }

            const std::uint32_t owners = entry.lease->activeOwners.load(std::memory_order_relaxed);
            const bool deferred = wasLoading || owners != 0;
            if (deferred) {
                ++result.activeEntryCount;
                result.activeEstimatedMemoryBytes += entry.estimatedMemoryBytes;
            }
            activeOwners += owners;
            removeCacheAccounting(entry);
        }
        result.activeOwnerCount = activeOwners > (std::numeric_limits<std::uint32_t>::max)()
            ? (std::numeric_limits<std::uint32_t>::max)()
            : static_cast<std::uint32_t>(activeOwners);
        calibrationFileCache.clear();
        incrementCacheGeneration();
        result.generation = calibrationFileCacheGeneration;
    }
    for (const auto& entry : notifyCanceledLoads) {
        entry->ready.notify_all();
    }
    return result;
}

bool isColorCalibration(CalibrationType type) noexcept
{
    return type == CalibrationType::Luminance
        || type == CalibrationType::LumOneColor
        || type == CalibrationType::LumFourColor
        || type == CalibrationType::LumMultiColor;
}

bool isSupportedCalibration(CalibrationType type) noexcept
{
    const auto value = static_cast<std::int32_t>(type);
    return value >= static_cast<std::int32_t>(CalibrationType::DarkNoise)
        && value <= static_cast<std::int32_t>(CalibrationType::AngleShift)
        && type != CalibrationType::LumColor;
}

bool CalibrationContext::clear()
{
    std::scoped_lock lock(mutex_);
    items_.clear();
    rawScratch_.reset();
    rawScratchCapacity_ = 0;
    rawScratch2_.reset();
    rawScratch2Capacity_ = 0;
    lastError_.clear();
    return true;
}

void CalibrationContext::transferWorkBuffersFrom(CalibrationContext& source)
{
    if (this == &source) return;

    std::scoped_lock lock(mutex_, source.mutex_);
    const auto takeLarger = [](std::unique_ptr<std::uint8_t[]>& destinationBuffer, std::size_t& destinationCapacity,
        std::unique_ptr<std::uint8_t[]>& sourceBuffer, std::size_t& sourceCapacity) {
        if (sourceCapacity > destinationCapacity) {
            destinationBuffer = std::move(sourceBuffer);
            destinationCapacity = sourceCapacity;
        }
        sourceBuffer.reset();
        sourceCapacity = 0;
    };
    takeLarger(rawScratch_, rawScratchCapacity_, source.rawScratch_, source.rawScratchCapacity_);
    takeLarger(rawScratch2_, rawScratch2Capacity_, source.rawScratch2_, source.rawScratch2Capacity_);
}

bool CalibrationContext::load(CalibrationType type, const std::filesystem::path& file, CalibrationFileDigest* digest)
{
    std::scoped_lock lock(mutex_);
    lastError_.clear();

    if (!isSupportedCalibration(type)) {
        lastError_ = type == CalibrationType::LumColor
            ? "LumColor is a reserved legacy value and has no calibration algorithm"
            : "Unknown calibration type";
        return false;
    }
    if (digest != nullptr && type != CalibrationType::Dsnu && type != CalibrationType::Uniformity) {
        lastError_ = "Verified calibration loading supports only DSNU and Uniformity";
        return false;
    }
    if (file.empty()) {
        lastError_ = "Calibration file path is empty";
        return false;
    }

    std::error_code fileError;
    if (!std::filesystem::is_regular_file(file, fileError)) {
        lastError_ = fileError
            ? "Unable to inspect calibration file: " + fileError.message()
            : "Calibration file does not exist";
        return false;
    }

    std::string error;
    CalibrationFileDigest loadedDigest{};
    std::shared_ptr<CalibrationItem> item = loadCachedCalibrationFile(
        type, file, error, digest != nullptr ? &loadedDigest : nullptr);

    if (!item) {
        lastError_ = error.empty() ? "Calibration type has no loader" : std::move(error);
        return false;
    }

    items_.push_back(std::move(item));
    if (digest != nullptr) *digest = loadedDigest;
    return true;
}

bool CalibrationContext::requiredInputDepth(std::uint32_t& bitsPerChannel)
{
    std::scoped_lock lock(mutex_);
    lastError_.clear();
    std::uint32_t required = 0;
    for (const auto& item : items_) {
        const auto stageDepth = item->requiredInputDepth();
        if (!stageDepth) continue;
        if (*stageDepth != 8 && *stageDepth != 16) {
            lastError_ = "Calibration stage declares unsupported RAW depth: " + std::to_string(*stageDepth);
            return false;
        }
        const auto depth = static_cast<std::uint32_t>(*stageDepth);
        if (required != 0 && required != depth) {
            lastError_ = "Calibration template contains incompatible 8-bit and 16-bit RAW requirements";
            return false;
        }
        required = depth;
    }
    bitsPerChannel = required;
    return true;
}

bool CalibrationContext::execute(
    const ImageView& raw,
    float* cieData,
    std::size_t cieElementCount,
    const ExecutionOptions& options,
    ColorTransform* preparedColor)
{
    std::scoped_lock lock(mutex_);
    lastError_.clear();

    std::size_t requiredRawBytes = 0;
    if (!validImage(raw, requiredRawBytes, lastError_)) {
        return false;
    }

    CalibrationItem* colorItem = nullptr;
    for (const auto& item : items_) {
        if (!item->isColorTransform()) {
            continue;
        }
        if (colorItem != nullptr) {
            lastError_ = "Only one luminance/color calibration may be selected";
            return false;
        }
        colorItem = item.get();
    }

    if (preparedColor != nullptr && colorItem == nullptr) {
        lastError_ = "Selected output requires a luminance/color calibration";
        return false;
    }
    if (preparedColor == nullptr && colorItem != nullptr && cieData == nullptr) {
        lastError_ = "CIE output pointer is null";
        return false;
    }
    if (colorItem != nullptr) {
        const bool validColor = preparedColor != nullptr
            ? colorItem->prepareColorTransform(raw, options, *preparedColor, lastError_)
            : validColorExposure(options, lastError_);
        if (!validColor) return false;
    }
    const bool writeCie = colorItem != nullptr && cieData != nullptr;
    if (writeCie) {
        std::uint64_t pixels = 0;
        std::uint64_t requiredElements = 0;
        const auto planes = colorItem->type() == CalibrationType::Luminance ? 1ULL : 3ULL;
        if (!checkedMultiply(raw.width, raw.height, pixels)
            || !checkedMultiply(pixels, planes, requiredElements)
            || requiredElements > (std::numeric_limits<std::size_t>::max)()
            || requiredElements > (std::numeric_limits<std::size_t>::max)() / sizeof(float)) {
            lastError_ = "CIE element count overflows the process address space";
            return false;
        }
        if (cieElementCount < static_cast<std::size_t>(requiredElements)) {
            lastError_ = "CIE buffer is smaller than the selected color transform requires";
            return false;
        }
        const auto requiredCieBytes = static_cast<std::size_t>(requiredElements) * sizeof(float);

        bool validRange = false;
        if (rangesOverlap(raw.data, requiredRawBytes, cieData, requiredCieBytes, validRange)) {
            lastError_ = "RAW and CIE buffers must not overlap";
            return false;
        }
        if (!validRange) {
            lastError_ = "RAW or CIE buffer address range overflows the process address space";
            return false;
        }
    }

    const bool hasBasic = items_.size() > (colorItem == nullptr ? 0U : 1U);
    if (options.preserveRawLayout && !options.interleavedBgr && hasBasic) {
        // Basic stages share the packed BGR execution path. Legacy stages may
        // assume interleaved indexing or merge planar inputs into packed output.
        constexpr auto maxCvDimension = static_cast<std::uint32_t>((std::numeric_limits<int>::max)());
        if (raw.width > maxCvDimension || raw.height > maxCvDimension) {
            lastError_ = "Planar RAW dimensions exceed OpenCV's integer range";
            return false;
        }
        if (!ensureScratch(rawScratch2_, rawScratch2Capacity_, requiredRawBytes)) return false;
        const int depth = raw.bitsPerChannel == 8 ? CV_8U : CV_16U;
        const cv::Size size(static_cast<int>(raw.width), static_cast<int>(raw.height));
        const auto planeBytes = requiredRawBytes / 3;
        std::array<cv::Mat, 3> bgrPlanes{
            cv::Mat(size, depth, raw.data + 2 * planeBytes),
            cv::Mat(size, depth, raw.data + planeBytes),
            cv::Mat(size, depth, raw.data)
        };
        ImageView packed = raw;
        packed.data = rawScratch2_.get();
        packed.dataLength = rawScratch2Capacity_;
        cv::Mat bgr(size, CV_MAKETYPE(depth, 3), packed.data);
        cv::merge(bgrPlanes.data(), bgrPlanes.size(), bgr);
        ExecutionOptions packedOptions = options;
        packedOptions.interleavedBgr = true;
        if (!executeStages(packed, requiredRawBytes, cieData, packedOptions, colorItem, preparedColor)) return false;
        cv::split(bgr, bgrPlanes.data());
        return true;
    }
    return executeStages(raw, requiredRawBytes, cieData, options, colorItem, preparedColor);
}

bool CalibrationContext::ensureScratch(
    std::unique_ptr<std::uint8_t[]>& buffer, std::size_t& capacity, std::size_t requiredBytes)
{
    if (capacity >= requiredBytes) return true;
    std::unique_ptr<std::uint8_t[]> replacement(new (std::nothrow) std::uint8_t[requiredBytes]);
    if (!replacement) {
        lastError_ = "Not enough memory for a calibration work buffer";
        return false;
    }
    buffer = std::move(replacement);
    capacity = requiredBytes;
    return true;
}

bool CalibrationContext::executeStages(
    const ImageView& raw, std::size_t requiredRawBytes, float* cieData, const ExecutionOptions& options,
    CalibrationItem* colorItem, const ColorTransform* preparedColor)
{
    const bool writeCie = colorItem != nullptr && cieData != nullptr;
    // A trailing deferred color item has already done its work. Exclude it so
    // the final basic stage can use the same fast paths as a RAW-only plan.
    auto stages = std::span(items_);
    if (preparedColor != nullptr && !writeCie && stages.back().get() == colorItem) {
        stages = stages.first(stages.size() - 1);
    }
    const bool requiresScratch = std::any_of(
        stages.begin(), stages.end(), [](const auto& item) {
            return !item->isColorTransform() && item->requiresDistinctOutput();
        });
    if (requiresScratch && !ensureScratch(rawScratch_, rawScratchCapacity_, requiredRawBytes)) return false;

    // Preserve LocalCalibrationCacheManager semantics: every basic item runs
    // in template order; the sole RAW-to-CIE transform always runs last.
    const bool endsInColorDiff = !stages.empty() && stages.back()->type() == CalibrationType::ColorDiff;
    const bool canSeedScratchFromPair = !writeCie && stages.size() >= 3
        && stages[stages.size() - 3]->type() == CalibrationType::Dsnu
        && stages[stages.size() - 2]->type() == CalibrationType::Uniformity
        && (endsInColorDiff || stages.back()->type() == CalibrationType::Distortion);
    bool fusedRawFlip = options.rawOutputFlip == 0 && endsInColorDiff;
    const bool canFuseHorizontalFlip = (options.rawOutputFlip == 1 || options.rawOutputFlip == -1)
        && options.interleavedBgr && options.rgbType == 0 && raw.channels == 3
        && (raw.bitsPerChannel == 8 || reinterpret_cast<std::uintptr_t>(raw.data) % alignof(std::uint16_t) == 0)
        && endsInColorDiff;
    ExecutionOptions calibrationOptions = options;
    calibrationOptions.rawOutputFlip = -99;
    ImageView current = raw;
    ImageView scratch = raw;
    scratch.data = rawScratch_.get();
    scratch.dataLength = rawScratchCapacity_;
    BgrNonzeroRows nextDistortionRows{};
    for (std::size_t index = 0; index < stages.size(); ++index) {
        const auto& item = stages[index];
        if (item->isColorTransform()) {
            continue;
        }
        // Once preceding stages have run, seed scratch from the adjacent maps
        // so final geometry can write back to RAW without a full-frame copy.
        if (canSeedScratchFromPair && index + 3 == stages.size() && current.data == raw.data) {
            const auto& geometry = stages.back();
            if ((options.rawOutputFlip == -99 || fusedRawFlip || canFuseHorizontalFlip
                    || geometry->canFuseRawFlip(scratch, raw, options))
                && item->tryApplyWithNextTo(*stages[index + 1], current, scratch, calibrationOptions)) {
                const auto restorePairOutput = [&] { std::memcpy(raw.data, scratch.data, requiredRawBytes); };
                std::string error;
                try {
                    auto geometryOptions = options;
                    if (options.rawOutputFlip == -99 && geometry->type() == CalibrationType::Distortion) {
                        geometryOptions.correctedBgrRows = stages[index + 1]->nonzeroBgrRows(scratch, calibrationOptions);
                    }
                    if (geometry->applyOutOfPlace(scratch, raw, geometryOptions, error)) return true;
                }
                catch (...) {
                    restorePairOutput();
                    throw;
                }
                // The ordinary path exposes all preceding corrections on failure.
                restorePairOutput();
                lastError_ = error.empty() ? "Basic calibration failed" : std::move(error);
                return false;
            }
        }
        const bool flipAfterPair = options.rawOutputFlip == 0 && index + 2 == stages.size();
        if (index + 1 < stages.size()
            && item->tryApplyWithNext(*stages[index + 1], current,
                flipAfterPair ? options : calibrationOptions)) {
            if (flipAfterPair) fusedRawFlip = true;
            if (index + 2 < stages.size() && stages[index + 2]->type() == CalibrationType::Distortion) {
                nextDistortionRows = stages[index + 1]->nonzeroBgrRows(current, calibrationOptions);
            }
            ++index;
            continue;
        }
        if (options.rawOutputFlip == 0 && index + 1 == stages.size()
            && item->tryApplyWithFlip(current, options)) {
            fusedRawFlip = true;
            continue;
        }
        std::string error;
        bool applied = false;
        if (item->requiresDistinctOutput()) {
            const ImageView& destination = current.data == raw.data ? scratch : raw;
            const bool finalStage = index + 1 == stages.size();
            // With an odd stage count the existing flip also copies scratch back to RAW.
            const bool fuseHorizontalFlip = canFuseHorizontalFlip && finalStage && destination.data == raw.data;
            const auto& stageOptions = finalStage && (fusedRawFlip || fuseHorizontalFlip)
                ? options : calibrationOptions;
            if (!nextDistortionRows.rows.empty()) {
                auto geometryOptions = stageOptions;
                geometryOptions.correctedBgrRows = nextDistortionRows;
                nextDistortionRows = {};
                applied = item->applyOutOfPlace(current, destination, geometryOptions, error);
            }
            else {
                applied = item->applyOutOfPlace(current, destination, stageOptions, error);
            }
            if (applied) {
                current = destination;
                fusedRawFlip = fusedRawFlip || fuseHorizontalFlip;
            }
        }
        else {
            applied = item->apply(current, nullptr, calibrationOptions, error);
        }
        if (!applied) {
            lastError_ = error.empty() ? "Basic calibration failed" : std::move(error);
            return false;
        }
    }

    if (writeCie && preparedColor != nullptr) {
        writeColorTransform(current, options.interleavedBgr, *preparedColor, cieData);
    }
    else if (writeCie) {
        std::string error;
        if (!colorItem->apply(current, cieData, options, error)) {
            lastError_ = error.empty() ? "Luminance/color calibration failed" : std::move(error);
            return false;
        }
    }
    if (options.rawOutputFlip != -99 && !fusedRawFlip) {
        // With odd geometric stage counts this also replaces the final scratch
        // copy. Planar RAW must mirror each plane, not interleaved samples.
        const bool planar = !options.interleavedBgr && raw.channels == 3;
        const std::size_t planeBytes = planar ? requiredRawBytes / 3 : requiredRawBytes;
        const int type = CV_MAKETYPE(raw.bitsPerChannel == 8 ? CV_8U : CV_16U, planar ? 1 : raw.channels);
        for (int plane = 0; plane < (planar ? 3 : 1); ++plane) {
            cv::Mat source(raw.height, raw.width, type, current.data + plane * planeBytes);
            cv::Mat destination(raw.height, raw.width, type, raw.data + plane * planeBytes);
            cv::flip(source, destination, options.rawOutputFlip);
        }
    }
    else if (current.data != raw.data) {
        std::memcpy(raw.data, current.data, requiredRawBytes);
    }
    return true;
}

bool CalibrationContext::executeTo(
    const ImageView& sourceRaw,
    ImageView* correctedRaw,
    float* cieData,
    std::size_t cieElementCount,
    const ExecutionOptions& options)
{
    std::scoped_lock lock(mutex_);
    lastError_.clear();

    std::size_t requiredRawBytes = 0;
    if (!validImage(sourceRaw, requiredRawBytes, lastError_)) {
        return false;
    }
    if (correctedRaw != nullptr) {
        std::size_t correctedRawBytes = 0;
        if (correctedRaw->width != sourceRaw.width
            || correctedRaw->height != sourceRaw.height
            || correctedRaw->bitsPerChannel != sourceRaw.bitsPerChannel
            || correctedRaw->channels != sourceRaw.channels
            || !validImage(*correctedRaw, correctedRawBytes, lastError_)
            || correctedRawBytes != requiredRawBytes) {
            if (lastError_.empty()) lastError_ = "Corrected RAW layout does not match the source RAW layout";
            return false;
        }
    }

    CalibrationItem* colorItem = nullptr;
    std::size_t basicCount = 0;
    std::size_t distinctAfterFirst = 0;
    for (const auto& item : items_) {
        if (item->isColorTransform()) {
            if (colorItem != nullptr) {
                lastError_ = "Only one luminance/color calibration may be selected";
                return false;
            }
            colorItem = item.get();
            continue;
        }
        if (basicCount != 0 && item->requiresDistinctOutput()) {
            ++distinctAfterFirst;
        }
        ++basicCount;
    }

    std::size_t requiredCieBytes = 0;
    if (colorItem != nullptr) {
        if (!validColorExposure(options, lastError_)) {
            return false;
        }
        if (cieData == nullptr) {
            lastError_ = "CIE output pointer is null";
            return false;
        }
        std::uint64_t pixels = 0;
        std::uint64_t requiredElements = 0;
        const auto planes = colorItem->type() == CalibrationType::Luminance ? 1ULL : 3ULL;
        if (!checkedMultiply(sourceRaw.width, sourceRaw.height, pixels)
            || !checkedMultiply(pixels, planes, requiredElements)
            || requiredElements > (std::numeric_limits<std::size_t>::max)()
            || requiredElements > (std::numeric_limits<std::size_t>::max)() / sizeof(float)) {
            lastError_ = "CIE element count overflows the process address space";
            return false;
        }
        if (cieElementCount < static_cast<std::size_t>(requiredElements)) {
            lastError_ = "CIE buffer is smaller than the selected color transform requires";
            return false;
        }
        requiredCieBytes = static_cast<std::size_t>(requiredElements) * sizeof(float);
    }

    bool validRange = false;
    if (correctedRaw != nullptr
        && rangesOverlap(sourceRaw.data, requiredRawBytes, correctedRaw->data, requiredRawBytes, validRange)) {
        lastError_ = "Source RAW and corrected RAW buffers must not overlap";
        return false;
    }
    if (!validRange && correctedRaw != nullptr) {
        lastError_ = "RAW buffer address range overflows the process address space";
        return false;
    }
    if (colorItem != nullptr) {
        if (rangesOverlap(sourceRaw.data, requiredRawBytes, cieData, requiredCieBytes, validRange)) {
            lastError_ = "Source RAW and CIE buffers must not overlap";
            return false;
        }
        if (!validRange) {
            lastError_ = "Source RAW or CIE buffer address range overflows the process address space";
            return false;
        }
        if (correctedRaw != nullptr) {
            if (rangesOverlap(correctedRaw->data, requiredRawBytes, cieData, requiredCieBytes, validRange)) {
                lastError_ = "Corrected RAW and CIE buffers must not overlap";
                return false;
            }
            if (!validRange) {
                lastError_ = "Corrected RAW or CIE buffer address range overflows the process address space";
                return false;
            }
        }
    }
    if (correctedRaw == nullptr && colorItem == nullptr) {
        lastError_ = "A corrected RAW or CIE output buffer is required";
        return false;
    }

    if (basicCount == 0) {
        if (correctedRaw != nullptr) {
            std::memcpy(correctedRaw->data, sourceRaw.data, requiredRawBytes);
        }
        if (colorItem != nullptr) {
            std::string error;
            if (!colorItem->apply(sourceRaw, cieData, options, error)) {
                lastError_ = error.empty() ? "Luminance/color calibration failed" : std::move(error);
                return false;
            }
        }
        return true;
    }

    const bool finalSlotMustBeCorrected = correctedRaw != nullptr;
    const bool initialSlotIsCorrected = finalSlotMustBeCorrected && distinctAfterFirst % 2 == 0;
    const bool alternateSlotIsCorrected = finalSlotMustBeCorrected && !initialSlotIsCorrected;
    if (!initialSlotIsCorrected && !ensureScratch(rawScratch_, rawScratchCapacity_, requiredRawBytes)) {
        return false;
    }
    if (!alternateSlotIsCorrected && distinctAfterFirst != 0
        && !ensureScratch(rawScratch2_, rawScratch2Capacity_, requiredRawBytes)) {
        return false;
    }

    ImageView scratch1 = sourceRaw;
    scratch1.data = rawScratch_.get();
    scratch1.dataLength = rawScratchCapacity_;
    ImageView scratch2 = sourceRaw;
    scratch2.data = rawScratch2_.get();
    scratch2.dataLength = rawScratch2Capacity_;
    ImageView initial = initialSlotIsCorrected ? *correctedRaw : scratch1;
    ImageView alternate = alternateSlotIsCorrected ? *correctedRaw : scratch2;

    ImageView current = sourceRaw;
    bool firstBasic = true;
    for (const auto& item : items_) {
        if (item->isColorTransform()) {
            continue;
        }
        std::string error;
        bool applied = false;
        if (firstBasic) {
            if (item->supportsDistinctOutput()) {
                applied = item->applyOutOfPlace(sourceRaw, initial, options, error);
            }
            else {
                std::memcpy(initial.data, sourceRaw.data, requiredRawBytes);
                applied = item->apply(initial, nullptr, options, error);
            }
            if (applied) current = initial;
            firstBasic = false;
        }
        else if (item->requiresDistinctOutput()) {
            const ImageView& destination = current.data == initial.data ? alternate : initial;
            applied = item->applyOutOfPlace(current, destination, options, error);
            if (applied) current = destination;
        }
        else {
            applied = item->apply(current, nullptr, options, error);
        }
        if (!applied) {
            lastError_ = error.empty() ? "Basic calibration failed" : std::move(error);
            return false;
        }
    }

    if (correctedRaw != nullptr && current.data != correctedRaw->data) {
        // Slot parity is planned so this should only be reachable if a future
        // calibration item changes the distinct-output contract.
        std::memcpy(correctedRaw->data, current.data, requiredRawBytes);
        current = *correctedRaw;
    }
    if (colorItem != nullptr) {
        std::string error;
        if (!colorItem->apply(current, cieData, options, error)) {
            lastError_ = error.empty() ? "Luminance/color calibration failed" : std::move(error);
            return false;
        }
    }
    return true;
}

std::string CalibrationContext::lastError() const
{
    std::scoped_lock lock(mutex_);
    return lastError_;
}

void CalibrationContext::recordError(std::string message)
{
    std::scoped_lock lock(mutex_);
    lastError_ = std::move(message);
}

std::size_t CalibrationContext::itemCount() const
{
    std::scoped_lock lock(mutex_);
    return items_.size();
}

bool CalibrationContext::colorTransform(const ExecutionOptions& options, ColorTransform& transform) const
{
    std::scoped_lock lock(mutex_);
    const CalibrationItem* color = nullptr;
    for (const auto& item : items_) {
        if (!item->isColorTransform()) continue;
        if (color != nullptr) return false;
        color = item.get();
    }
    return color != nullptr && color->colorTransform(options, transform);
}

} // namespace cvcore::calibration
