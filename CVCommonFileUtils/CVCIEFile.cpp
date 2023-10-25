#include "CVCIEFile.h"
#include "pch.h"

#include <sys/stat.h> // struct stat
#include<iostream>
#include<fstream>
using namespace std;

char ciehd[] = { 'C', 'V', 'C', 'I', 'E' };
int cur_ver = 1;

EXPORTC MYDLL int STDCALL WriteCVCIE(char* cieFileName, CVCIEFileInfo* fileInfo) {
	ofstream file(cieFileName);
	int ret = -1;
	if (file.good()) {
		file.write(ciehd, 5);//标识
		//版本
		file.write((char*)&cur_ver, sizeof(cur_ver));
		file.write((char*)&fileInfo->srcFileNameLen, sizeof(fileInfo->srcFileNameLen));//显示的源图
		file.write(fileInfo->srcFileName, fileInfo->srcFileNameLen);//显示的源图
		//增益
		file.write((char*)&fileInfo->gain, sizeof(fileInfo->gain));
		//通道
		file.write((char*)&fileInfo->channels, sizeof(fileInfo->channels));
		//曝光
		for (int i = 0; i < fileInfo->channels; i++) {
			file.write((char*)&fileInfo->exp[i], sizeof(float));
		}
		//宽
		file.write((char*)&fileInfo->width, sizeof(fileInfo->width));
		//高
		file.write((char*)&fileInfo->height, sizeof(fileInfo->height));
		//位
		file.write((char*)&fileInfo->bpp, sizeof(fileInfo->bpp));
		//数据长度
		file.write((char*)&fileInfo->dataLen, sizeof(fileInfo->dataLen));
		//数据
		file.write(fileInfo->data, fileInfo->dataLen);
		file.flush();
		ret = 0;
	}
	else {
		ret = -1;
	}
	file.close();
	return ret;
}
/*
* 返回值:
* 0 : 成功
 -1 : 文件头非法
 -2 : 文件版本非法
*/
int readHeader(ifstream& file, float* exp, int& width, int& height, int& bpp, int& channels, int& gain, int& dateLen, char* srcFileName, int& srcFileNameLen) {
	char hd[5];
	file.read(hd, 5);
	for (int i = 0; i < 5; i++) {
		if (hd[i] != ciehd[i]) {
			return -1;
		}
	}
	int ver = 0;
	file.read(reinterpret_cast<char*>(&ver), sizeof(int));
	if (ver != cur_ver) {
		return -2;
	}
	//显示的源图
	int len = 0;
	file.read(reinterpret_cast<char*>(&len), sizeof(int));
	if (len > 0 ) {
		if (srcFileNameLen >= len && srcFileName != NULL) { memset(srcFileName, 0, srcFileNameLen); file.read(srcFileName, len); }
		else file.seekg(len, ios::cur);
		srcFileNameLen = len;
	}
	//增益
	file.read(reinterpret_cast<char*>(&gain), sizeof(int));
	//通道
	file.read(reinterpret_cast<char*>(&channels), sizeof(int));
	//曝光
	for (int i = 0; i < channels; i++) {
		file.read(reinterpret_cast<char*>(&exp[i]), sizeof(float));
	}
	//宽
	file.read(reinterpret_cast<char*>(&width), sizeof(int));
	//高
	file.read(reinterpret_cast<char*>(&height), sizeof(int));
	//位
	file.read(reinterpret_cast<char*>(&bpp), sizeof(int));
	//数据长度
	file.read(reinterpret_cast<char*>(&dateLen), sizeof(int));
	return 0;
}

/*
* 返回值:
* 0 : 成功
 -1 : 文件头非法
 -2 : 文件版本非法
 -999 : 文件不存在
*/
EXPORTC MYDLL int STDCALL ReadCVCIEHeader(char* cieFileName, CVCIEFileInfo* fileInfo) {
	ifstream file(cieFileName);
	int ret = -999;
	if (file.good()) {
		float* exp = new float[3];
		fileInfo->exp = NULL;
		fileInfo->data = NULL;
		ret = readHeader(file, exp, fileInfo->width, fileInfo->height, fileInfo->bpp, fileInfo->channels, fileInfo->gain, fileInfo->dataLen, fileInfo->srcFileName, fileInfo->srcFileNameLen);
	}
	else {
		ret = -999;
	}
	file.close();
	return ret;
}

EXPORTC MYDLL long STDCALL GetCVCIEFileLength(char* cieFileName) {
	if (cieFileName == NULL) {
		return -1;
	}
	// 这是一个存储文件(夹)信息的结构体，其中有文件大小和创建时间、访问时间、修改时间等
	struct stat statbuf;
	// 提供文件名字符串，获得文件属性结构体
	if (stat(cieFileName, &statbuf) == 0) {
		// 获取文件大小
		size_t filesize = statbuf.st_size;
		return filesize;
	}
	else {
		return -1;
	}
}

/*
* 返回值:
* 0 : 成功
 -1 : 文件头非法
 -2 : 文件版本非法
 -3 : 数据区长度不够
 -999 : 文件不存在
*/
EXPORTC MYDLL int STDCALL ReadCVCIE(char* cieFileName, CVCIEFileInfo* fileInfo) {
	ifstream file(cieFileName);
	int ret = -999;
	if (file.good()) {
		int _dataLen, _srcFileNameLen = fileInfo->srcFileNameLen;
		ret = readHeader(file, fileInfo->exp, fileInfo->width, fileInfo->height, fileInfo->bpp, fileInfo->channels, fileInfo->gain, _dataLen, fileInfo->srcFileName, _srcFileNameLen);
		if (ret == 0) {
			fileInfo->srcFileNameLen = _srcFileNameLen;
			if (fileInfo->dataLen >= _dataLen) {
				file.read(fileInfo->data, _dataLen);
				fileInfo->dataLen = _dataLen;
			}
			else {
				ret = -3;
			}
		}
	}
	else {
		ret = -999;
	}
	file.close();
	return ret;
}