#include "pch.h"
#include "common.h"
#include <Windows.h>
#include <cstring>
#include <string>
#include <vector>

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
