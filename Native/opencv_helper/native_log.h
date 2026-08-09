#pragma once

#include <atomic>
#include <string>
#include <utility>

#include "../include/opencv_media_export.h"

namespace cvnative {

enum class LogLevel : int {
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
};

[[nodiscard]] bool ShouldLog(LogLevel level) noexcept;
void Log(LogLevel level, const char* utf8Message) noexcept;
void Log(LogLevel level, const std::string& utf8Message) noexcept;

namespace detail {

// Kept header-only so the disabled-path contract can be unit-tested without
// exporting private C++ implementation symbols from opencv_helper.dll.
class LogGate final {
public:
    [[nodiscard]] bool ShouldLog(LogLevel level) const noexcept
    {
        // Enabled is deliberately the first load. The normal/default path does
        // not inspect the threshold or destinations after finding it disabled.
        if (!enabled_.load(std::memory_order_relaxed)) {
            return false;
        }
        if (static_cast<int>(level) < minimumLevel_.load(std::memory_order_relaxed)) {
            return false;
        }
        return callbackPresent_.load(std::memory_order_acquire)
            || nativeSinkEnabled_.load(std::memory_order_relaxed);
    }

    void SetEnabled(bool enabled) noexcept
    {
        enabled_.store(enabled, std::memory_order_relaxed);
    }

    void SetMinimumLevel(LogLevel level) noexcept
    {
        minimumLevel_.store(static_cast<int>(level), std::memory_order_relaxed);
    }

    void SetCallbackPresent(bool present) noexcept
    {
        callbackPresent_.store(present, std::memory_order_release);
    }

    void SetNativeSinkEnabled(bool enabled) noexcept
    {
        nativeSinkEnabled_.store(enabled, std::memory_order_relaxed);
    }

private:
    std::atomic<bool> enabled_{ false };
    std::atomic<int> minimumLevel_{ static_cast<int>(LogLevel::Info) };
    std::atomic<bool> callbackPresent_{ false };
    std::atomic<bool> nativeSinkEnabled_{ false };
};

class CurrentLogGate final {
public:
    [[nodiscard]] bool ShouldLog(LogLevel level) const noexcept
    {
        return cvnative::ShouldLog(level);
    }
};

template <typename Gate, typename Writer, typename MessageFactory>
inline void DispatchLazy(
    const Gate& gate,
    LogLevel level,
    Writer&& writer,
    MessageFactory&& messageFactory) noexcept
{
    if (!gate.ShouldLog(level)) {
        return;
    }

    try {
        std::forward<Writer>(writer)(
            std::forward<MessageFactory>(messageFactory)());
    }
    catch (...) {
        // Diagnostics must never alter algorithm behavior.
    }
}

} // namespace detail

template <typename MessageFactory>
inline void LogLazy(LogLevel level, MessageFactory&& messageFactory) noexcept
{
    detail::DispatchLazy(
        detail::CurrentLogGate{},
        level,
        [level](const auto& message) noexcept { Log(level, message); },
        std::forward<MessageFactory>(messageFactory));
}

void LogEvent(
    LogLevel level,
    const char* category,
    const char* operation,
    const char* detail = nullptr) noexcept;
void LogFailure(
    LogLevel level,
    const char* category,
    const char* operation,
    int resultCode,
    const char* detail = nullptr) noexcept;
void LogException(
    const char* category,
    const char* operation,
    int resultCode,
    const char* exceptionType,
    const char* detail = nullptr) noexcept;

void SetLogCallback(CVNativeLogCallback callback) noexcept;
void SetLogEnabled(bool enabled) noexcept;
void SetLogLevel(LogLevel level) noexcept;
void EnableNativeSink(bool enabled) noexcept;

} // namespace cvnative
