#pragma once

#include <opencv2/opencv.hpp>
#include <memory>

#ifdef OPENCV_EXPORTS
#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif


struct MatDataDeleter {
    void operator()(char* p) const {
        free(p);
    }
};

using MatDataPtr = std::unique_ptr<char[], MatDataDeleter>;

typedef struct CustomFile
{
    int rows;
    int cols;
    int type;
    int compression; //0: uncompressed; 1: Zlib; 2: gz
    long long srcLen; // Mat.data length
    long long destLen; // compressed length (0 if uncompressed)
} CustomFile;

typedef struct CustomFileHeader
{
    char Name[6] = { 0x43,0x75,0x73,0x74,0x6f,0x6d }; // "Custom"
    int Version; // 0
    int Matoffset; // Offset to Mat data
} CustomFileHeader;


// Safe file I/O functions with RAII and error handling
COLORVISIONCORE_API int CVWrite(std::string path, cv::Mat src, int compression = 0);
COLORVISIONCORE_API cv::Mat CVRead(std::string FileName);
COLORVISIONCORE_API bool IsCustomFile(std::string path);

// Raw binary write functions
COLORVISIONCORE_API void OsWrite(std::string path, cv::Mat src);
COLORVISIONCORE_API void OsWrite1(std::string path, cv::Mat src);

// Utility functions
COLORVISIONCORE_API std::string UTF8ToGB(const char* str);

