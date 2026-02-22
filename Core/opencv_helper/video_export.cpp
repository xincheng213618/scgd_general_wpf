#include "pch.h"
#include "video_export.h"
#include <opencv2/opencv.hpp>
#include <unordered_map>
#include <mutex>
#include <atomic>
#include <thread>
#include <chrono>

struct VideoContext {
	cv::VideoCapture cap;
	int totalFrames;
	double fps;
	int width;
	int height;
	std::atomic<bool> threadRunning; // 控制线程生命周期 (Close 时设为 false)
	std::atomic<bool> isPaused;      // 控制播放状态 (Pause/Play 切换)
	std::thread playThread;          // 解码线程 (生产者)
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

	// "最新帧槽位" (Latest Frame Slot) — 生产者直接覆盖，消费者取走最新帧
	cv::Mat latestFrame;
	int latestFrameIndex;
	bool latestFrameValid;
	std::mutex slotMutex;              // 保护槽位读写
	std::condition_variable slotReady; // 通知消费者有新帧

	VideoContext() : totalFrames(0), fps(0), width(0), height(0),
		stopRequested(false), playbackSpeed(1.0),
		frameCallback(nullptr), statusCallback(nullptr), userData(nullptr),
		seekRequestFrame(-1), threadRunning(true), isPaused(true),
		resizeScale(1.0), latestFrameIndex(0), latestFrameValid(false) {}
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

	// 生产者线程 (Producer): 按视频帧率解码，写入"最新帧槽位"，不等待消费者
	ctx->playThread = std::thread([ctx, handle]() {
		while (ctx->threadRunning) {
			auto frameStartTime = std::chrono::steady_clock::now();

			// 1. 处理暂停逻辑 (核心!)
			{
				std::unique_lock<std::mutex> lock(ctx->pauseMutex);
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
			}

			// 3. 读取帧
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
						if (!ctx->isPaused) {
							ctx->isPaused = true;
							if (ctx->statusCallback) ctx->statusCallback(handle, 2, ctx->userData);
						}
					}
				}

				// 4. Resize + 写入最新帧槽位 (直接覆盖，不等待)
				if (readSuccess) {
					double scale = ctx->resizeScale.load();
					if (scale > 0.0 && scale < 1.0) {
						// pyrDown 比 cv::resize 快很多 (SIMD优化，无逐像素插值)
						// 直接对 frame 做 in-place pyrDown 链，避免中间变量分配
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
							// 非2的幂次缩放：INTER_NEAREST最快，但会有锯齿
							cv::Mat resized;
							cv::resize(frame, resized, cv::Size(), scale, scale, cv::INTER_NEAREST);
							frame = resized;
						}
					}

					// 写入槽位 — 直接覆盖，生产者永不阻塞
					{
						std::lock_guard<std::mutex> slotLock(ctx->slotMutex);
						ctx->latestFrame = frame;       // cv::Mat 引用计数赋值，共享数据指针
						ctx->latestFrameIndex = currentFrame;
						ctx->latestFrameValid = true;
					}
					ctx->slotReady.notify_one();

					// Seek while paused: 直接回调显示一帧
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

			// 5. 帧率控制 — 扣除已用时间，保持稳定帧率
			if (!ctx->isPaused) {
				double effectiveFps = ctx->fps * ctx->playbackSpeed;
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
		});

	// 消费者线程 (Consumer): 从槽位取最新帧，调用C#回调
	ctx->consumerThread = std::thread([ctx, handle]() {
		while (ctx->threadRunning) {
			cv::Mat frameCopy;
			int frameIndex = 0;
			bool gotFrame = false;

			{
				std::unique_lock<std::mutex> slotLock(ctx->slotMutex);
				// 等待新帧，超时50ms避免死锁
				ctx->slotReady.wait_for(slotLock, std::chrono::milliseconds(50), [ctx] {
					return ctx->latestFrameValid || !ctx->threadRunning;
					});

				if (!ctx->threadRunning) break;

				if (ctx->latestFrameValid) {
					frameCopy = ctx->latestFrame;  // cv::Mat 引用计数，快速
					frameIndex = ctx->latestFrameIndex;
					ctx->latestFrameValid = false; // 标记已取走
					gotFrame = true;
				}
			}

			// 调用回调 (在锁外，不阻塞生产者)
			if (gotFrame && ctx->frameCallback && !ctx->isPaused) {
				HImage hImage;
				hImage.rows = frameCopy.rows;
				hImage.cols = frameCopy.cols;
				hImage.channels = frameCopy.channels();
				hImage.stride = static_cast<int>(frameCopy.step);
				hImage.depth = static_cast<int>(frameCopy.elemSize1()) * 8;
				hImage.pData = frameCopy.data;
				hImage.isDispose = true;
				ctx->frameCallback(handle, &hImage, frameIndex, ctx->totalFrames, ctx->userData);
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
	ctx->cvPause.notify_all();   // 唤醒生产者
	ctx->slotReady.notify_all(); // 唤醒消费者

	// 等待线程结束
	if (ctx->playThread.joinable()) {
		ctx->playThread.join();
	}
	if (ctx->consumerThread.joinable()) {
		ctx->consumerThread.join();
	}

	// 释放资源
	ctx->cap.release();
	delete ctx;
	return 0;
}
