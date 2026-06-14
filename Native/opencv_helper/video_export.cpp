#include "pch.h"
#include "video_export.h"
#include <opencv2/opencv.hpp>
#include <unordered_map>
#include <mutex>
#include <atomic>
#include <thread>
#include <chrono>
#include <condition_variable>
#include <exception>
#include <memory>

struct VideoContext {
	cv::VideoCapture cap;
	int totalFrames;
	double fps;
	int width;
	int height;
	std::atomic<bool> threadRunning; // Cleared by Close to stop worker threads.
	std::atomic<bool> isPaused;      // Playback state toggled by Pause/Play.
	std::thread playThread;          // Decode producer thread.
	std::thread consumerThread;      // Callback consumer thread.
	std::atomic<bool> stopRequested;
	std::atomic<double> playbackSpeed;
	std::mutex capMutex;
	std::condition_variable cvPause; // Suspends the producer while paused.
	std::mutex pauseMutex;
	VideoFrameCallback frameCallback;
	VideoStatusCallback statusCallback;
	void* userData;
	std::mutex callbackMutex;
	std::mutex seekMutex;
	std::atomic<int> seekRequestFrame; // -1 means no request; >=0 is target frame.

	// Resize scale: 1.0 = original, 0.5 = 1/2, 0.25 = 1/4, 0.125 = 1/8
	std::atomic<double> resizeScale;

	// Latest frame slot: producer overwrites it, consumer takes the newest frame.
	cv::Mat latestFrame;
	int latestFrameIndex;
	bool latestFrameValid;
	std::mutex slotMutex;              // Protects latest frame slot reads/writes.
	std::condition_variable slotReady; // Notifies the consumer when a frame is ready.

	VideoContext() : totalFrames(0), fps(0), width(0), height(0),
		stopRequested(false), playbackSpeed(1.0),
		frameCallback(nullptr), statusCallback(nullptr), userData(nullptr),
		seekRequestFrame(-1), threadRunning(true), isPaused(true),
		resizeScale(1.0), latestFrameIndex(0), latestFrameValid(false) {}
};

using VideoContextPtr = std::shared_ptr<VideoContext>;

static std::unordered_map<int, VideoContextPtr> g_videos;
static std::mutex g_mapMutex;
static int g_nextHandle = 1;

template <typename Func>
static int GuardVideoExport(Func func) noexcept
{
	try {
		return func();
	}
	catch (const cv::Exception&) {
		return -2;
	}
	catch (const std::exception&) {
		return -3;
	}
	catch (...) {
		return -4;
	}
}

static void StopVideoWorkers(const VideoContextPtr& ctx) noexcept
{
	if (!ctx) {
		return;
	}

	try {
		{
			std::lock_guard<std::mutex> lock(ctx->pauseMutex);
			ctx->threadRunning = false;
			ctx->isPaused = false;
		}
		{
			std::lock_guard<std::mutex> lock(ctx->slotMutex);
			ctx->threadRunning = false;
			ctx->latestFrameValid = false;
		}

		ctx->cvPause.notify_all();
		ctx->slotReady.notify_all();

		const auto currentThreadId = std::this_thread::get_id();
		if (ctx->playThread.joinable()) {
			if (ctx->playThread.get_id() == currentThreadId) {
				ctx->playThread.detach();
			}
			else {
				ctx->playThread.join();
			}
		}
		if (ctx->consumerThread.joinable()) {
			if (ctx->consumerThread.get_id() == currentThreadId) {
				ctx->consumerThread.detach();
			}
			else {
				ctx->consumerThread.join();
			}
		}
	}
	catch (...) {
	}
}

static bool IsImageSequencePattern(const std::string& path)
{
	for (size_t pos = path.find('%'); pos != std::string::npos; pos = path.find('%', pos + 1)) {
		size_t index = pos + 1;
		if (index < path.size() && path[index] == '0') {
			++index;
		}
		while (index < path.size() && path[index] >= '0' && path[index] <= '9') {
			++index;
		}
		if (index < path.size() && path[index] == 'd') {
			return true;
		}
	}

	return false;
}

