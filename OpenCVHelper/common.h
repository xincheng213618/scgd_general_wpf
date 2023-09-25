#pragma once

#ifdef OPENCV_EXPORTS
#define OPENCV_API __declspec(dllexport)
#else
#define OPENCV_API __declspec(dllimport)
#endif

typedef int(__cdecl* InitialFrame)(void*, int, int, int);
typedef int(__cdecl* UpdateFrame)(void*, int, int);


extern "C" OPENCV_API void SetInitialFrame(InitialFrame fn);
extern "C" OPENCV_API void SetUpdateFrame(UpdateFrame fn);

extern "C" OPENCV_API int ReadCVFile(char* FilePath);

extern "C" OPENCV_API int ReadVideoTest(char* FilePath);

