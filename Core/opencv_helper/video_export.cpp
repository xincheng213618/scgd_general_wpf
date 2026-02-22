#include "pch.h"
#include "video_export.h"
#include <opencv2/opencv.hpp>
#include <unordered_map>
#include <mutex>
#include <atomic>
#include <thread>

// Frame buffer entry for producer-consumer queue
struct FrameBufferEntry {
	cv::Mat frame;
	int frameIndex;
	bool valid;
	FrameBufferEntry() : frameIndex(0), valid(false) {}
};

struct VideoContext {
	cv::VideoCapture cap;
	int totalFrames;
	double fps;
	int width;
	int height;
	std::atomic<bool> threadRunning; // 控制线程生命周期 (Close 时设为 false)
	std::atomic<bool> isPaused;      // 控制播放状态 (Pause/Play 切换)
	std::thread playThread;
	std::thread consumerThread;      // 消费者线程 (回调UI)
	std::atomic<bool> stopRequested;
	double playbackSpeed;
	std::mutex capMutex;
	std::condition_variable cvPause; //用于暂停时挂起线程，避免空转占用CPU
	std::mutex pauseMutex;
	VideoFrameCallback frameCallback;
	VideoStatusCallback statusCallback;
	void* userData;
	std::mutex seekMutex;
	std::atomic<int> seekRequestFrame; // -1 表示无请求，>=0 表示目标帧

	// Resize scale: 1.0 = original, 0.5 = 1/2, 0.25 = 1/4, 0.125 = 1/8
	std::atomic<double> resizeScale;

	// Producer-consumer frame buffer queue
	static const int BUFFER_SIZE = 4;
	FrameBufferEntry frameBuffer[BUFFER_SIZE];
	std::atomic<int> bufferWriteIdx;
	std::atomic<int> bufferReadIdx;
	std::atomic<int> bufferCount;
	std::mutex bufferMutex;
	std::condition_variable bufferNotEmpty;
	std::condition_variable bufferNotFull;

	VideoContext() : totalFrames(0), fps(0), width(0), height(0),
		stopRequested(false), playbackSpeed(1.0),
		frameCallback(nullptr), statusCallback(nullptr), userData(nullptr),
		seekRequestFrame(-1), threadRunning(true), isPaused(true),
		resizeScale(1.0), bufferWriteIdx(0), bufferReadIdx(0), bufferCount(0) {}
};

static std::unordered_map<int, VideoContext*> g_videos;
static std::mutex g_mapMutex;
static int g_nextHandle = 1;

