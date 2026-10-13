#pragma once

#include <cmath>

// Shared by CPU and CUDA. The legacy JSON array remains a default-settings request.
struct FusionOptions {
    int focusWindow = 5;
    int consistencyWindow = 3;
    int focusStep = 2;
    float alpha = 0.2f;
    float threshold = 13.0f;

    bool valid() const {
        return focusWindow >= 3 && focusWindow <= 31 && (focusWindow & 1)
            && consistencyWindow >= 3 && consistencyWindow <= 31 && (consistencyWindow & 1)
            && focusStep >= 1 && focusStep <= 1024
            && std::isfinite(alpha) && alpha >= 0.01f && alpha <= 1.0f
            && std::isfinite(threshold) && threshold >= 0.0f && threshold <= 60.0f;
    }
};
