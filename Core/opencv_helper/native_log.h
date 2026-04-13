#pragma once

#include <string>
#include "opencv_media_export.h"

namespace cvnative {

enum class LogLevel : int {
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
};

void Log(LogLevel level, const std::string& message);
void SetLogCallback(CVNativeLogCallback callback);
void SetLogEnabled(bool enabled);
void SetLogLevel(LogLevel level);
void EnableNativeSink(bool enabled);

} // namespace cvnative
