#include "pch.h"
#include "Utilcom.h"
#include <stringapiset.h>
#include <wincodec.h>
#include <Shlwapi.h>
#include <io.h>
#include <random>
#include <iomanip>
#include <sstream>

std::string Multi2Utf8(const char* str)
{
	std::string result;
	WCHAR* strSrc;
	LPSTR szRes;

	int i = MultiByteToWideChar(CP_UTF8, 0, str, -1, NULL, 0);
	strSrc = new WCHAR[i + 1];
	MultiByteToWideChar(CP_UTF8, 0, str, -1, strSrc, i);

	i = WideCharToMultiByte(CP_ACP, 0, strSrc, -1, NULL, 0, NULL, NULL);
	szRes = new CHAR[i + 1];
	WideCharToMultiByte(CP_ACP, 0, strSrc, -1, szRes, i, NULL, NULL);

	result = szRes;
	delete[]strSrc;
	delete[]szRes;
	return result;
}

std::string Utf8ToGbk(const char* src_str)
{
	int len = MultiByteToWideChar(CP_UTF8, 0, src_str, -1, NULL, 0);
	wchar_t* wszGBK = new wchar_t[len + 1];
	memset(wszGBK, 0, len * 2 + 2);
	MultiByteToWideChar(CP_UTF8, 0, src_str, -1, wszGBK, len);
	len = WideCharToMultiByte(CP_ACP, 0, wszGBK, -1, NULL, 0, NULL, NULL);
	char* szGBK = new char[len + 1];
	memset(szGBK, 0, len + 1);
	WideCharToMultiByte(CP_ACP, 0, wszGBK, -1, szGBK, len, NULL, NULL);
	std::string strTemp(szGBK);
	if (wszGBK) delete[] wszGBK;
	if (szGBK) delete[] szGBK;
	return strTemp;
}

std::string GbkToUtf8(const char* src_str)
{
	int len = MultiByteToWideChar(CP_ACP, 0, src_str, -1, NULL, 0);
	wchar_t* wstr = new wchar_t[len + 1];
	memset(wstr, 0, len + 1);
	MultiByteToWideChar(CP_ACP, 0, src_str, -1, wstr, len);
	len = WideCharToMultiByte(CP_UTF8, 0, wstr, -1, NULL, 0, NULL, NULL);
	char* str = new char[len + 1];
	memset(str, 0, len + 1);
	WideCharToMultiByte(CP_UTF8, 0, wstr, -1, str, len, NULL, NULL);
	std::string strTemp = str;
	if (wstr) delete[] wstr;
	if (str) delete[] str;
	return strTemp;
}

std::string readStringFile(const char* path)
{
	FILE* file;
	errno_t err = fopen_s(&file, path, "rb");
	if (err != 0)
		return std::string("");
	fseek(file, 0, SEEK_END);
	long size = ftell(file);
	fseek(file, 0, SEEK_SET);
	std::string text;
	char* buffer = new char[size + 1];
	buffer[size] = 0;
	if (fread(buffer, 1, size, file) == (unsigned long)size)
	{
		text = Utf8ToGbk(buffer);
	}
	fclose(file);
	delete[] buffer;
	return text;
}

std::string WtoA(const std::wstring& wstr)
{
	int len = WideCharToMultiByte(CP_ACP, 0, wstr.c_str(), (int)wstr.size(), NULL, 0, NULL, NULL);
	if (0 == len)  
		return "";

	std::vector<char> buf;
	buf.resize(len);
	WideCharToMultiByte(CP_ACP, 0, wstr.c_str(), (int)wstr.size(), &buf[0], len, NULL, NULL);
	return std::string(buf.begin(), buf.end());
}

std::wstring AtoW(const std::string& str)
{
	int len = MultiByteToWideChar(CP_ACP, 0, str.c_str(), (int)str.size(), NULL, 0);
	if (0 == len)
		return L"";

	std::vector<wchar_t> buf;
	buf.resize(len);
	MultiByteToWideChar(CP_ACP, 0, str.c_str(), -1, &buf[0], len);
	return std::wstring(buf.begin(), buf.end());
}

std::string GenerateUUID()
{
	std::random_device rd;
	std::mt19937 gen(rd());
	std::uniform_int_distribution<uint32_t> dis(0, 0xFFFFFFFF);

	std::stringstream ss;
	for (int i = 0; i < 4; ++i) {
		ss << std::setfill('0') << std::setw(8) << std::hex << dis(gen);
		if (i < 3) {
			ss << '-';
		}
	}

	return ss.str();
}

std::string GetDLLPath() 
{
	char moduleFileName[MAX_PATH] = { 0 };
	GetModuleFileNameA(NULL, moduleFileName, MAX_PATH);
	PathRemoveFileSpecA(moduleFileName);
	strcat_s(moduleFileName, "\\cvCamera.dll");
	return moduleFileName;
}

std::string GetDLLBuildTime()
{
	HANDLE hFile = CreateFileA(GetDLLPath().c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
	if (hFile != INVALID_HANDLE_VALUE) {
		FILETIME creationTime, localCreationTime;
		if (GetFileTime(hFile, &creationTime, NULL, NULL)) {
			FileTimeToLocalFileTime(&creationTime, &localCreationTime);
			SYSTEMTIME st;
			FileTimeToSystemTime(&localCreationTime, &st);
			CloseHandle(hFile);
			char buffer[100];
			sprintf_s(buffer, "%04d-%02d-%02d %02d:%02d:%02d",
				st.wYear, st.wMonth, st.wDay,
				st.wHour, st.wMinute, st.wSecond);
			return std::string(buffer);
		}
		CloseHandle(hFile);
	}
	return "Unknown";
}

