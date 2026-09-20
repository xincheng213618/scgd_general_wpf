#pragma once

#include <opencv2/core.hpp>
#include <nlohmann/json.hpp>

namespace cvcore::distortion {

// Process-local measurement only. Input pixels are borrowed for this call.
// The origin translates ROI-local measured centers to full-image coordinates.
// A rejected measurement has success=false and metrics=null.
nlohmann::json calculateGridDistortionV2(const cv::Mat& image,
    const nlohmann::json& config, const cv::Point& origin = {});

} // namespace cvcore::distortion
