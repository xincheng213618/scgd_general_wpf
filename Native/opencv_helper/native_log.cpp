#include "pch.h"
#include "native_log.h"

#include "spdlog/spdlog.h"

#include <atomic>
#include <mutex>

namespace {
constexpr int kHelperSource = 1;
cvnative::detail::LogGate g_gate;
std::atomic<bool> g_nativeSink(false);
std::atomic<CVNativeLogCallback> g_callback(nullptr);
std::atomic<unsigned int> g_activeCallbacks(0);
std::mutex g_callbackRegistrationMutex;
thread_local bool t_dispatchingLog = false;

class DispatchScope final {
public:
    DispatchScope() noexcept
    {
        t_dispatchingLog = true;
    }

    ~DispatchScope()
    {
        t_dispatchingLog = false;
    }
};

class CallbackActivity final {
public:
    CallbackActivity() noexcept
    {
        g_activeCallbacks.fetch_add(1, std::memory_order_seq_cst);
    }

    ~CallbackActivity()
    {
        if (g_activeCallbacks.fetch_sub(1, std::memory_order_seq_cst) == 1) {
            g_activeCallbacks.notify_all();
        }
    }
};

void DrainCallbacks() noexcept
{
    // A callback cannot wait for itself. Managed callers keep the delegate
    // rooted for process lifetime, and normal registration changes occur from
    // outside the callback.
    if (t_dispatchingLog) {
        return;
    }

    unsigned int active = g_activeCallbacks.load(std::memory_order_seq_cst);
    while (active != 0) {
        g_activeCallbacks.wait(active, std::memory_order_seq_cst);
        active = g_activeCallbacks.load(std::memory_order_seq_cst);
    }
}

const char* SafeText(const char* value) noexcept
{
    return value != nullptr ? value : "";
}

void AppendField(std::string& message, const char* name, const char* value)
{
    if (value == nullptr || value[0] == '\0') {
        return;
    }
    message.push_back(' ');
    message.append(name);
    message.push_back('=');
    for (const char* current = value; *current != '\0'; ++current) {
        const char character = *current;
        message.push_back(character == '\r' || character == '\n' || character == '\t' ? ' ' : character);
    }
}

void WriteNativeSink(cvnative::LogLevel level, const char* utf8Message) noexcept
{
    try {
        switch (level) {
        case cvnative::LogLevel::Trace:
            spdlog::log(spdlog::level::trace, "{}", utf8Message);
            break;
        case cvnative::LogLevel::Debug:
            spdlog::log(spdlog::level::debug, "{}", utf8Message);
            break;
        case cvnative::LogLevel::Info:
            spdlog::log(spdlog::level::info, "{}", utf8Message);
            break;
        case cvnative::LogLevel::Warn:
            spdlog::log(spdlog::level::warn, "{}", utf8Message);
            break;
        case cvnative::LogLevel::Error:
            spdlog::log(spdlog::level::err, "{}", utf8Message);
            break;
        default:
            spdlog::log(spdlog::level::info, "{}", utf8Message);
            break;
        }
    }
    catch (...) {
        // A missing/misconfigured native sink must not escape the DLL boundary.
    }
}

} // namespace

