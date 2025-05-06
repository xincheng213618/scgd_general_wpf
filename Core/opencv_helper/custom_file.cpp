// ConsoleApplication1.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//
#include "pch.h"
#include "custom_file.h"
#include <zlib.h>
#include <fstream>
#include <iostream>
#include <opencv2/opencv.hpp>
#include<stdio.h>
#include<stdlib.h>
#include<string.h>
#include <Windows.h>

using namespace std;

int gzip_inflate(char* compr, int comprLen, char* uncompr, int uncomprLen)
{
    int err;
    z_stream d_stream; /* decompression stream */

    d_stream.zalloc = (alloc_func)0;
    d_stream.zfree = (free_func)0;
    d_stream.opaque = (voidpf)0;

    d_stream.next_in = (unsigned char*)compr;
    d_stream.avail_in = comprLen;

    d_stream.next_out = (unsigned char*)uncompr;
    d_stream.avail_out = uncomprLen;

    err = inflateInit2(&d_stream, 16 + MAX_WBITS);
    if (err != Z_OK) return err;

    while (err != Z_STREAM_END) err = inflate(&d_stream, Z_NO_FLUSH);

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

    // hard to believe they don't have a macro for gzip encoding, "Add 16" is the best thing zlib can do:
    // "Add 16 to windowBits to write a simple gzip header and trailer around the compressed data instead of a zlib wrapper"
    deflateInit2(&zs, Z_DEFAULT_COMPRESSION, Z_DEFLATED, 15 | 16, 8, Z_DEFAULT_STRATEGY);
    deflate(&zs, Z_FINISH);
    deflateEnd(&zs);
    return zs.total_out;
}



int CVWrite(string path , cv::Mat src, int compression) {
    ofstream outFile(path, ios::out | ios::binary);

    CustomFileHeader fileHeader;
    fileHeader.Version = 0;
    fileHeader.Matoffset = sizeof(CustomFileHeader);

    outFile.write((char*)&fileHeader, sizeof(fileHeader));


    CustomFile grifMat;
    grifMat.rows = src.rows;
    grifMat.cols = src.cols;
    grifMat.type = src.type();
    grifMat.srcLen = src.total() * src.elemSize();
    grifMat.compression = compression;

    outFile.write((char*)&grifMat, sizeof(grifMat));

    if (grifMat.compression == 1) {


        const char* istream = (char*)src.data;
        uLongf srcLen = (uLongf)grifMat.srcLen;      // +1 for the trailing `\0`
        uLongf destLen = compressBound(srcLen); // this is how you should estimate size 
        char* ostream = (char*)malloc(destLen);
        int res = compress((Bytef*)ostream, &destLen, (Bytef*)istream, srcLen);
        if (res == Z_BUF_ERROR) {
            printf("Buffer was too small!\n");
            return -1;
        }
        if (res == Z_MEM_ERROR) {
            printf("Not enough memory for compression!\n");
            return -2;
        }
        //char* ostream1 = (char*)malloc(destLen);
        //uLongf destLen1 = compressBound(srcLen); // this is how you should estimate size 

        //int b = compressToGzip(istream, srcLen, ostream1, destLen1);

        grifMat.destLen = destLen;
        outFile.write(ostream, grifMat.destLen);
    }
    else if (grifMat.compression == 0)
    {
        outFile.write((char*)src.data, grifMat.srcLen);
    }
    outFile.close();
    return 0;

}



 cv::Mat CVRead(string path) {
    ifstream inFile(path, ios::in | ios::binary); //二进制读方式打开
    if (!inFile) {
        return cv::Mat::zeros(0, 0, CV_8UC3);
    }
    CustomFileHeader header;
    inFile.read((char*)&header, sizeof(CustomFileHeader));
    unsigned char expectedName[6] = { 0x43, 0x75, 0x73, 0x74, 0x6f, 0x6d };
    if (memcmp(header.Name, expectedName, sizeof(header.Name))!=0)
    {
        return cv::Mat::zeros(0, 0, CV_8UC3);
    }
    inFile.seekg(header.Matoffset, ios::beg);

    CustomFile grifMat;
    inFile.read((char*)&grifMat, sizeof(CustomFile));
    if (grifMat.compression == 1)
    {
        char* i2stream = new char[grifMat.destLen];
        // Read the pixels from the stringstream
        inFile.read(i2stream, grifMat.destLen);

        char* o2stream = (char*)malloc(grifMat.srcLen);
        uLongf destLen2 = (uLongf)grifMat.destLen;
        uLongf srcLen = (uLongf)grifMat.srcLen;

        int des = uncompress((Bytef*)o2stream, &srcLen, (Bytef*)i2stream, destLen2);
        return cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, o2stream);
    }
    else if (grifMat.compression == 0)
    {
        char* data = new char[grifMat.srcLen];
        // Read the pixels from the stringstream
        inFile.read(data, grifMat.srcLen);
        cv::Mat mat1 = cv::Mat(grifMat.rows, grifMat.cols, grifMat.type, data);
        return mat1;
    }
    return cv::Mat::zeros(0, 0, CV_8UC3);
}


bool IsCustomFile(string path) {

    ifstream inFile(path, ios::in | ios::binary); //二进制读方式打开
    if (!inFile) {
        return  false;
    }
    CustomFileHeader grifheader;
    inFile.read((char*)&grifheader, sizeof(CustomFileHeader));
    if (std::string("custom").compare(grifheader.Name))
    {
        return true;
    }
    return false;
}


void OsWrite(std::string path, cv::Mat src) {
    ofstream outFile1(path, ios::out | ios::binary);
    outFile1.write((char*)src.data, src.total() * src.elemSize());
    outFile1.close();
}
void OsWrite1(std::string path, cv::Mat src) {
    ofstream outFile1(path, ios::out | ios::binary);
    outFile1 << src;
    outFile1.close();
}
