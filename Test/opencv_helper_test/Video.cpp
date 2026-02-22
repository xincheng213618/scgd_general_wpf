#include <opencv2/opencv.hpp>
#include <iostream>
#include <chrono>

int main() {
    cv::VideoCapture cap("D:\\3dsvr-1889\\twojav.com@13dsvr01889_1_8k.mp4");
    if (!cap.isOpened()) return -1;

    int total_frames = cap.get(cv::CAP_PROP_FRAME_COUNT);
    double fps = cap.get(cv::CAP_PROP_FPS);
    int width = cap.get(cv::CAP_PROP_FRAME_WIDTH);
    int height = cap.get(cv::CAP_PROP_FRAME_HEIGHT);

    std::cout << "Video: " << width << "x" << height << " @ " << fps << "fps" << std::endl;

    cv::Mat frame;
    bool paused = false;
    int current_pos = 0;

    // 统计变量
    auto last_time = std::chrono::high_resolution_clock::now();
    int frame_count = 0;
    double decode_ms = 0, show_ms = 0, wait_ms = 0;

    while (true) {
        if (!paused) {
            auto t1 = std::chrono::high_resolution_clock::now();
            cap >> frame;
            auto t2 = std::chrono::high_resolution_clock::now();

            if (frame.empty()) break;
            current_pos = cap.get(cv::CAP_PROP_POS_FRAMES);

            // ⚠️ 关键优化：8K 必须缩放，否则 imshow 极慢
            cv::Mat display;
            if (width > 1920) {
                cv::resize(frame, display, cv::Size(1920, 1080), 0, 0, cv::INTER_LINEAR);
            }
            else {
                display = frame;
            }

            auto t3 = std::chrono::high_resolution_clock::now();
            cv::imshow("Video", display);
            auto t4 = std::chrono::high_resolution_clock::now();

            // 计算各环节耗时
            decode_ms = std::chrono::duration<double, std::milli>(t2 - t1).count();
            show_ms = std::chrono::duration<double, std::milli>(t4 - t3).count();
        }

        auto t5 = std::chrono::high_resolution_clock::now();

        // 每秒输出一次统计
        frame_count++;
        auto now = std::chrono::high_resolution_clock::now();
        if (std::chrono::duration<double>(now - last_time).count() >= 1.0) {
            double actual_fps = frame_count / std::chrono::duration<double>(now - last_time).count();
            std::cout << "FPS: " << actual_fps
                << " (理论: " << fps << ")"
                << " | 解码: " << decode_ms << "ms"
                << " | 显示: " << show_ms << "ms"
                << " | 帧队列: " << current_pos << "/" << total_frames
                << std::endl;
            frame_count = 0;
            last_time = now;
        }
    }

cleanup:
    cap.release();
    cv::destroyAllWindows();
    return 0;
}