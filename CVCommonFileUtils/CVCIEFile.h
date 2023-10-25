#pragma once


#define MYDLL _declspec(dllexport)
#define EXPORTC extern "C"
#define STDCALL __stdcall


struct CVCIEFileInfo
{
    //图像宽度
    int width;
    //图像高度
    int height;
    //图像位数，8/16/32
    int bpp;
    //图像通道数, 1/3
    int channels;
    //增益
    int gain;
    //曝光时间
    float* exp;
    //源图像文件
    char* srcFileName;
    //源图像文件名缓冲区大小
    int srcFileNameLen;
    //图像数据
    char* data;
    //图像数据大小
    int dataLen;
};

/*
* 写CVCIE文件
* 参数:
* cieFileName ：CVCIE文件名
* exp ：曝光时间数组指针
* width ：图像宽度
* height ：图像高度
* bpp ：图像位数，8/16/32
* channels ：图像通道数
* data ：图像数据
* dataLen ：图像数据大小
* srcFileName ：源图像文件
* 
* 返回值:
* 0 : 写入成功
 -1 : 写入失败
*/
EXPORTC MYDLL int STDCALL WriteCVCIE(char* cieFileName, CVCIEFileInfo fileInfo);

/*
* 读取CVCIE文件头信息
* 返回值:
* 0 : 成功
 -1 : 文件头非法
 -2 : 文件版本非法
 -999 : 文件不存在
*/
EXPORTC MYDLL int STDCALL ReadCVCIEHeader(char* cieFileName, CVCIEFileInfo& fileInfo);

/*
* 读取CVCIE文件信息
* 返回值:
* 0 : 成功
 -1 : 文件头非法
 -2 : 文件版本非法
 -999 : 文件不存在
*/
EXPORTC MYDLL int STDCALL ReadCVCIE(char* cieFileName, CVCIEFileInfo& fileInfo);

/*
* 获取文件大小
* 
* 返回值:
* >0 : 成功
* -1 : 失败,文件为空或文件不存在
*/
EXPORTC MYDLL long STDCALL GetCVCIEFileLength(char* cieFileName);

/*
* 一次读取CVCIE文件
* 返回值:
* 0 : 成功
 -1 : 文件头非法
 -2 : 文件版本非法
 -3 : 数据区长度不够
 -999 : 文件不存在
*/
EXPORTC MYDLL int STDCALL ReadCVCIEByOne(char* cieFileName, CVCIEFileInfo& fileInfo);