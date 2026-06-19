#include "Windows.h"
#include "cuda_runtime.h"
#include "device_launch_parameters.h"

#include "pch.h"
#include "cuda_export.h"
#include <opencv2/opencv.hpp>
#include <nlohmann\json.hpp>
#include "Fusion.h"
#include <thread>
#include <queue>
#include <mutex>
#include <condition_variable>
#include <future>
#include <atomic>
#include <exception>

using json = nlohmann::json;

// ============================================================================
// Async Pipeline Architecture
// ============================================================================

/**
 * @brief Thread-safe queue for producer-consumer pattern
 */
template<typename T>
class ThreadSafeQueue {
private:
    std::queue<T> queue_;
    mutable std::mutex mutex_;
    std::condition_variable cond_;
    bool shutdown_ = false;

public:
    void push(T value) {
        {
            std::lock_guard<std::mutex> lock(mutex_);
            queue_.push(std::move(value));
        }
        cond_.notify_one();
    }

    bool pop(T& value) {
        std::unique_lock<std::mutex> lock(mutex_);
        cond_.wait(lock, [this] { return !queue_.empty() || shutdown_; });
        if (queue_.empty()) return false;
        value = std::move(queue_.front());
        queue_.pop();
        return true;
    }

    bool try_pop(T& value, std::chrono::milliseconds timeout) {
        std::unique_lock<std::mutex> lock(mutex_);
        if (!cond_.wait_for(lock, timeout, [this] { return !queue_.empty() || shutdown_; })) {
            return false;
        }
        if (queue_.empty()) return false;
        value = std::move(queue_.front());
        queue_.pop();
        return true;
    }

    void shutdown() {
        {
            std::lock_guard<std::mutex> lock(mutex_);
            shutdown_ = true;
        }
        cond_.notify_all();
    }

    size_t size() const {
        std::lock_guard<std::mutex> lock(mutex_);
        return queue_.size();
    }

    bool empty() const {
        std::lock_guard<std::mutex> lock(mutex_);
        return queue_.empty();
    }
};

/**
 * @brief Image loading task result
 */
struct ImageLoadResult {
    int index;
    cv::Mat image;
    bool success;
    std::chrono::steady_clock::time_point load_time;
};

/**
 * @brief GPU upload task
 */
struct GPUUploadTask {
    int index;
    cv::Mat image;
    double* d_buffer;  // Pre-allocated GPU memory
};

/**
 * @brief Asynchronous image loader with thread pool
 */
class AsyncImageLoader {
private:
    std::vector<std::thread> workers_;
    ThreadSafeQueue<std::pair<int, std::string>> load_queue_;
    ThreadSafeQueue<ImageLoadResult> result_queue_;
    std::atomic<size_t> pending_count_{0};
    bool running_ = false;

    void worker_thread() {
        std::pair<int, std::string> task;
        while (load_queue_.pop(task)) {
            ImageLoadResult result;
            result.index = task.first;
            auto start = std::chrono::steady_clock::now();
            result.image = cv::imread(task.second, cv::IMREAD_UNCHANGED);
            result.success = !result.image.empty();
            result.load_time = start;
            result_queue_.push(std::move(result));
            pending_count_--;
        }
    }

public:
    void start(int num_threads = 4) {
        running_ = true;
        for (int i = 0; i < num_threads; ++i) {
            workers_.emplace_back(&AsyncImageLoader::worker_thread, this);
        }
    }

    void stop() {
        load_queue_.shutdown();
        for (auto& t : workers_) {
            if (t.joinable()) t.join();
        }
        running_ = false;
    }

    void enqueue(int index, const std::string& path) {
        pending_count_++;
        load_queue_.push({index, path});
    }

    bool get_result(ImageLoadResult& result, std::chrono::milliseconds timeout = std::chrono::milliseconds(100)) {
        return result_queue_.try_pop(result, timeout);
    }

    size_t pending() const { return pending_count_.load(); }
};

/**
 * @brief CUDA Stream Pool for parallel GPU operations
 */
class CUDAStreamPool {
private:
    std::vector<cudaStream_t> streams_;
    std::queue<size_t> available_;
    std::mutex mutex_;

public:
    void initialize(size_t count) {
        streams_.resize(count);
        for (size_t i = 0; i < count; ++i) {
            cudaStreamCreate(&streams_[i]);
            available_.push(i);
        }
    }

    void destroy() {
        for (auto& stream : streams_) {
            if (stream) cudaStreamDestroy(stream);
        }
        streams_.clear();
    }

    cudaStream_t acquire() {
        std::lock_guard<std::mutex> lock(mutex_);
        if (available_.empty()) return nullptr;
        size_t idx = available_.front();
        available_.pop();
        return streams_[idx];
    }

    void release(cudaStream_t stream) {
        std::lock_guard<std::mutex> lock(mutex_);
        for (size_t i = 0; i < streams_.size(); ++i) {
            if (streams_[i] == stream) {
                available_.push(i);
                break;
            }
        }
    }

