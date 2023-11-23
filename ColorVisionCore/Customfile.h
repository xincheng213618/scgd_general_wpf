#pragma once

#ifdef COLORVISIONCORE_EXPORTS
#define OPENCV_API __declspec(dllexport)
#else
#define OPENCV_API __declspec(dllimport)
#endif

struct HImage
{
    int rows;
    int cols;
    int type;
    char* pData;
};

typedef struct CustomMatFile
{
    int rows;
    int cols;
    int type;
    int compression; //0,不压缩; 1,Zlib; 2,gz
    long long srcLen; //Mat.data 的长度
    long long destLen; //无压缩时，destLen =0;
}CustomMatFile;

typedef struct  CustomFileHeader
{
    char Name[6] = { 0x43,0x75,0x73,0x74,0x6f,0x6d }; //Custom
    int Version; //0
    int Matoffset; //直接读取Mat数据的偏移量 int 限制了2G大小，如果需要更多，则需要float or double d但是这回让内存对齐比较麻烦
}CustomFileHeader;





extern "C" OPENCV_API int CVWrite(char* path, HImage src, int compression = 0);
extern "C" OPENCV_API int CVRead(char* FileName, HImage src);



