#pragma once

#ifdef OPENCV_EXPORTS
#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif

#include <string>
std::string UTF8ToGB(const char* str);


typedef int(__cdecl* InitialFrame)(void*, int, int, int);
typedef int(__cdecl* UpdateFrame)(void*, int, int);


extern "C" COLORVISIONCORE_API void SetInitialFrame(InitialFrame fn);
extern "C" COLORVISIONCORE_API void SetUpdateFrame(UpdateFrame fn);