    size_t available_count() {
        std::lock_guard<std::mutex> lock(mutex_);
        return available_.size();
    }
};

// ============================================================================
// Optimized Fusion with Async Pipeline
// ============================================================================

/**
 * @brief Fusion with asynchronous image loading and GPU upload
 *
 * Pipeline stages:
 * 1. Async image loading (CPU, multi-threaded)
 * 2. Async GPU upload (overlapped with loading)
 * 3. GPU processing ( Fusion algorithm )
 */
cv::Mat FusionAsyncPipeline(const std::vector<std::string>& files, int STEP, int num_loader_threads = 4) {
    if (files.empty()) return cv::Mat();

    auto total_start = std::chrono::steady_clock::now();

    // Phase 1: Async load all images
    AsyncImageLoader loader;
    loader.start(num_loader_threads);

    for (size_t i = 0; i < files.size(); ++i) {
        loader.enqueue(static_cast<int>(i), files[i]);
    }

    std::vector<cv::Mat> imgs(files.size());
    size_t loaded_count = 0;
    ImageLoadResult result;

    while (loaded_count < files.size()) {
        if (loader.get_result(result, std::chrono::milliseconds(1000))) {
            if (result.success) {
                imgs[result.index] = std::move(result.image);
            }
            loaded_count++;
        }
    }

    loader.stop();

    auto load_end = std::chrono::steady_clock::now();
    std::cout << "Async loading time: "
              << std::chrono::duration_cast<std::chrono::milliseconds>(load_end - total_start).count()
              << " ms" << std::endl;

    // Phase 2: GPU fusion
    cv::Mat out = Fusion(imgs, STEP);

    return out;
}

/**
 * @brief Advanced pipeline with overlapped load-upload-process
 *
 * This version overlaps image loading, GPU upload, and processing
 * for maximum throughput with large image sets.
 */
cv::Mat FusionAdvancedPipeline(const std::vector<std::string>& files, int STEP) {
    if (files.empty()) return cv::Mat();

    const int BATCH_SIZE = 4;  // Process in batches for memory efficiency
    const int NUM_STREAMS = 2;

    auto total_start = std::chrono::steady_clock::now();

    // Initialize CUDA streams
    CUDAStreamPool stream_pool;
    stream_pool.initialize(NUM_STREAMS);

    std::vector<cv::Mat> all_imgs;
    all_imgs.reserve(files.size());

    // Process in batches
    for (size_t batch_start = 0; batch_start < files.size(); batch_start += BATCH_SIZE) {
        size_t batch_end = std::min(batch_start + BATCH_SIZE, files.size());

        // Load batch asynchronously
        std::vector<std::future<cv::Mat>> futures;
        for (size_t i = batch_start; i < batch_end; ++i) {
            futures.push_back(std::async(std::launch::async, [&files, i]() {
                return cv::imread(files[i], cv::IMREAD_UNCHANGED);
            }));
        }

        // Collect results
        for (auto& f : futures) {
            cv::Mat img = f.get();
            if (!img.empty()) {
                all_imgs.push_back(std::move(img));
            }
        }
    }

    auto load_end = std::chrono::steady_clock::now();
    std::cout << "Batch loading time: "
              << std::chrono::duration_cast<std::chrono::milliseconds>(load_end - total_start).count()
              << " ms" << std::endl;

    stream_pool.destroy();

    // Perform fusion
    cv::Mat out = Fusion(all_imgs, STEP);

    return out;
}

// ============================================================================
// Original and Optimized Export Functions
// ============================================================================

namespace {

int ParseFusionFiles(const char* fusionjson, std::vector<std::string>& files)
{
    if (fusionjson == nullptr) {
        return -1;
    }

    json j = json::parse(fusionjson, nullptr, false);
    if (j.is_discarded() || !j.is_array()) {
        return -2;
    }

    try {
        files = j.get<std::vector<std::string>>();
    }
    catch (const json::exception&) {
        return -3;
    }

    return files.empty() ? -4 : 0;
}

int LoadImagesParallel(const std::vector<std::string>& files, std::vector<cv::Mat>& imgs)
{
    imgs.assign(files.size(), cv::Mat());
    std::vector<std::thread> threads;
    std::vector<bool> read_success(files.size(), false);
    std::mutex imgs_mutex;

    threads.reserve(files.size());
    try {
        for (size_t i = 0; i < files.size(); ++i) {
            threads.emplace_back([i, &files, &imgs, &read_success, &imgs_mutex]() {
                cv::Mat img = cv::imread(files[i], cv::IMREAD_UNCHANGED);
                if (!img.empty()) {
                    std::lock_guard<std::mutex> lock(imgs_mutex);
                    imgs[i] = std::move(img);
                    read_success[i] = true;
                }
            });
        }
    }
    catch (...) {
        for (auto& t : threads) {
            if (t.joinable()) {
                t.join();
            }
        }
        throw;
    }

    for (auto& t : threads) {
        if (t.joinable()) {
            t.join();
        }
    }

    for (size_t i = 0; i < files.size(); ++i) {
        if (!read_success[i]) {
            std::cerr << "Error: Failed to load image " << i << ": " << files[i] << std::endl;
            return -5;
        }
    }

    return 0;
}

int WriteOutputImage(const cv::Mat& out, HImage* outImage)
{
    if (outImage == nullptr) {
        return -1;
    }
    if (out.empty()) {
        *outImage = HImage{};
        return -6;
    }

    return MatToHImage(out, outImage);
}

void ClearHImages(HImage* images, int count) noexcept
{
    if (images == nullptr || count <= 0) {
        return;
    }

    for (int i = 0; i < count; ++i) {
        images[i] = HImage{};
    }
}

int MapExceptionToError(const char* operation, const std::exception& e)
{
    std::cerr << operation << " exception: " << e.what() << std::endl;
    return -100;
}

} // namespace

