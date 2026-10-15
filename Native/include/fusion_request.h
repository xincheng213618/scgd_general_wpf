#pragma once
#include "fusion_options.h"
#include <nlohmann/json.hpp>
#include <opencv2/imgcodecs.hpp>
#include <filesystem>
#include <fstream>
#include <limits>
#include <string>
#include <vector>

inline bool ParseFusionRequest(const char* text, std::vector<std::string>& files, FusionOptions& options) {
    if (!text) return false;
    const auto request = nlohmann::json::parse(text, nullptr, false);
    if (request.is_discarded()) return false;
    try {
        const auto& paths = request.is_array() ? request : request.at("files");
        files = paths.get<std::vector<std::string>>();
        if (request.is_object() && request.contains("options")) {
            const auto& config = request.at("options");
            if (!config.is_object()) return false;
            for (const auto& item : config.items()) {
                const auto& key = item.key();
                const auto& value = item.value();
                if (key == "focusWindow" || key == "consistencyWindow" || key == "focusStep") {
                    if (!value.is_number_integer()) return false;
                    const auto number = value.get<int64_t>();
                    if (number < 1 || number > 1024) return false;
                    if (key == "focusWindow") options.focusWindow = static_cast<int>(number);
                    else if (key == "consistencyWindow") options.consistencyWindow = static_cast<int>(number);
                    else options.focusStep = static_cast<int>(number);
                }
                else if (key == "alpha" || key == "threshold") {
                    if (!value.is_number()) return false;
                    if (key == "alpha") options.alpha = value.get<float>();
                    else options.threshold = value.get<float>();
                }
                else return false;
            }
        }
        if (files.empty() || !options.valid()) return false;
        for (const auto& path : files) if (path.empty()) return false;
        return true;
    }
    catch (const nlohmann::json::exception&) { return false; }
}

// JSON paths are UTF-8. Windows OpenCV imread uses the active ANSI codepage;
// open through std::filesystem instead so Chinese and other Unicode paths work.
inline cv::Mat ReadFusionImage(const std::string& path) {
#if defined(__cpp_char8_t)
    const std::filesystem::path nativePath(std::u8string(path.begin(), path.end()));
#else
    const std::filesystem::path nativePath = std::filesystem::u8path(path);
#endif
    std::ifstream stream(nativePath, std::ios::binary | std::ios::ate);
    if (!stream) return {};
    const std::streamoff length = stream.tellg();
    if (length <= 0 || length > (std::numeric_limits<int>::max)()) return {};
    std::vector<unsigned char> bytes(static_cast<size_t>(length));
    stream.seekg(0);
    if (!stream.read(reinterpret_cast<char*>(bytes.data()), static_cast<std::streamsize>(length))) return {};
    return cv::imdecode(bytes, cv::IMREAD_UNCHANGED);
}
