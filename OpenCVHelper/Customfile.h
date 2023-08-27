#pragma once
#include <opencv2/opencv.hpp>
#include <iostream>



typedef struct GrifMatFile
{
    int rows;
    int cols;
    int type;
    int compression;  //0,不压缩; 1,Zlib; 2,gz
    long long srcLen; //Mat.data 的长度
    long long destLen; //无压缩时，destLen =0;
}GrifMatFile;

typedef struct GrifFileHeader
{
    char Name[4] = { 0x67,0x72,0x69,0x66 };
    int Version; //0
    int Matoffset; //直接读取Mat数据的偏移量 int 限制了2G大小，如果需要更多，则需要float or double d但是这回让内存对齐比较麻烦
}GrifFileHeader;


int WriteFile(std::string path, cv::Mat src, int compression = 1);
int WriteFile(std::string path, GrifFile grifFileInfo, cv::Mat src, int compression = 1);
GrifFile ReadFileHeader(std::string path);
cv::Mat ReadFile(std::string FileName);
int GrifToMat(std::string path, cv::Mat& src);
int WriteGrifFile(std::string path, std::string name, cv::Mat src, int x, int y, int z);
int GrifToMatGz(std::string path, cv::Mat& src);
int WriteGrifFileGz(std::string path, std::string name, cv::Mat src, int x, int y, int z);
void OsWrite(std::string path, cv::Mat src);
void OsWrite1(std::string path, cv::Mat src);
int WriteFileCache(std::string path, GrifFile grifFileInfo, cv::Mat src);
int WriteFileCache(std::string path, cv::Mat src);

