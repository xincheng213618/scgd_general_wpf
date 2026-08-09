#include "calibration_context.h"

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>

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
        std::string& error)
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
    std::string& error)
{
    std::filesystem::path canonicalFile;
    FileMetadata metadata;
    if (!inspectCalibrationFile(file, canonicalFile, metadata, error)) {
        return CalibrationFileLoadResult::Failure;
    }

    CalibrationFileKey key{ type, normalizedCachePath(canonicalFile) };
    std::shared_ptr<CalibrationFileEntry> entry;
    {
        std::unique_lock lock(calibrationFileCacheMutex);
        const auto existing = calibrationFileCache.find(key);
        if (existing != calibrationFileCache.end()
            && existing->second->metadata == metadata) {
            entry = existing->second;
            entry->ready.wait(lock, [&entry] {
                return !entry->loading.load(std::memory_order_acquire);
            });
            if (entry->canceled
                || entry->releaseEpoch != calibrationFileCacheReleaseEpoch) {
                error = entry->error.empty()
                    ? "Calibration file load was canceled by cache release"
                    : entry->error;
                return CalibrationFileLoadResult::Failure;
            }
            if (entry->prototype) {
                ++entry->hitCount;
                ++calibrationFileCacheHits;
                entry->lastAccessSequence = ++calibrationFileCacheAccessSequence;
                // Reserve ownership while the cache lock is still held so a
                // concurrent release cannot miss an about-to-be-created lease.
                entry->lease->activeOwners.fetch_add(1, std::memory_order_relaxed);
                incrementCacheGeneration();
                evictCalibrationFiles(entry.get());
                const auto prototype = entry->prototype;
                const auto lease = entry->lease;
                lock.unlock();
                result = createContextCalibrationItem(
                    prototype, lease, true, error);
                return result
                    ? CalibrationFileLoadResult::Success
                    : CalibrationFileLoadResult::Failure;
            }
            if (entry->retry.load(std::memory_order_relaxed)) {
                error = entry->error;
                return CalibrationFileLoadResult::Retry;
            }
            if (!entry->error.empty()) {
                error = entry->error;
                return CalibrationFileLoadResult::Failure;
            }
        }

        // A different metadata snapshot supersedes an older entry.  Any load
        // already using the old snapshot continues independently and cannot
        // publish itself over this replacement.
        entry = std::make_shared<CalibrationFileEntry>(
            metadata, std::move(canonicalFile), calibrationFileCacheReleaseEpoch);
        const auto replaced = existing == calibrationFileCache.end()
            ? std::shared_ptr<CalibrationFileEntry>{}
            : existing->second;
        calibrationFileCache.insert_or_assign(key, entry);
        if (replaced) removeCacheAccounting(*replaced);
        ++calibrationFileCacheMisses;
        incrementCacheGeneration();
    }
    PendingCalibrationFileLoad pendingLoad(key, *entry);

    std::string loadError;
    std::unique_ptr<CalibrationItem> loaded;
    if (isColorCalibration(type)) {
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

    FileMetadata metadataAfter;
    std::filesystem::path canonicalAfter;
    std::string inspectionError;
    const bool inspectedAfter = inspectCalibrationFile(
        entry->canonicalFile, canonicalAfter, metadataAfter, inspectionError);
    const bool fileChanged = !inspectedAfter
        || normalizedCachePath(canonicalAfter) != key.file
        || !(metadataAfter == metadata);

    if (fileChanged) {
        error = inspectedAfter
            ? "Calibration file changed while it was being loaded"
            : std::move(inspectionError);
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
    const std::uint64_t estimatedMemoryBytes = (std::max)(
        static_cast<std::uint64_t>(metadata.size),
        loadedShared->cacheFootprintBytes());
    if (!pendingLoad.succeed(loadedShared, estimatedMemoryBytes, error)) {
        return entry->retry.load(std::memory_order_relaxed)
            ? CalibrationFileLoadResult::Retry
            : CalibrationFileLoadResult::Failure;
    }
    result = createContextCalibrationItem(
        loadedShared, entry->lease, true, error);
    return result
        ? CalibrationFileLoadResult::Success
        : CalibrationFileLoadResult::Failure;
}

std::shared_ptr<CalibrationItem> loadCachedCalibrationFile(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error)
{
    // A producer validates metadata again after reading.  Retry a bounded
    // number of times if the file is atomically replaced during that window.
    for (int attempt = 0; attempt < 3; ++attempt) {
        std::shared_ptr<CalibrationItem> item;
        const auto loadResult = loadCalibrationFileOnce(type, file, item, error);
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

bool CalibrationContext::load(CalibrationType type, const std::filesystem::path& file)
{
    std::scoped_lock lock(mutex_);
    lastError_.clear();

    if (!isSupportedCalibration(type)) {
        lastError_ = type == CalibrationType::LumColor
            ? "LumColor is a reserved legacy value and has no calibration algorithm"
            : "Unknown calibration type";
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
    std::shared_ptr<CalibrationItem> item = loadCachedCalibrationFile(
        type, file, error);

    if (!item) {
        lastError_ = error.empty() ? "Calibration type has no loader" : std::move(error);
        return false;
    }

    items_.push_back(std::move(item));
    return true;
}

bool CalibrationContext::execute(
    const ImageView& raw,
    float* cieData,
    std::size_t cieElementCount,
    const ExecutionOptions& options)
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

    if (colorItem != nullptr && cieData == nullptr) {
        lastError_ = "CIE output pointer is null";
        return false;
    }
    std::size_t requiredCieBytes = 0;
    if (colorItem != nullptr) {
        if (!validColorExposure(options, lastError_)) {
            return false;
        }
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
        requiredCieBytes = static_cast<std::size_t>(requiredElements) * sizeof(float);

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

    const bool requiresScratch = std::any_of(
        items_.begin(), items_.end(), [](const auto& item) {
            return !item->isColorTransform() && item->requiresDistinctOutput();
        });
    if (requiresScratch && rawScratchCapacity_ < requiredRawBytes) {
        std::unique_ptr<std::uint8_t[]> replacement(
            new (std::nothrow) std::uint8_t[requiredRawBytes]);
        if (!replacement) {
            lastError_ = "Not enough memory for the shared calibration work buffer";
            return false;
        }
        rawScratch_ = std::move(replacement);
        rawScratchCapacity_ = requiredRawBytes;
    }

    // Preserve LocalCalibrationCacheManager semantics: every basic item runs
    // in template order; the sole RAW-to-CIE transform always runs last.
    ImageView current = raw;
    ImageView scratch = raw;
    scratch.data = rawScratch_.get();
    scratch.dataLength = rawScratchCapacity_;
    for (const auto& item : items_) {
        if (item->isColorTransform()) {
            continue;
        }
        std::string error;
        bool applied = false;
        if (item->requiresDistinctOutput()) {
            const ImageView& destination = current.data == raw.data ? scratch : raw;
            applied = item->applyOutOfPlace(current, destination, options, error);
            if (applied) {
                current = destination;
            }
        }
        else {
            applied = item->apply(current, nullptr, options, error);
        }
        if (!applied) {
            lastError_ = error.empty() ? "Basic calibration failed" : std::move(error);
            return false;
        }
    }

    if (colorItem != nullptr) {
        std::string error;
        if (!colorItem->apply(current, cieData, options, error)) {
            lastError_ = error.empty() ? "Luminance/color calibration failed" : std::move(error);
            return false;
        }
    }
    if (current.data != raw.data) {
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

    auto ensureScratch = [&](std::unique_ptr<std::uint8_t[]>& buffer, std::size_t& capacity) {
        if (capacity >= requiredRawBytes) return true;
        std::unique_ptr<std::uint8_t[]> replacement(new (std::nothrow) std::uint8_t[requiredRawBytes]);
        if (!replacement) {
            lastError_ = "Not enough memory for a calibration work buffer";
            return false;
        }
        buffer = std::move(replacement);
        capacity = requiredRawBytes;
        return true;
    };

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
    if (!initialSlotIsCorrected && !ensureScratch(rawScratch_, rawScratchCapacity_)) {
        return false;
    }
    if (!alternateSlotIsCorrected && distinctAfterFirst != 0
        && !ensureScratch(rawScratch2_, rawScratch2Capacity_)) {
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

} // namespace cvcore::calibration
