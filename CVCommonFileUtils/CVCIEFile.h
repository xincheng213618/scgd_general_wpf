#pragma once


#define MYDLL _declspec(dllexport)
#define EXPORTC extern "C"
#define STDCALL __stdcall


EXPORTC MYDLL int STDCALL WriteCVCIE(char* cieFileName, float* exp, int width, int height, int bpp, int channels, const char* data, int dataLen, char* srcFileName);

EXPORTC MYDLL int STDCALL ReadCVCIEHeader(char* cieFileName, int& width, int& height, int& bpp, int& channels, int& dataLen, int& srcFileNameLen);

EXPORTC MYDLL int STDCALL ReadCVCIE(char* cieFileName, float* exp, char* data, int dataLen, char* srcFileName, int srcFileNameLen);