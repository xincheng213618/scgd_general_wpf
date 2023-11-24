#pragma once

#ifdef COLORVISIONCORE_EXPORTS
#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif

typedef struct HImage
{
    int rows;
    int cols;
    int channels;
    int depth; //Bpp
    int type()  const { return (((depth) & ((1 << 3) - 1)) + (((channels) - 1) << 3)); }
    int elemSize() const { return  ((((((((depth) & ((1 << 3) - 1)) + (((channels)-1) << 3))) & ((512 - 1) << 3)) >> 3) + 1) * ((0x28442211 >> (((((depth) & ((1 << 3) - 1)) + (((channels)-1) << 3))) & ((1 << 3) - 1)) * 4) & 15)); }
    char* pData;
}HImage;

typedef struct CustomFile
{
    int rows;
    int cols;
    int channels;
    int depth;
    int compression; //0,不压缩; 1,Zlib; 2,gz
    long long srcLen; //Mat.data 的长度
    long long destLen; //无压缩时，destLen =0;
}CustomFile;

typedef struct  CustomFileHeader
{
    char Name[6] = { 0x43,0x75,0x73,0x74,0x6f,0x6d }; //Custom
    int Version; //0
    int Matoffset; //直接读取Mat数据的偏移量 int 限制了2G大小，如果需要更多，则需要float or double d但是这回让内存对齐比较麻烦
}CustomFileHeader;



extern "C" COLORVISIONCORE_API int CVWrite(const char* path, HImage src, int compression = 0);
extern "C" COLORVISIONCORE_API int CVRead(const char* FileName, HImage* src);