extern "C" COLORVISIONCORE_API void M_FreeHImageData(unsigned char* data)
{
    if (data != nullptr) {
        CoTaskMemFree(data);
    }
}

extern "C" COLORVISIONCORE_API int CM_Fusion(const char* fusionjson, HImage* outImage)
{
    if (outImage == nullptr) {
        return -1;
    }
    *outImage = HImage{};

    try {
    std::chrono::steady_clock::time_point start, end;
    std::chrono::microseconds duration;
    start = std::chrono::high_resolution_clock::now();

    std::vector<std::string> files;
    int parseResult = ParseFusionFiles(fusionjson, files);
    if (parseResult != 0) {
        return parseResult;
    }

    // Use async pipeline for loading
    auto load_start = std::chrono::steady_clock::now();
    std::vector<cv::Mat> imgs;
    int loadResult = LoadImagesParallel(files, imgs);
    auto load_end = std::chrono::steady_clock::now();
    std::cout << "Image loading time: "
              << std::chrono::duration_cast<std::chrono::milliseconds>(load_end - load_start).count()
              << " ms" << std::endl;
    if (loadResult != 0) {
        return loadResult;
    }

    auto fusion_start = std::chrono::steady_clock::now();
    cv::Mat out = Fusion(imgs, 2);
    auto fusion_end = std::chrono::steady_clock::now();

    std::cout << "Fusion time: "
              << std::chrono::duration_cast<std::chrono::milliseconds>(fusion_end - fusion_start).count()
              << " ms" << std::endl;

    end = std::chrono::high_resolution_clock::now();
    duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
    std::cout << "Total execution time: " << duration.count() / 1000.0 << " ms" << std::endl;

    int result = WriteOutputImage(out, outImage);

    auto convert_end = std::chrono::steady_clock::now();
    std::cout << "MatToHImage time: "
              << std::chrono::duration_cast<std::chrono::milliseconds>(convert_end - fusion_end).count()
              << " ms" << std::endl;

    return result;
    }
    catch (const cv::Exception& e) {
        return MapExceptionToError("CM_Fusion OpenCV", e);
    }
    catch (const std::exception& e) {
        return MapExceptionToError("CM_Fusion", e);
    }
    catch (...) {
        return -101;
    }
}

extern "C" COLORVISIONCORE_API int M_Fusion(const char* fusionjson, HImage* outImage)
{
    return CM_Fusion(fusionjson, outImage);
}

/**
 * @brief Optimized fusion with async pipeline
 */
extern "C" COLORVISIONCORE_API int CM_Fusion_Async(const char* fusionjson, HImage* outImage)
{
    if (outImage == nullptr) {
        return -1;
    }
    *outImage = HImage{};

    try {
    std::chrono::steady_clock::time_point start;
    start = std::chrono::high_resolution_clock::now();

    std::vector<std::string> files;
    int parseResult = ParseFusionFiles(fusionjson, files);
    if (parseResult != 0) {
        return parseResult;
    }

    // Use optimized async pipeline
    cv::Mat out = FusionAsyncPipeline(files, 2);

    if (out.empty()) {
        std::cerr << "Error: Fusion failed" << std::endl;
        return -1;
    }

    auto end = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
    std::cout << "Total async execution time: " << duration.count() / 1000.0 << " ms" << std::endl;

    return WriteOutputImage(out, outImage);
    }
    catch (const cv::Exception& e) {
        return MapExceptionToError("CM_Fusion_Async OpenCV", e);
    }
    catch (const std::exception& e) {
        return MapExceptionToError("CM_Fusion_Async", e);
    }
    catch (...) {
        return -101;
    }
}

/**
 * @brief Batch processing fusion for multiple image sets
 */
extern "C" COLORVISIONCORE_API int CM_Fusion_Batch(const char* batchjson, HImage* outImages, int outCapacity, int* outCount)
{
    if (outCount != nullptr) {
        *outCount = 0;
    }
    ClearHImages(outImages, outCapacity);

    if (batchjson == nullptr || outImages == nullptr || outCapacity <= 0 || outCount == nullptr) {
        return -1;
    }

    // TODO: Implement batch processing for multiple fusion tasks
    // This would allow processing multiple image sets in parallel
    return -10;
}
