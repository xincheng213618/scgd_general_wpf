#include "pch.h"
#include "common.h"
#include <Windows.h>
#include <opencv2/opencv.hpp>
#include <atomic>
#include <cstring>
#include <exception>
#include <string>
#include <vector>

namespace
{
std::atomic<InitialFrame> g_initialFrame{ nullptr };
std::atomic<UpdateFrame> g_updateFrame{ nullptr };

template <typename Func>
int GuardCommonExport(Func func) noexcept
{
    try {
        return func();
    }
    catch (const cv::Exception&) {
        return -4;
    }
    catch (const std::exception&) {
        return -5;
    }
    catch (...) {
        return -6;
    }
}
}

extern "C" COLORVISIONCORE_API void SetInitialFrame(InitialFrame fn)
{
    g_initialFrame.store(fn, std::memory_order_release);
}

extern "C" COLORVISIONCORE_API void SetUpdateFrame(UpdateFrame fn)
{
    g_updateFrame.store(fn, std::memory_order_release);
}

// Optimized UTF8ToGB using RAII and std::vector for automatic memory management
std::string UTF8ToGB(const char* str)
{
    if (!str || strlen(str) == 0) {
        return std::string();
    }

    // Get required buffer size for UTF-8 to WideChar conversion
    int wideCharLen = MultiByteToWideChar(CP_UTF8, 0, str, -1, NULL, 0);
    if (wideCharLen <= 0) {
        return std::string();
    }

    // Use vector for automatic memory management
    std::vector<WCHAR> wideBuffer(wideCharLen);
    if (MultiByteToWideChar(CP_UTF8, 0, str, -1, wideBuffer.data(), wideCharLen) == 0) {
        return std::string();
    }

    // Get required buffer size for WideChar to MultiByte (GB) conversion
    int multiByteLen = WideCharToMultiByte(CP_ACP, 0, wideBuffer.data(), -1, NULL, 0, NULL, NULL);
    if (multiByteLen <= 0) {
        return std::string();
    }

    // Use vector for automatic memory management
    std::vector<char> multiByteBuffer(multiByteLen);
    if (WideCharToMultiByte(CP_ACP, 0, wideBuffer.data(), -1, multiByteBuffer.data(), multiByteLen, NULL, NULL) == 0) {
        return std::string();
    }

    return std::string(multiByteBuffer.data());
}

extern "C" COLORVISIONCORE_API int ReadVideoTest(const char* filePath)
{
    return GuardCommonExport([&]() -> int {
        InitialFrame callback = g_initialFrame.load(std::memory_order_acquire);
        if (filePath == nullptr || filePath[0] == '\0' || callback == nullptr) {
            return -1;
        }

        cv::Mat frame;
        cv::VideoCapture cap = cv::VideoCapture(filePath);

        if (!cap.isOpened()) {
            return -2;
        }

        for (;;) {
            cap >> frame;
            if (frame.empty()) {
                break;
            }
            const int callbackResult = callback(frame.data, frame.rows, frame.cols, frame.channels());
            if (callbackResult != 0) {
                return callbackResult;
            }
            cv::waitKey(30);
        }
        return 0;
        });
}