static VideoContextPtr GetVideoContext(int handle)
{
	std::lock_guard<std::mutex> lock(g_mapMutex);
	auto it = g_videos.find(handle);
	return it != g_videos.end() ? it->second : nullptr;
}

static void InvokeStatus(VideoContext& ctx, int handle, int status)
{
	VideoStatusCallback callback = nullptr;
	void* userData = nullptr;
	{
		std::lock_guard<std::mutex> lock(ctx.callbackMutex);
		callback = ctx.statusCallback;
		userData = ctx.userData;
	}

	if (callback) {
		callback(handle, status, userData);
	}
}

static int InvokeFrame(VideoContext& ctx, int handle, const cv::Mat& frame, int frameIndex)
{
	VideoFrameCallback callback = nullptr;
	void* userData = nullptr;
	{
		std::lock_guard<std::mutex> lock(ctx.callbackMutex);
		callback = ctx.frameCallback;
		userData = ctx.userData;
	}

	if (!callback) {
		return 0;
	}

	HImage hImage{};
	int convertResult = MatToHImage(frame, &hImage);
	if (convertResult != 0) {
		return convertResult;
	}

	callback(handle, &hImage, frameIndex, ctx.totalFrames, userData);
	return 0;
}

static void VideoProducerLoop(VideoContextPtr ctx, int handle)
{
	try {
		while (ctx->threadRunning) {
			auto frameStartTime = std::chrono::steady_clock::now();

			{
				std::unique_lock<std::mutex> lock(ctx->pauseMutex);
				ctx->cvPause.wait(lock, [ctx] {
					return !ctx->isPaused || !ctx->threadRunning || ctx->seekRequestFrame >= 0;
					});
			}

			if (!ctx->threadRunning) break;

			bool justSeeked = false;
			int seekTo = ctx->seekRequestFrame.exchange(-1);
			if (seekTo >= 0) {
				std::lock_guard<std::mutex> lock(ctx->capMutex);
				if (seekTo < ctx->totalFrames) {
					ctx->cap.set(cv::CAP_PROP_POS_FRAMES, seekTo);
					justSeeked = true;
				}
			}

			if (!ctx->isPaused || justSeeked) {
				cv::Mat frame;
				int currentFrame = 0;
				bool readSuccess = false;
				bool reachedEnd = false;

				{
					std::lock_guard<std::mutex> lock(ctx->capMutex);
					currentFrame = (int)ctx->cap.get(cv::CAP_PROP_POS_FRAMES);
					if (ctx->totalFrames > 0 && currentFrame >= ctx->totalFrames) {
						reachedEnd = true;
					}
					else if (ctx->cap.read(frame) && !frame.empty()) {
						readSuccess = true;
					}
					else {
						reachedEnd = true;
					}
				}

				if (reachedEnd && !ctx->isPaused) {
					ctx->isPaused = true;
					InvokeStatus(*ctx, handle, 2);
				}

				if (readSuccess) {
					double scale = ctx->resizeScale.load();
					if (scale > 0.0 && scale < 1.0) {
						if (scale == 0.5) {
							cv::pyrDown(frame, frame);
						}
						else if (scale == 0.25) {
							cv::pyrDown(frame, frame);
							cv::pyrDown(frame, frame);
						}
						else if (scale == 0.125) {
							cv::pyrDown(frame, frame);
							cv::pyrDown(frame, frame);
							cv::pyrDown(frame, frame);
						}
						else {
							cv::Mat resized;
							cv::resize(frame, resized, cv::Size(), scale, scale, cv::INTER_NEAREST);
							frame = resized;
						}
					}

					{
						std::lock_guard<std::mutex> slotLock(ctx->slotMutex);
						ctx->latestFrame = frame;
						ctx->latestFrameIndex = currentFrame;
						ctx->latestFrameValid = true;
					}
					ctx->slotReady.notify_one();

					if (justSeeked && ctx->isPaused) {
						InvokeFrame(*ctx, handle, frame, currentFrame);
					}
				}
			}

			if (!ctx->isPaused) {
				double effectiveFps = ctx->fps * ctx->playbackSpeed.load();
				if (effectiveFps <= 0) effectiveFps = 30.0;
				int targetDelayMs = (int)(1000.0 / effectiveFps);
				auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
					std::chrono::steady_clock::now() - frameStartTime).count();
				int remainingMs = targetDelayMs - (int)elapsed;
				if (remainingMs > 1) {
					std::this_thread::sleep_for(std::chrono::milliseconds(remainingMs));
				}
			}
		}
	}
	catch (...) {
		ctx->threadRunning = false;
		ctx->isPaused = true;
		ctx->slotReady.notify_all();
		ctx->cvPause.notify_all();
	}
}

