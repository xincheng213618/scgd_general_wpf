#include "pch.h"
#include "video_export.h"
#include <opencv2/opencv.hpp>
#include <unordered_map>
#include <mutex>
#include <atomic>
#include <thread>

struct VideoContext {
	cv::VideoCapture cap;
	int totalFrames;
	double fps;
	int width;
	int height;
	std::atomic<bool> isPlaying;
	std::atomic<bool> stopRequested;
	std::thread playThread;
	double playbackSpeed;
	VideoFrameCallback frameCallback;
	VideoStatusCallback statusCallback;
	void* userData;
	std::mutex seekMutex;

	VideoContext() : totalFrames(0), fps(0), width(0), height(0),
		isPlaying(false), stopRequested(false), playbackSpeed(1.0),
		frameCallback(nullptr), statusCallback(nullptr), userData(nullptr) {}
};

static std::unordered_map<int, VideoContext*> g_videos;
static std::mutex g_mapMutex;
static int g_nextHandle = 1;

COLORVISIONCORE_API int M_VideoOpen(const wchar_t* filePath, VideoInfo* info)
{
	if (!filePath || !info) return -1;

	// Convert wchar_t to std::string for OpenCV
	int len = WideCharToMultiByte(CP_ACP, 0, filePath, -1, NULL, 0, NULL, NULL);
	std::string path(len - 1, '\0');
	WideCharToMultiByte(CP_ACP, 0, filePath, -1, &path[0], len, NULL, NULL);

	auto ctx = new VideoContext();
	ctx->cap.open(path);
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
	std::lock_guard<std::mutex> lock(g_mapMutex);
	auto it = g_videos.find(handle);
	if (it == g_videos.end()) return -1;

	VideoContext* ctx = it->second;
	std::lock_guard<std::mutex> seekLock(ctx->seekMutex);

	if (frameIndex < 0 || frameIndex >= ctx->totalFrames) return -2;
	ctx->cap.set(cv::CAP_PROP_POS_FRAMES, frameIndex);
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

COLORVISIONCORE_API int M_VideoPlay(int handle, VideoFrameCallback frameCallback, VideoStatusCallback statusCallback, void* userData)
{
	VideoContext* ctx = nullptr;
	{
		std::lock_guard<std::mutex> lock(g_mapMutex);
		auto it = g_videos.find(handle);
		if (it == g_videos.end()) return -1;
		ctx = it->second;
	}

	if (ctx->isPlaying) return 0; // Already playing

	ctx->frameCallback = frameCallback;
	ctx->statusCallback = statusCallback;
	ctx->userData = userData;
	ctx->stopRequested = false;
	ctx->isPlaying = true;

	// If there's an old thread, join it first
	if (ctx->playThread.joinable()) {
		ctx->playThread.join();
	}

	ctx->playThread = std::thread([ctx, handle]() {
		while (!ctx->stopRequested) {
			cv::Mat frame;
			int currentFrame;
			{
				std::lock_guard<std::mutex> seekLock(ctx->seekMutex);
				currentFrame = (int)ctx->cap.get(cv::CAP_PROP_POS_FRAMES);
				if (!ctx->cap.read(frame) || frame.empty()) {
					// Reached end of video
					if (ctx->statusCallback) {
						ctx->statusCallback(handle, 2, ctx->userData); // 2 = ended
					}
					break;
				}
			}

			if (ctx->frameCallback) {
				HImage hImage;
				if (MatToHImage(frame, &hImage) == 0) {
					ctx->frameCallback(handle, &hImage, currentFrame, ctx->totalFrames, ctx->userData);
				}
			}

			// Calculate delay based on fps and playback speed
			double delay = 1000.0 / (ctx->fps * ctx->playbackSpeed);
			if (delay < 1) delay = 1;
			std::this_thread::sleep_for(std::chrono::milliseconds((int)delay));
		}

		ctx->isPlaying = false;
		if (ctx->statusCallback && !ctx->stopRequested) {
			// Only send stopped status if not manually stopped
		}
	});

	if (ctx->statusCallback) {
		ctx->statusCallback(handle, 1, ctx->userData); // 1 = playing
	}

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

	ctx->stopRequested = true;
	if (ctx->playThread.joinable()) {
		ctx->playThread.join();
	}
	ctx->isPlaying = false;

	if (ctx->statusCallback) {
		ctx->statusCallback(handle, 0, ctx->userData); // 0 = paused
	}
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
		g_videos.erase(it);
	}

	// Stop playback
	ctx->stopRequested = true;
	if (ctx->playThread.joinable()) {
		ctx->playThread.join();
	}

	ctx->cap.release();
	delete ctx;
	return 0;
}
