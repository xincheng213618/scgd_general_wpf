#ifndef _UTILCOM_H_
#define _UTILCOM_H_

#include<string>
#include <vector>
#include <memory>


#define MYDLL _declspec(dllexport)
#define EXPORTC extern "C"
#define STDCALL __stdcall

#define ToChar(x)			#@x
#define EnumToStr(val)		#val

std::string Multi2Utf8(const char* str);
MYDLL std::string STDCALL Utf8ToGbk(const char* src_str);
MYDLL std::string STDCALL GbkToUtf8(const char* src_str);

std::string readStringFile(const char* path);

std::string WtoA(const std::wstring& wstr);
std::wstring AtoW(const std::string& str);

std::string  GenerateUUID();

EXPORTC MYDLL void STDCALL FindFilesWithExtensionW(const std::string& directory, const std::string& extension, std::vector<std::string>& vFiles);

EXPORTC MYDLL void STDCALL FindFilesWithExtensionC(const char* directory, const char* extension, std::vector<std::string>& vFiles);

EXPORTC MYDLL void STDCALL MD5Transf(std::string& MD5message, const char* message);

std::string GetDLLBuildTime();
std::string GetDLLVersion();

struct ChImage
{
	UINT nWidth;
	UINT nHeight;
	UINT nChannels;
	UINT nBpp;
	unsigned char* pData;
	std::shared_ptr<unsigned char> psData;
};

template <class Type>
Type stringToNum(const std::string& str)
{
	std::istringstream iss(str);
	Type num;
	iss >> num;
	return num;
};

template <class Type>
Type wstringToNum(const std::wstring& str)
{
	std::wistringstream iss(str);
	Type num;
	iss >> num;
	return num;
};

template <class Type>
std::string NumTostring(Type val)
{
	std::ostringstream iss;
	iss << val;
	return iss.str();
};

template <class Type>
std::wstring NumTowstring(Type val)
{
	std::wostringstream iss;
	iss << val;
	return iss.str();
};

#endif 