static void VideoConsumerLoop(VideoContextPtr ctx, int handle)
{
	try {
		while (ctx->threadRunning) {
			cv::Mat frameCopy;
			int frameIndex = 0;
			bool gotFrame = false;

			{
				std::unique_lock<std::mutex> slotLock(ctx->slotMutex);
				ctx->slotReady.wait_for(slotLock, std::chrono::milliseconds(50), [ctx] {
					return ctx->latestFrameValid || !ctx->threadRunning;
					});

				if (!ctx->threadRunning) break;

				if (ctx->latestFrameValid) {
					frameCopy = ctx->latestFrame;
					frameIndex = ctx->latestFrameIndex;
					ctx->latestFrameValid = false;
					gotFrame = true;
				}
			}

			if (gotFrame && !ctx->isPaused) {
				InvokeFrame(*ctx, handle, frameCopy, frameIndex);
			}
		}
	}
	catch (...) {
		ctx->threadRunning = false;
		ctx->isPaused = true;
		ctx->slotReady.notify_all();
		ctx->cvPause.notify_all();
	}
}

COLORVISIONCORE_API int M_VideoOpen(const wchar_t* filePath, VideoInfo* info)
{
	return GuardVideoExport([&]() -> int {
		if (!info) return -1;
		*info = VideoInfo{};
		if (!filePath) return -1;

		// Convert wchar_t to UTF-8 for OpenCV
		int len = WideCharToMultiByte(CP_UTF8, 0, filePath, -1, NULL, 0, NULL, NULL);
		if (len <= 1) {
			return -1;
		}

		std::string path(len, '\0');
		WideCharToMultiByte(CP_UTF8, 0, filePath, -1, path.data(), len, NULL, NULL);
		path.resize(len - 1);

		auto ctx = std::make_shared<VideoContext>();
		std::string gbkPath = UTF8ToGB(path.c_str());
		bool isOpened = false;
		if (IsImageSequencePattern(gbkPath)) {
			isOpened = ctx->cap.open(gbkPath, cv::CAP_IMAGES);
		}
		else {
			isOpened = ctx->cap.open(gbkPath);
		}
		if (!isOpened || !ctx->cap.isOpened()) {
			return -1;
		}

		ctx->totalFrames = (int)ctx->cap.get(cv::CAP_PROP_FRAME_COUNT);
		ctx->fps = ctx->cap.get(cv::CAP_PROP_FPS);
		ctx->width = (int)ctx->cap.get(cv::CAP_PROP_FRAME_WIDTH);
		ctx->height = (int)ctx->cap.get(cv::CAP_PROP_FRAME_HEIGHT);
		ctx->playbackSpeed = 1.0;

		info->totalFrames = ctx->totalFrames;
		info->fps = ctx->fps;
		info->width = ctx->width;
		info->height = ctx->height;

		int handle;
		{
			std::lock_guard<std::mutex> lock(g_mapMutex);
			handle = g_nextHandle++;
		}

		ctx->threadRunning = true;
		ctx->isPaused = true; // Starts paused.

		try {
			ctx->playThread = std::thread(VideoProducerLoop, ctx, handle);
			ctx->consumerThread = std::thread(VideoConsumerLoop, ctx, handle);

			std::lock_guard<std::mutex> lock(g_mapMutex);
			g_videos[handle] = ctx;
		}
		catch (...) {
			StopVideoWorkers(ctx);
			throw;
		}

		return handle;
		});
}

