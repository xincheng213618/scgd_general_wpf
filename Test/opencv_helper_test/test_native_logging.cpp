#include "../../Native/opencv_helper/native_log.h"
#include "../../Native/include/video_export.h"

#include <intrin.h>

#include <algorithm>
#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <limits>
#include <mutex>
#include <stdexcept>
#include <string>
#include <thread>

namespace
{
enum class CallbackMode
{
    Capture,
    Reenter,
    Throw,
};

int g_callbackCount = 0;
int g_callbackSource = -1;
int g_callbackLevel = -1;
std::string g_callbackMessage;
CallbackMode g_callbackMode = CallbackMode::Capture;
std::mutex g_blockingCallbackMutex;
std::condition_variable g_blockingCallbackCondition;
bool g_blockingCallbackEntered = false;
bool g_releaseBlockingCallback = false;
std::atomic<bool> g_blockingCallbackExited = false;

class NativeLogReset final
{
public:
    ~NativeLogReset()
    {
        M_SetLogEnabled(0);
        M_EnableNativeSink(0);
        M_SetLogCallback(nullptr);
    }
};

void ResetCapture(CallbackMode mode = CallbackMode::Capture)
{
    g_callbackCount = 0;
    g_callbackSource = -1;
    g_callbackLevel = -1;
    g_callbackMessage.clear();
    g_callbackMode = mode;
}

void __stdcall CaptureNativeLog(int source, int level, const char* utf8Message)
{
    ++g_callbackCount;
    g_callbackSource = source;
    g_callbackLevel = level;
    g_callbackMessage = utf8Message == nullptr ? std::string() : std::string(utf8Message);

    if (g_callbackMode == CallbackMode::Reenter) {
        HImage ignored{};
        (void)M_Fusion("[]", &ignored);
    }
    else if (g_callbackMode == CallbackMode::Throw) {
        throw std::runtime_error("native callback test exception");
    }
}

void __stdcall BlockingNativeLog(int, int, const char*)
{
    std::unique_lock<std::mutex> lock(g_blockingCallbackMutex);
    g_blockingCallbackEntered = true;
    g_blockingCallbackCondition.notify_all();
    g_blockingCallbackCondition.wait(lock, [] { return g_releaseBlockingCallback; });
    g_blockingCallbackExited.store(true, std::memory_order_release);
}

bool IsValidUtf8(const std::string& value)
{
    const auto* bytes = reinterpret_cast<const unsigned char*>(value.data());
    std::size_t index = 0;
    while (index < value.size()) {
        const unsigned char first = bytes[index];
        if (first <= 0x7f) {
            ++index;
            continue;
        }

        std::size_t continuationCount = 0;
        std::uint32_t codePoint = 0;
        if ((first & 0xe0) == 0xc0) {
            continuationCount = 1;
            codePoint = first & 0x1f;
        }
        else if ((first & 0xf0) == 0xe0) {
            continuationCount = 2;
            codePoint = first & 0x0f;
        }
        else if ((first & 0xf8) == 0xf0) {
            continuationCount = 3;
            codePoint = first & 0x07;
        }
        else {
            return false;
        }

        if (index + continuationCount >= value.size()) {
            return false;
        }
        for (std::size_t offset = 1; offset <= continuationCount; ++offset) {
            const unsigned char next = bytes[index + offset];
            if ((next & 0xc0) != 0x80) {
                return false;
            }
            codePoint = (codePoint << 6) | (next & 0x3f);
        }

        const bool overlong = (continuationCount == 1 && codePoint < 0x80)
            || (continuationCount == 2 && codePoint < 0x800)
            || (continuationCount == 3 && codePoint < 0x10000);
        if (overlong || codePoint > 0x10ffff || (codePoint >= 0xd800 && codePoint <= 0xdfff)) {
            return false;
        }
        index += continuationCount + 1;
    }
    return true;
}

template <typename Action>
double BestNanosecondsPerCall(std::uint64_t iterations, Action&& action)
{
    constexpr int SampleCount = 5;
    double best = (std::numeric_limits<double>::max)();
    for (int sample = 0; sample < SampleCount; ++sample) {
        const auto start = std::chrono::steady_clock::now();
        for (std::uint64_t iteration = 0; iteration < iterations; ++iteration) {
            action();
        }
        const auto elapsed = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::steady_clock::now() - start).count();
        best = (std::min)(best, static_cast<double>(elapsed) / static_cast<double>(iterations));
    }
    return best;
}

