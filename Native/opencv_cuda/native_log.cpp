#include "pch.h"
#include "native_log.h"

#include "spdlog/spdlog.h"

#include <atomic>

namespace {
constexpr int kCudaSource = 2;
std::atomic<int> g_level(static_cast<int>(cvnative::LogLevel::Info));
std::atomic<bool> g_enabled(false);
std::atomic<bool> g_nativeSink(false);
std::atomic<CVNativeLogCallback> g_callback(nullptr);
}

namespace cvnative {

void Log(LogLevel level, const std::string& message)
{
    if (!g_enabled.load(std::memory_order_relaxed)) {
        return;
    }
    if (static_cast<int>(level) < g_level.load(std::memory_order_relaxed)) {
        return;
    }

    CVNativeLogCallback callback = g_callback.load(std::memory_order_relaxed);
    if (callback != nullptr) {
        callback(kCudaSource, static_cast<int>(level), message.c_str());
    }

    if (!g_nativeSink.load(std::memory_order_relaxed)) {
        return;
    }

    switch (level) {
    case LogLevel::Trace:
        spdlog::trace(message);
        break;
    case LogLevel::Debug:
        spdlog::debug(message);
        break;
    case LogLevel::Info:
        spdlog::info(message);
        break;
    case LogLevel::Warn:
        spdlog::warn(message);
        break;
    case LogLevel::Error:
        spdlog::error(message);
        break;
    default:
        spdlog::info(message);
        break;
    }
}

void SetLogCallback(CVNativeLogCallback callback)
{
    g_callback.store(callback, std::memory_order_relaxed);
}

void SetLogEnabled(bool enabled)
{
    g_enabled.store(enabled, std::memory_order_relaxed);
}

void SetLogLevel(LogLevel level)
{
    g_level.store(static_cast<int>(level), std::memory_order_relaxed);
}

void EnableNativeSink(bool enabled)
{
    g_nativeSink.store(enabled, std::memory_order_relaxed);
}

} // namespace cvnative

extern "C" COLORVISIONCORE_API void CM_SetLogCallback(CVNativeLogCallback callback)
{
    cvnative::SetLogCallback(callback);
}

extern "C" COLORVISIONCORE_API void CM_SetLogEnabled(int enabled)
{
    cvnative::SetLogEnabled(enabled != 0);
}

extern "C" COLORVISIONCORE_API void CM_SetLogLevel(int level)
{
    if (level < static_cast<int>(cvnative::LogLevel::Trace)) {
        level = static_cast<int>(cvnative::LogLevel::Trace);
    }
    if (level > static_cast<int>(cvnative::LogLevel::Error)) {
        level = static_cast<int>(cvnative::LogLevel::Error);
    }
    cvnative::SetLogLevel(static_cast<cvnative::LogLevel>(level));
}

extern "C" COLORVISIONCORE_API void CM_EnableNativeSink(int enabled)
{
    cvnative::EnableNativeSink(enabled != 0);
}