COLORVISIONCORE_API int M_VideoReadFrame(int handle, HImage* outImage)
{
	return GuardVideoExport([&]() -> int {
		if (!outImage) return -1;
		*outImage = HImage{};

		auto ctx = GetVideoContext(handle);
		if (!ctx) return -1;

		cv::Mat frame;
		{
			std::lock_guard<std::mutex> lock(ctx->capMutex);
			int currentFrame = (int)ctx->cap.get(cv::CAP_PROP_POS_FRAMES);
			if (ctx->totalFrames > 0 && currentFrame >= ctx->totalFrames) return -3;
			if (!ctx->cap.read(frame) || frame.empty()) return -2;
		}

		// Convert to BGR if needed (OpenCV reads as BGR by default)
		return MatToHImage(frame, outImage);
		});
}

COLORVISIONCORE_API int M_VideoSeek(int handle, int frameIndex)
{
	return GuardVideoExport([&]() -> int {
		auto ctx = GetVideoContext(handle);
		if (!ctx) return -1;

		if (frameIndex >= 0 && frameIndex < ctx->totalFrames) {
			{
				std::lock_guard<std::mutex> lock(ctx->pauseMutex);
				ctx->seekRequestFrame = frameIndex;
			}
			ctx->cvPause.notify_one();
			return 0;
		}
		return -2;
		});
}

COLORVISIONCORE_API int M_VideoGetCurrentFrame(int handle)
{
	return GuardVideoExport([&]() -> int {
		auto ctx = GetVideoContext(handle);
		if (!ctx) return -1;

		std::lock_guard<std::mutex> lock(ctx->capMutex);
		return (int)ctx->cap.get(cv::CAP_PROP_POS_FRAMES);
		});
}

COLORVISIONCORE_API int M_VideoSetPlaybackSpeed(int handle, double speed)
{
	return GuardVideoExport([&]() -> int {
		auto ctx = GetVideoContext(handle);
		if (!ctx) return -1;

		if (speed <= 0.0) speed = 1.0;
		ctx->playbackSpeed.store(speed);
		return 0;
		});
}

COLORVISIONCORE_API int M_VideoSetResizeScale(int handle, double scale)
{
	return GuardVideoExport([&]() -> int {
		auto ctx = GetVideoContext(handle);
		if (!ctx) return -1;

		// Clamp to valid values
		if (scale <= 0.0) scale = 0.125;
		if (scale > 1.0) scale = 1.0;
		ctx->resizeScale = scale;
		return 0;
		});
}

COLORVISIONCORE_API int M_VideoPlay(int handle, VideoFrameCallback frameCallback, VideoStatusCallback statusCallback, void* userData)
{
	return GuardVideoExport([&]() -> int {
		auto ctx = GetVideoContext(handle);
		if (!ctx) return -1;
		if (!frameCallback) return -2;

		{
			std::lock_guard<std::mutex> callbackLock(ctx->callbackMutex);
			ctx->frameCallback = frameCallback;
			ctx->statusCallback = statusCallback;
			ctx->userData = userData;
		}

		{
			std::lock_guard<std::mutex> pauseLock(ctx->pauseMutex);
			ctx->isPaused = false;
		}

		ctx->cvPause.notify_one();
		InvokeStatus(*ctx, handle, 1);
		return 0;
		});
}

COLORVISIONCORE_API int M_VideoPause(int handle)
{
	return GuardVideoExport([&]() -> int {
		auto ctx = GetVideoContext(handle);
		if (!ctx) return -1;

		{
			std::lock_guard<std::mutex> pauseLock(ctx->pauseMutex);
			ctx->isPaused = true;
		}

		InvokeStatus(*ctx, handle, 0);
		return 0;
		});
}

COLORVISIONCORE_API int M_VideoClose(int handle)
{
	return GuardVideoExport([&]() -> int {
		VideoContextPtr ctx;
		{
			std::lock_guard<std::mutex> lock(g_mapMutex);
			auto it = g_videos.find(handle);
			if (it == g_videos.end()) return -1;
			ctx = it->second;
			g_videos.erase(it); // Remove first so no new API call can obtain it.
		}

		{
			std::lock_guard<std::mutex> lock(ctx->callbackMutex);
			ctx->frameCallback = nullptr;
			ctx->statusCallback = nullptr;
			ctx->userData = nullptr;
		}

		StopVideoWorkers(ctx);

		// Release capture resources.
		ctx->cap.release();
		return 0;
		});
}
