#pragma once

#include "custom_structs.h"
#include "common.h"
#include <Windows.h>

#ifdef OPENCV_EXPORTS
#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif

#pragma pack(push, 1)
typedef struct VideoInfo {
	int totalFrames;
	double fps;
	int width;
	int height;
} VideoInfo;
#pragma pack(pop)

// Callback: called on each frame during playback
// handle: video handle
// frame: the frame image (caller must free with HImage dispose)
// currentFrame: current frame index
// totalFrames: total frame count
// userData: user-provided context pointer
typedef void(__stdcall* VideoFrameCallback)(int handle, HImage* frame, int currentFrame, int totalFrames, void* userData);

// Callback: called when playback status changes
// handle: video handle
// status: 0=paused, 1=playing, 2=ended
// userData: user-provided context pointer
typedef void(__stdcall* VideoStatusCallback)(int handle, int status, void* userData);

// Open a video file, returns handle (>0) on success, <0 on error
extern "C" COLORVISIONCORE_API int M_VideoOpen(const wchar_t* filePath, VideoInfo* info);

// Read the next frame from video (manual frame-by-frame reading)
extern "C" COLORVISIONCORE_API int M_VideoReadFrame(int handle, HImage* outImage);

// Seek to a specific frame
extern "C" COLORVISIONCORE_API int M_VideoSeek(int handle, int frameIndex);

// Get current frame position
extern "C" COLORVISIONCORE_API int M_VideoGetCurrentFrame(int handle);

// Set playback speed multiplier (1.0 = normal, 2.0 = 2x, 0.5 = half speed)
extern "C" COLORVISIONCORE_API int M_VideoSetPlaybackSpeed(int handle, double speed);

// Start playback with frame callback (runs on background thread)
extern "C" COLORVISIONCORE_API int M_VideoPlay(int handle, VideoFrameCallback frameCallback, VideoStatusCallback statusCallback, void* userData);

// Pause playback
extern "C" COLORVISIONCORE_API int M_VideoPause(int handle);

// Set resize scale for video playback (1.0 = original, 0.5 = 1/2, 0.25 = 1/4, 0.125 = 1/8)
extern "C" COLORVISIONCORE_API int M_VideoSetResizeScale(int handle, double scale);

// Close video and release resources
extern "C" COLORVISIONCORE_API int M_VideoClose(int handle);