bool TestLazyGateAndBenchmark()
{
    cvnative::detail::LogGate gate;
    gate.SetCallbackPresent(true);

    std::uint64_t producerCalls = 0;
    std::uint64_t writerCalls = 0;
    const auto writer = [&](const std::string&) { ++writerCalls; };
    const auto producer = [&]() {
        ++producerCalls;
        return std::string("message requiring allocation");
    };

    cvnative::detail::DispatchLazy(gate, cvnative::LogLevel::Debug, writer, producer);
    if (producerCalls != 0 || writerCalls != 0) {
        return false;
    }

    gate.SetEnabled(true);
    gate.SetMinimumLevel(cvnative::LogLevel::Error);
    cvnative::detail::DispatchLazy(gate, cvnative::LogLevel::Debug, writer, producer);
    if (producerCalls != 0 || writerCalls != 0) {
        return false;
    }

    gate.SetMinimumLevel(cvnative::LogLevel::Trace);
    cvnative::detail::DispatchLazy(
        gate,
        cvnative::LogLevel::Debug,
        [](const std::string&) { throw std::runtime_error("writer failure"); },
        [] { return std::string("writer failure is swallowed"); });

    constexpr std::uint64_t Iterations = 20'000'000;
    gate.SetEnabled(false);
    const double controlNs = BestNanosecondsPerCall(Iterations, [] { _ReadWriteBarrier(); });
    const double disabledNs = BestNanosecondsPerCall(Iterations, [&] {
        cvnative::detail::DispatchLazy(gate, cvnative::LogLevel::Debug, writer, producer);
        });

    gate.SetEnabled(true);
    gate.SetMinimumLevel(cvnative::LogLevel::Error);
    const double filteredNs = BestNanosecondsPerCall(Iterations, [&] {
        cvnative::detail::DispatchLazy(gate, cvnative::LogLevel::Debug, writer, producer);
        });

    std::cout << "native_log_benchmark,iterations," << Iterations << std::endl;
    std::cout << "native_log_benchmark,control_ns_per_call," << controlNs << std::endl;
    std::cout << "native_log_benchmark,disabled_ns_per_call," << disabledNs << std::endl;
    std::cout << "native_log_benchmark,disabled_delta_ns_per_call," << (disabledNs - controlNs) << std::endl;
    std::cout << "native_log_benchmark,level_filtered_ns_per_call," << filteredNs << std::endl;
    std::cout << "native_log_benchmark,level_filtered_delta_ns_per_call," << (filteredNs - controlNs) << std::endl;

    return producerCalls == 0 && writerCalls == 0;
}

bool InvokeFusionFailure()
{
    HImage output{};
    const int result = M_Fusion("[]", &output);
    return result == -1 && output.pData == nullptr;
}

template <typename Action>
bool ExpectBoundaryLog(Action&& action, int expectedResult, cvnative::LogLevel expectedLevel, const char* operation)
{
    ResetCapture();
    return action() == expectedResult
        && g_callbackCount == 1
        && g_callbackLevel == static_cast<int>(expectedLevel)
        && g_callbackMessage.find(operation) != std::string::npos
        && IsValidUtf8(g_callbackMessage);
}

bool TestRealDllCallbackContract()
{
    NativeLogReset resetOnExit;
    M_EnableNativeSink(0);
    M_SetLogCallback(CaptureNativeLog);
    M_SetLogLevel(static_cast<int>(cvnative::LogLevel::Trace));
    M_SetLogEnabled(0);

    ResetCapture();
    if (!InvokeFusionFailure() || g_callbackCount != 0) {
        return false;
    }

    M_SetLogEnabled(1);
    M_SetLogLevel(static_cast<int>(cvnative::LogLevel::Error));
    ResetCapture();
    if (!InvokeFusionFailure() || g_callbackCount != 0) {
        return false;
    }

    M_SetLogLevel(static_cast<int>(cvnative::LogLevel::Debug));
    ResetCapture();
    if (!InvokeFusionFailure()
        || g_callbackCount != 1
        || g_callbackSource != 1
        || g_callbackLevel != static_cast<int>(cvnative::LogLevel::Debug)
        || g_callbackMessage.find("M_Fusion") == std::string::npos
        || !IsValidUtf8(g_callbackMessage)) {
        return false;
    }

    if (!ExpectBoundaryLog(
        [] { return M_CalculatePoiBatchV1(0, 0, 32, 1, nullptr, 0, nullptr, 0, nullptr); },
        M_POI_INVALID_ARGUMENT,
        cvnative::LogLevel::Debug,
        "M_CalculatePoiBatchV1")) {
        return false;
    }

    if (!ExpectBoundaryLog(
        [] {
            double frequency = 0.0;
            double sfr = 0.0;
            double mtf10Norm = 0.0;
            double mtf50Norm = 0.0;
            double mtf10CyPix = 0.0;
            double mtf50CyPix = 0.0;
            int outputLength = 0;
            return M_CalSFR(
                HImage{}, 1.0, RoiRect{}, &frequency, &sfr, 1, &outputLength,
                &mtf10Norm, &mtf50Norm, &mtf10CyPix, &mtf50CyPix);
        },
        -2,
        cvnative::LogLevel::Debug,
        "M_CalSFR")) {
        return false;
    }

    if (!ExpectBoundaryLog(
        [] {
            char* result = nullptr;
            return M_DetectGhosts(HImage{}, RoiRect{}, nullptr, &result);
        },
        -1,
        cvnative::LogLevel::Debug,
        "M_DetectGhosts")) {
        return false;
    }

    if (!ExpectBoundaryLog(
        [] { return M_CalibrationCreate(nullptr); },
        M_CALIBRATION_INVALID_ARGUMENT,
        cvnative::LogLevel::Debug,
        "M_CalibrationCreate")) {
        return false;
    }

    if (!ExpectBoundaryLog(
        [] {
            VideoInfo info{};
            return M_VideoOpen(nullptr, &info);
        },
        -1,
        cvnative::LogLevel::Warn,
        "M_VideoOpen")) {
        return false;
    }

    ResetCapture(CallbackMode::Reenter);
    if (!InvokeFusionFailure() || g_callbackCount != 1) {
        return false;
    }

    ResetCapture(CallbackMode::Throw);
    if (!InvokeFusionFailure() || g_callbackCount != 1) {
        return false;
    }

    ResetCapture();
    return true;
}

