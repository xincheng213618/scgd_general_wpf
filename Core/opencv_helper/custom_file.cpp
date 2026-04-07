// ConsoleApplication1.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//
#include "pch.h"
#include "custom_file.h"
#include <zlib.h>
#include <fstream>
#include <iostream>
#include <opencv2/opencv.hpp>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <Windows.h>
#include <vector>
#include <memory>

using namespace std;

// Error codes for file operations
enum class FileError {
    Success = 0,
    FileNotFound = -1,
    InvalidFormat = -2,
    BufferTooSmall = -3,
    MemoryError = -4,
    CompressionError = -5,
    DecompressionError = -6
};

// RAII wrapper for file streams
class FileStreamGuard {
    std::ifstream& m_inFile;
    std::ofstream& m_outFile;
    bool m_inOpened;
    bool m_outOpened;

public:
    FileStreamGuard(std::ifstream& inFile, std::ofstream& outFile)
        : m_inFile(inFile), m_outFile(outFile), m_inOpened(false), m_outOpened(false) {}

    ~FileStreamGuard() {
        if (m_inFile.is_open()) m_inFile.close();
        if (m_outFile.is_open()) m_outFile.close();
    }
};

// RAII wrapper for memory allocated with malloc
class MallocGuard {
    char* m_ptr;

public:
    explicit MallocGuard(char* ptr) : m_ptr(ptr) {}
    ~MallocGuard() { if (m_ptr) free(m_ptr); }

    void release() { m_ptr = nullptr; }
    char* get() const { return m_ptr; }

    // Disable copy
    MallocGuard(const MallocGuard&) = delete;
    MallocGuard& operator=(const MallocGuard&) = delete;

    // Enable move
    MallocGuard(MallocGuard&& other) noexcept : m_ptr(other.m_ptr) {
        other.m_ptr = nullptr;
    }
    MallocGuard& operator=(MallocGuard&& other) noexcept {
        if (this != &other) {
            if (m_ptr) free(m_ptr);
            m_ptr = other.m_ptr;
            other.m_ptr = nullptr;
        }
        return *this;
    }
};

// RAII wrapper for memory allocated with new[]
template<typename T>
class ArrayGuard {
    T* m_ptr;

public:
    explicit ArrayGuard(T* ptr) : m_ptr(ptr) {}
    ~ArrayGuard() { delete[] m_ptr; }

    void release() { m_ptr = nullptr; }
    T* get() const { return m_ptr; }
    T& operator[](size_t idx) { return m_ptr[idx]; }

    // Disable copy
    ArrayGuard(const ArrayGuard&) = delete;
    ArrayGuard& operator=(const ArrayGuard&) = delete;

    // Enable move
    ArrayGuard(ArrayGuard&& other) noexcept : m_ptr(other.m_ptr) {
        other.m_ptr = nullptr;
    }
    ArrayGuard& operator=(ArrayGuard&& other) noexcept {
        if (this != &other) {
            delete[] m_ptr;
            m_ptr = other.m_ptr;
            other.m_ptr = nullptr;
        }
        return *this;
    }
};

int gzip_inflate(char* compr, int comprLen, char* uncompr, int uncomprLen)
{
    z_stream d_stream;
    d_stream.zalloc = Z_NULL;
    d_stream.zfree = Z_NULL;
    d_stream.opaque = Z_NULL;

    d_stream.next_in = (unsigned char*)compr;
    d_stream.avail_in = comprLen;
    d_stream.next_out = (unsigned char*)uncompr;
    d_stream.avail_out = uncomprLen;

    int err = inflateInit2(&d_stream, 16 + MAX_WBITS);
    if (err != Z_OK) return err;

    while (err != Z_STREAM_END) {
        err = inflate(&d_stream, Z_NO_FLUSH);
        if (err != Z_OK && err != Z_STREAM_END) {
            inflateEnd(&d_stream);
            return err;
        }
    }

    err = inflateEnd(&d_stream);
    return err;
}

#define CHUNK 16384

int compressToGzip(const char* input, int inputSize, char* output, int outputSize)
{
    z_stream zs;
    zs.zalloc = Z_NULL;
    zs.zfree = Z_NULL;
    zs.opaque = Z_NULL;
    zs.avail_in = (uInt)inputSize;
    zs.next_in = (Bytef*)input;
    zs.avail_out = (uInt)outputSize;
    zs.next_out = (Bytef*)output;

    deflateInit2(&zs, Z_DEFAULT_COMPRESSION, Z_DEFLATED, 15 | 16, 8, Z_DEFAULT_STRATEGY);
    deflate(&zs, Z_FINISH);
    deflateEnd(&zs);
    return (int)zs.total_out;
}