COLORVISIONCORE_API int M_VideoOpen(const wchar_t* filePath, VideoInfo* info)
{
	if (!filePath || !info) return -1;

	// Convert wchar_t to UTF-8 for OpenCV
	int len = WideCharToMultiByte(CP_UTF8, 0, filePath, -1, NULL, 0, NULL, NULL);
	std::string path(len - 1, '\0');
	WideCharToMultiByte(CP_UTF8, 0, filePath, -1, &path[0], len, NULL, NULL);

	auto ctx = new VideoContext();
	std::string gbkPath = UTF8ToGB(path.c_str());
	ctx->cap.open(gbkPath);
	if (!ctx->cap.isOpened()) {
		delete ctx;
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

	std::lock_guard<std::mutex> lock(g_mapMutex);
	int handle = g_nextHandle++;
	g_videos[handle] = ctx;

	ctx->threadRunning = true;
	ctx->isPaused = true; // 初始状态是暂停

	// Producer thread: reads frames from video and puts them into the buffer
	ctx->playThread = std::thread([ctx, handle]() {
		while (ctx->threadRunning) {
			// 1. 处理暂停逻辑 (核心!)
			{
				std::unique_lock<std::mutex> lock(ctx->pauseMutex);
				// 如果暂停且没有退出请求，就挂起线程等待
				ctx->cvPause.wait(lock, [ctx] {
					return !ctx->isPaused || !ctx->threadRunning || ctx->seekRequestFrame >= 0;
					});
			}

			if (!ctx->threadRunning) break;

			// 2. 处理 Seek (即使在暂停状态也能 Seek!)
			bool justSeeked = false;
			int seekTo = ctx->seekRequestFrame.exchange(-1);
			if (seekTo >= 0) {
				std::lock_guard<std::mutex> lock(ctx->capMutex);
				if (seekTo < ctx->totalFrames) {
					ctx->cap.set(cv::CAP_PROP_POS_FRAMES, seekTo);
					justSeeked = true;
				}
				// Clear buffer on seek
				{
					std::lock_guard<std::mutex> bufLock(ctx->bufferMutex);
					ctx->bufferWriteIdx = 0;
					ctx->bufferReadIdx = 0;
					ctx->bufferCount = 0;
				}
				ctx->bufferNotEmpty.notify_one();
			}

			// 3. 读取帧 (只有在播放中，或者刚刚 Seek 完需要刷新一帧时才读)
			if (!ctx->isPaused || justSeeked) {
				cv::Mat frame;
				int currentFrame;
				bool readSuccess = false;

				{
					std::lock_guard<std::mutex> lock(ctx->capMutex);
					currentFrame = (int)ctx->cap.get(cv::CAP_PROP_POS_FRAMES);
					if (ctx->cap.read(frame) && !frame.empty()) {
						readSuccess = true;
					}
					else {
						// 播放结束处理 (End of Video)
						if (!ctx->isPaused) { // 只有播放中才算结束
							ctx->isPaused = true;
							if (ctx->statusCallback) ctx->statusCallback(handle, 2, ctx->userData);
						}
					}
				}

				// 4. Apply resize if needed and put frame into buffer
				if (readSuccess) {
					double scale = ctx->resizeScale.load();
					if (scale > 0.0 && scale < 1.0) {
						cv::Mat resized;
						cv::resize(frame, resized, cv::Size(), scale, scale, cv::INTER_LINEAR);
						frame = resized;
					}

					// Producer: wait for space in buffer (with timeout to allow exit)
					{
						std::unique_lock<std::mutex> bufLock(ctx->bufferMutex);
						ctx->bufferNotFull.wait_for(bufLock, std::chrono::milliseconds(50), [ctx] {
							return ctx->bufferCount < VideoContext::BUFFER_SIZE || !ctx->threadRunning;
							});

						if (!ctx->threadRunning) break;

						if (ctx->bufferCount < VideoContext::BUFFER_SIZE) {
							int idx = ctx->bufferWriteIdx;
							ctx->frameBuffer[idx].frame = frame;
							ctx->frameBuffer[idx].frameIndex = currentFrame;
							ctx->frameBuffer[idx].valid = true;
							ctx->bufferWriteIdx = (idx + 1) % VideoContext::BUFFER_SIZE;
							ctx->bufferCount++;
						}
						// If buffer is full, drop this frame (producer-side frame dropping)
					}
					ctx->bufferNotEmpty.notify_one();

					// If this was a seek while paused, directly call callback for immediate display
					if (justSeeked && ctx->isPaused && ctx->frameCallback) {
						HImage hImage;
						hImage.rows = frame.rows;
						hImage.cols = frame.cols;
						hImage.channels = frame.channels();
						hImage.stride = static_cast<int>(frame.step);
						hImage.depth = static_cast<int>(frame.elemSize1()) * 8;
						hImage.pData = frame.data;
						hImage.isDispose = true;
						ctx->frameCallback(handle, &hImage, currentFrame, ctx->totalFrames, ctx->userData);
					}
				}
			}

			// 5. 帧率控制 (仅在播放时 sleep，暂停时会在 loop 头部 wait)
			if (!ctx->isPaused) {
				double effectiveFps = ctx->fps * ctx->playbackSpeed;
				if (effectiveFps <= 0) effectiveFps = 30.0;
				int delay = (int)(1000.0 / effectiveFps);
				std::this_thread::sleep_for(std::chrono::milliseconds(std::max(1, delay)));
			}
		}
		});

	// Consumer thread: reads frames from buffer and calls callback
	ctx->consumerThread = std::thread([ctx, handle]() {
		while (ctx->threadRunning) {
			FrameBufferEntry entry;
			{
				std::unique_lock<std::mutex> bufLock(ctx->bufferMutex);
				ctx->bufferNotEmpty.wait_for(bufLock, std::chrono::milliseconds(50), [ctx] {
					return ctx->bufferCount > 0 || !ctx->threadRunning;
					});

				if (!ctx->threadRunning) break;
				if (ctx->bufferCount <= 0) continue;

				// Frame dropping: if multiple frames buffered, skip to latest (discard strategy)
				while (ctx->bufferCount > 1) {
					ctx->bufferReadIdx = (ctx->bufferReadIdx.load() + 1) % VideoContext::BUFFER_SIZE;
					ctx->bufferCount--;
				}
				// Notify producer that buffer space was freed by dropping frames
				if (ctx->bufferCount < VideoContext::BUFFER_SIZE) {
					ctx->bufferNotFull.notify_one();
				}

				int idx = ctx->bufferReadIdx;
				entry = ctx->frameBuffer[idx];
				ctx->frameBuffer[idx].valid = false;
				ctx->bufferReadIdx = (idx + 1) % VideoContext::BUFFER_SIZE;
				ctx->bufferCount--;
			}
			ctx->bufferNotFull.notify_one();

			// Call frame callback
			if (entry.valid && ctx->frameCallback && !ctx->isPaused) {
				HImage hImage;
				hImage.rows = entry.frame.rows;
				hImage.cols = entry.frame.cols;
				hImage.channels = entry.frame.channels();
				hImage.stride = static_cast<int>(entry.frame.step);
				hImage.depth = static_cast<int>(entry.frame.elemSize1()) * 8;
				hImage.pData = entry.frame.data;
				hImage.isDispose = true;
				ctx->frameCallback(handle, &hImage, entry.frameIndex, ctx->totalFrames, ctx->userData);
			}
		}
		});

	return handle;
}

COLORVISIONCORE_API int M_VideoReadFrame(int handle, HImage* outImage)
{
	std::lock_guard<std::mutex> lock(g_mapMutex);
	auto it = g_videos.find(handle);
	if (it == g_videos.end() || !outImage) return -1;

	VideoContext* ctx = it->second;
	std::lock_guard<std::mutex> seekLock(ctx->seekMutex);

	cv::Mat frame; 
	if (!ctx->cap.read(frame) || frame.empty()) return -2;

	// Convert to BGR if needed (OpenCV reads as BGR by default)
	return MatToHImage(frame, outImage);
}

COLORVISIONCORE_API int M_VideoSeek(int handle, int frameIndex)
{
	VideoContext* ctx = nullptr;
	{
		std::lock_guard<std::mutex> lock(g_mapMutex); // 保护 map 查找
		auto it = g_videos.find(handle);
		if (it == g_videos.end()) return -1;
		ctx = it->second;
	}
	if (frameIndex >= 0 && frameIndex < ctx->totalFrames) {
		ctx->seekRequestFrame = frameIndex;
		ctx->cvPause.notify_one();
	}
	return 0;
}

COLORVISIONCORE_API int M_VideoGetCurrentFrame(int handle)
{
	std::lock_guard<std::mutex> lock(g_mapMutex);
	auto it = g_videos.find(handle);
	if (it == g_videos.end()) return -1;

	return (int)it->second->cap.get(cv::CAP_PROP_POS_FRAMES);
}

COLORVISIONCORE_API int M_VideoSetPlaybackSpeed(int handle, double speed)
{
	std::lock_guard<std::mutex> lock(g_mapMutex);
	auto it = g_videos.find(handle);
	if (it == g_videos.end()) return -1;

	it->second->playbackSpeed = speed;
	return 0;
}

COLORVISIONCORE_API int M_VideoSetResizeScale(int handle, double scale)
{
	std::lock_guard<std::mutex> lock(g_mapMutex);
	auto it = g_videos.find(handle);
	if (it == g_videos.end()) return -1;

	// Clamp to valid values
	if (scale <= 0.0) scale = 0.125;
	if (scale > 1.0) scale = 1.0;
	it->second->resizeScale = scale;
	return 0;
}

COLORVISIONCORE_API int M_VideoPlay(int handle, VideoFrameCallback frameCallback, VideoStatusCallback statusCallback, void* userData)
{
	VideoContext* ctx = nullptr;
	{
		std::lock_guard<std::mutex> lock(g_mapMutex);
		auto it = g_videos.find(handle);
		if (it == g_videos.end()) return -1;
		ctx = it->second;
	}

	ctx->frameCallback = frameCallback;
	ctx->statusCallback = statusCallback;
	ctx->userData = userData;

	// 切换状态
	ctx->isPaused = false;
	ctx->cvPause.notify_one(); // 唤醒沉睡的线程

	if (ctx->statusCallback) ctx->statusCallback(handle, 1, ctx->userData);

	return 0;
}

COLORVISIONCORE_API int M_VideoPause(int handle)
{
	VideoContext* ctx = nullptr;
	{
		std::lock_guard<std::mutex> lock(g_mapMutex);
		auto it = g_videos.find(handle);
		if (it == g_videos.end()) return -1;
		ctx = it->second;
	}

	ctx->isPaused = true;

	if (ctx->statusCallback) ctx->statusCallback(handle, 0, ctx->userData);
	return 0;
}

COLORVISIONCORE_API int M_VideoClose(int handle)
{
	VideoContext* ctx = nullptr;
	{
		std::lock_guard<std::mutex> lock(g_mapMutex);
		auto it = g_videos.find(handle);
		if (it == g_videos.end()) return -1;
		ctx = it->second;
		g_videos.erase(it); // 先从全局表中移除，防止其他线程再次获取
	}

	ctx->threadRunning = false;
	ctx->isPaused = false; // 确保它不卡在 wait 里
	ctx->cvPause.notify_all(); // 唤醒所有等待
	ctx->bufferNotEmpty.notify_all(); // 唤醒消费者线程
	ctx->bufferNotFull.notify_all();  // 唤醒生产者线程

	// 2. 等待线程结束
	if (ctx->playThread.joinable()) {
		ctx->playThread.join();
	}
	if (ctx->consumerThread.joinable()) {
		ctx->consumerThread.join();
	}

	// 3. 释放资源
	ctx->cap.release();
	delete ctx;
	return 0;
}