bool TestCallbackUnregisterIsQuiescent()
{
    NativeLogReset resetOnExit;
    {
        std::lock_guard<std::mutex> lock(g_blockingCallbackMutex);
        g_blockingCallbackEntered = false;
        g_releaseBlockingCallback = false;
        g_blockingCallbackExited.store(false, std::memory_order_relaxed);
    }

    M_EnableNativeSink(0);
    M_SetLogCallback(BlockingNativeLog);
    M_SetLogLevel(static_cast<int>(cvnative::LogLevel::Debug));
    M_SetLogEnabled(1);

    bool producerResult = false;
    std::thread producer([&] { producerResult = InvokeFusionFailure(); });
    {
        std::unique_lock<std::mutex> lock(g_blockingCallbackMutex);
        if (!g_blockingCallbackCondition.wait_for(
            lock,
            std::chrono::seconds(2),
            [] { return g_blockingCallbackEntered; })) {
            g_releaseBlockingCallback = true;
            lock.unlock();
            g_blockingCallbackCondition.notify_all();
            producer.join();
            return false;
        }
    }

    std::thread releaser([] {
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
        {
            std::lock_guard<std::mutex> lock(g_blockingCallbackMutex);
            g_releaseBlockingCallback = true;
        }
        g_blockingCallbackCondition.notify_all();
        });

    M_SetLogCallback(nullptr);
    const bool callbackExitedBeforeReturn = g_blockingCallbackExited.load(std::memory_order_acquire);

    releaser.join();
    producer.join();
    return producerResult && callbackExitedBeforeReturn;
}

bool BenchmarkRealExportPath()
{
    NativeLogReset resetOnExit;
    unsigned char pixel = 37;
    HImage image{};
    image.rows = 1;
    image.cols = 1;
    image.channels = 1;
    image.depth = 8;
    image.stride = 1;
    image.pData = &pixel;

    constexpr std::uint64_t Iterations = 1'000'000;
    std::uint64_t checksum = 0;
    auto invoke = [&] {
        unsigned int minimum = 0;
        unsigned int maximum = 0;
        if (M_GetMinMax(image, &minimum, &maximum, -1) == 0) {
            checksum += minimum + maximum;
        }
    };

    M_EnableNativeSink(0);
    M_SetLogCallback(CaptureNativeLog);
    M_SetLogLevel(static_cast<int>(cvnative::LogLevel::Debug));
    M_SetLogEnabled(0);
    const double disabledNs = BestNanosecondsPerCall(Iterations, invoke);

    ResetCapture();
    M_SetLogEnabled(1);
    const double enabledNs = BestNanosecondsPerCall(Iterations, invoke);
    const int unexpectedCallbacks = g_callbackCount;

    std::cout << "native_log_export_benchmark,operation,M_GetMinMax_1x1_success" << std::endl;
    std::cout << "native_log_export_benchmark,iterations," << Iterations << std::endl;
    std::cout << "native_log_export_benchmark,disabled_ns_per_call," << disabledNs << std::endl;
    std::cout << "native_log_export_benchmark,enabled_debug_ns_per_call," << enabledNs << std::endl;
    std::cout << "native_log_export_benchmark,enabled_to_disabled_ratio," << (enabledNs / disabledNs) << std::endl;
    std::cout << "native_log_export_benchmark,checksum," << checksum << std::endl;

    return checksum != 0 && unexpectedCallbacks == 0;
}
}

bool RunNativeLoggingTests()
{
    const bool lazyGate = TestLazyGateAndBenchmark();
    const bool callbackContract = TestRealDllCallbackContract();
    const bool quiescentUnregister = TestCallbackUnregisterIsQuiescent();
    const bool exportBenchmark = BenchmarkRealExportPath();
    if (!lazyGate) {
        std::cerr << "Native logging lazy-gate test failed" << std::endl;
    }
    if (!callbackContract) {
        std::cerr << "Native logging callback contract test failed" << std::endl;
    }
    if (!quiescentUnregister) {
        std::cerr << "Native logging callback unregister was not quiescent" << std::endl;
    }
    if (!exportBenchmark) {
        std::cerr << "Native logging real-export benchmark validation failed" << std::endl;
    }
    return lazyGate && callbackContract && quiescentUnregister && exportBenchmark;
}