int CVWrite(string path, cv::Mat src, int compression)
{
    if (src.empty()) {
        cerr << "Error: Source image is empty" << endl;
        return static_cast<int>(FileError::InvalidFormat);
    }

    ofstream outFile(path, ios::out | ios::binary);
    if (!outFile) {
        cerr << "Error: Cannot open file for writing: " << path << endl;
        return static_cast<int>(FileError::FileNotFound);
    }

    CustomFileHeader fileHeader;
    fileHeader.Version = 0;
    fileHeader.Matoffset = sizeof(CustomFileHeader);

    outFile.write((char*)&fileHeader, sizeof(fileHeader));

    CustomFile grifMat;
    grifMat.rows = src.rows;
    grifMat.cols = src.cols;
    grifMat.type = src.type();
    grifMat.srcLen = static_cast<long long>(src.total() * src.elemSize());
    grifMat.compression = compression;

    outFile.write((char*)&grifMat, sizeof(grifMat));

    if (grifMat.compression == 1) {
        const char* istream = (char*)src.data;
        uLongf srcLen = (uLongf)grifMat.srcLen;
        uLongf destLen = compressBound(srcLen);

        // Use RAII guard for automatic cleanup
        char* ostream = (char*)malloc(destLen);
        if (!ostream) {
            cerr << "Error: Memory allocation failed" << endl;
            return static_cast<int>(FileError::MemoryError);
        }
        MallocGuard streamGuard(ostream);

        int res = compress((Bytef*)ostream, &destLen, (Bytef*)istream, srcLen);
        if (res == Z_BUF_ERROR) {
            cerr << "Error: Buffer was too small!" << endl;
            return static_cast<int>(FileError::BufferTooSmall);
        }
        if (res == Z_MEM_ERROR) {
            cerr << "Error: Not enough memory for compression!" << endl;
            return static_cast<int>(FileError::MemoryError);
        }
        if (res != Z_OK) {
            cerr << "Error: Compression failed with code: " << res << endl;
            return static_cast<int>(FileError::CompressionError);
        }

        grifMat.destLen = static_cast<long long>(destLen);
        outFile.write(ostream, grifMat.destLen);
        // streamGuard will automatically free ostream here
    }
    else if (grifMat.compression == 0) {
        outFile.write((char*)src.data, grifMat.srcLen);
    }
    else {
        cerr << "Error: Unsupported compression type: " << grifMat.compression << endl;
        return static_cast<int>(FileError::InvalidFormat);
    }

    outFile.close();
    return static_cast<int>(FileError::Success);
}

cv::Mat CVRead(string path)
{
    ifstream inFile(path, ios::in | ios::binary);
    if (!inFile) {
        cerr << "Error: Cannot open file for reading: " << path << endl;
        return cv::Mat::zeros(0, 0, CV_8UC3);
    }

    CustomFileHeader header;
    inFile.read((char*)&header, sizeof(CustomFileHeader));

    unsigned char expectedName[6] = { 0x43, 0x75, 0x73, 0x74, 0x6f, 0x6d };
    if (memcmp(header.Name, expectedName, sizeof(header.Name)) != 0) {
        cerr << "Error: Invalid file format" << endl;
        return cv::Mat::zeros(0, 0, CV_8UC3);
    }

    inFile.seekg(header.Matoffset, ios::beg);

    CustomFile grifMat;
    inFile.read((char*)&grifMat, sizeof(CustomFile));

    if (grifMat.compression == 1) {
        // Read compressed data
        ArrayGuard<char> i2stream(new char[grifMat.destLen]);
        inFile.read(i2stream.get(), grifMat.destLen);

        // Allocate output buffer
        char* o2stream = (char*)malloc(grifMat.srcLen);
        if (!o2stream) {
            cerr << "Error: Memory allocation failed" << endl;
            return cv::Mat::zeros(0, 0, CV_8UC3);
        }

        uLongf destLen2 = (uLongf)grifMat.destLen;
        uLongf srcLen = (uLongf)grifMat.srcLen;

        int res = uncompress((Bytef*)o2stream, &srcLen, (Bytef*)i2stream.get(), destLen2);
        if (res != Z_OK) {
            cerr << "Error: Decompression failed with code: " << res << endl;
            free(o2stream);
            return cv::Mat::zeros(0, 0, CV_8UC3);
        }

        // Create Mat with custom allocator that will free the memory
        // Note: OpenCV's Mat doesn't take ownership of external data by default
        // We need to use a custom approach to ensure memory is freed
        cv::Mat result(grifMat.rows, grifMat.cols, grifMat.type, o2stream);

        // Clone the data so OpenCV owns it, then free our buffer
        cv::Mat cloned = result.clone();
        free(o2stream);

        return cloned;
    }
    else if (grifMat.compression == 0) {
        // Read uncompressed data
        ArrayGuard<char> data(new char[grifMat.srcLen]);
        inFile.read(data.get(), grifMat.srcLen);

        cv::Mat mat1(grifMat.rows, grifMat.cols, grifMat.type, data.get());
        return mat1.clone(); // Clone to ensure data is copied
    }

    cerr << "Error: Unsupported compression type: " << grifMat.compression << endl;
    return cv::Mat::zeros(0, 0, CV_8UC3);
}

bool IsCustomFile(string path)
{
    ifstream inFile(path, ios::in | ios::binary);
    if (!inFile) {
        return false;
    }

    CustomFileHeader grifheader;
    inFile.read((char*)&grifheader, sizeof(CustomFileHeader));

    // Check for "Custom" signature
    return (memcmp(grifheader.Name, "Custom", 6) == 0);
}

void OsWrite(std::string path, cv::Mat src)
{
    ofstream outFile1(path, ios::out | ios::binary);
    if (!outFile1) {
        cerr << "Error: Cannot open file for writing: " << path << endl;
        return;
    }
    outFile1.write((char*)src.data, src.total() * src.elemSize());
    outFile1.close();
}

void OsWrite1(std::string path, cv::Mat src)
{
    ofstream outFile1(path, ios::out | ios::binary);
    if (!outFile1) {
        cerr << "Error: Cannot open file for writing: " << path << endl;
        return;
    }
    outFile1 << src;
    outFile1.close();
}