namespace cvnative {

bool ShouldLog(LogLevel level) noexcept
{
    if (!g_gate.ShouldLog(level)) {
        return false;
    }
    return !t_dispatchingLog;
}

void Log(LogLevel level, const char* utf8Message) noexcept
{
    if (utf8Message == nullptr || !ShouldLog(level) || t_dispatchingLog) {
        return;
    }

    DispatchScope dispatchScope;

    if (g_callback.load(std::memory_order_seq_cst) != nullptr) {
        // Increment before the authoritative pointer load. Once unregister
        // returns, any later activity can only observe a null/new callback.
        CallbackActivity callbackActivity;
        CVNativeLogCallback callback = g_callback.load(std::memory_order_seq_cst);
        if (callback != nullptr) {
            try {
                // utf8Message is borrowed and valid only for this synchronous call.
                callback(kHelperSource, static_cast<int>(level), utf8Message);
            }
            catch (...) {
                // Includes native test callbacks. Managed callbacks are also
                // expected to catch internally before returning across the ABI.
            }
        }
    }

    if (g_nativeSink.load(std::memory_order_relaxed)) {
        WriteNativeSink(level, utf8Message);
    }
}

void Log(LogLevel level, const std::string& utf8Message) noexcept
{
    Log(level, utf8Message.c_str());
}

void LogEvent(
    LogLevel level,
    const char* category,
    const char* operation,
    const char* detail) noexcept
{
    LogLazy(level, [=] {
        std::string message;
        message.reserve(96);
        message.push_back('[');
        message.append(SafeText(category));
        message.append("] ");
        message.append(SafeText(operation));
        AppendField(message, "detail", detail);
        return message;
        });
}

void LogFailure(
    LogLevel level,
    const char* category,
    const char* operation,
    int resultCode,
    const char* detail) noexcept
{
    LogLazy(level, [=] {
        std::string message;
        message.reserve(128);
        message.push_back('[');
        message.append(SafeText(category));
        message.append("] ");
        message.append(SafeText(operation));
        message.append(" failed result=");
        message.append(std::to_string(resultCode));
        AppendField(message, "detail", detail);
        return message;
        });
}

void LogException(
    const char* category,
    const char* operation,
    int resultCode,
    const char* exceptionType,
    const char* detail) noexcept
{
    LogLazy(LogLevel::Error, [=] {
        std::string message;
        message.reserve(192);
        message.push_back('[');
        message.append(SafeText(category));
        message.append("] ");
        message.append(SafeText(operation));
        message.append(" failed result=");
        message.append(std::to_string(resultCode));
        AppendField(message, "exception", exceptionType);
        AppendField(message, "message", detail);
        return message;
        });
}

void SetLogCallback(CVNativeLogCallback callback) noexcept
{
    try {
        std::lock_guard<std::mutex> registrationLock(g_callbackRegistrationMutex);

        // Stop new callback producers, detach the old pointer, then wait for any
        // callback that already acquired it. This makes unregister/replacement
        // quiescent for callers outside the callback itself.
        g_gate.SetCallbackPresent(false);
        g_callback.store(nullptr, std::memory_order_seq_cst);
        DrainCallbacks();

        if (callback != nullptr) {
            g_callback.store(callback, std::memory_order_seq_cst);
            g_gate.SetCallbackPresent(true);
        }
    }
    catch (...) {
        // A registration failure leaves callback delivery safely disabled.
        g_gate.SetCallbackPresent(false);
        g_callback.store(nullptr, std::memory_order_seq_cst);
    }
}

void SetLogEnabled(bool enabled) noexcept
{
    g_gate.SetEnabled(enabled);
}

void SetLogLevel(LogLevel level) noexcept
{
    g_gate.SetMinimumLevel(level);
}

void EnableNativeSink(bool enabled) noexcept
{
    g_nativeSink.store(enabled, std::memory_order_relaxed);
    g_gate.SetNativeSinkEnabled(enabled);
}

} // namespace cvnative

extern "C" COLORVISIONCORE_API void __cdecl M_SetLogCallback(CVNativeLogCallback callback)
{
    cvnative::SetLogCallback(callback);
}

extern "C" COLORVISIONCORE_API void __cdecl M_SetLogEnabled(int enabled)
{
    cvnative::SetLogEnabled(enabled != 0);
}

extern "C" COLORVISIONCORE_API void __cdecl M_SetLogLevel(int level)
{
    if (level < static_cast<int>(cvnative::LogLevel::Trace)) {
        level = static_cast<int>(cvnative::LogLevel::Trace);
    }
    if (level > static_cast<int>(cvnative::LogLevel::Error)) {
        level = static_cast<int>(cvnative::LogLevel::Error);
    }
    cvnative::SetLogLevel(static_cast<cvnative::LogLevel>(level));
}

extern "C" COLORVISIONCORE_API void __cdecl M_EnableNativeSink(int enabled)
{
    cvnative::EnableNativeSink(enabled != 0);
}
