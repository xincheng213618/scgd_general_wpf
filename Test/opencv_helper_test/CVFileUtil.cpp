#include "CVCIEFile.hpp"
#include <fstream>
#include <iostream>

static const char* MAGIC_HEADER = "CVCIE";
static const int   HEADER_SIZE = 5;

// 简单判断是否为 CVCIE 文件
bool IsCIEFile(const std::string& filePath)
{
    std::ifstream ifs(filePath, std::ios::binary);
    if (!ifs)
        return false;

    char header[HEADER_SIZE] = {};
    ifs.read(header, HEADER_SIZE);
    if (!ifs)
        return false;

    return std::string(header, HEADER_SIZE) == MAGIC_HEADER;
}

// 从路径读取文件全部为 vector<uint8_t>
static bool ReadAllBytes(const std::string& filePath, std::vector<uint8_t>& outBytes)
{
    std::ifstream ifs(filePath, std::ios::binary | std::ios::ate);
    if (!ifs)
        return false;

    std::streamsize size = ifs.tellg();
    if (size <= 0)
        return false;

    ifs.seekg(0, std::ios::beg);
    outBytes.resize(static_cast<size_t>(size));
    if (!ifs.read(reinterpret_cast<char*>(outBytes.data()), size))
        return false;

    return true;
}

// 从字节数组读取 UInt32（小端）
static bool TryReadUInt32(const std::vector<uint8_t>& data, size_t& offset, uint32_t& value)
{
    if (offset + 4 > data.size())
        return false;

    value = static_cast<uint32_t>(data[offset]) |
        (static_cast<uint32_t>(data[offset + 1]) << 8) |
        (static_cast<uint32_t>(data[offset + 2]) << 16) |
        (static_cast<uint32_t>(data[offset + 3]) << 24);
    offset += 4;
    return true;
}

// 从字节数组读取 Int32
static bool TryReadInt32(const std::vector<uint8_t>& data, size_t& offset, int32_t& value)
{
    uint32_t tmp;
    if (!TryReadUInt32(data, offset, tmp))
        return false;
    value = static_cast<int32_t>(tmp);
    return true;
}

// 从字节数组读取 float
static bool TryReadFloat(const std::vector<uint8_t>& data, size_t& offset, float& value)
{
    if (offset + 4 > data.size())
        return false;
    uint32_t tmp =
        static_cast<uint32_t>(data[offset]) |
        (static_cast<uint32_t>(data[offset + 1]) << 8) |
        (static_cast<uint32_t>(data[offset + 2]) << 16) |
        (static_cast<uint32_t>(data[offset + 3]) << 24);
    offset += 4;
    std::memcpy(&value, &tmp, sizeof(float));
    return true;
}

// 从字节数组读取 Int64
static bool TryReadInt64(const std::vector<uint8_t>& data, size_t& offset, int64_t& value)
{
    if (offset + 8 > data.size())
        return false;

    uint64_t tmp =
        static_cast<uint64_t>(data[offset]) |
        (static_cast<uint64_t>(data[offset + 1]) << 8) |
        (static_cast<uint64_t>(data[offset + 2]) << 16) |
        (static_cast<uint64_t>(data[offset + 3]) << 24) |
        (static_cast<uint64_t>(data[offset + 4]) << 32) |
        (static_cast<uint64_t>(data[offset + 5]) << 40) |
        (static_cast<uint64_t>(data[offset + 6]) << 48) |
        (static_cast<uint64_t>(data[offset + 7]) << 56);
    offset += 8;
    std::memcpy(&value, &tmp, sizeof(int64_t));
    return true;
}

/**
 * 解析头部（从字节数组）
 * 返回：>=0 为数据部分起始 offset；<0 失败
 */
int ReadCIEFileHeader(const std::vector<uint8_t>& fileData, CVCIEFile& cvcie)
{
    if (fileData.size() < 9)
        return -1;

    // Magic header
    if (std::string(reinterpret_cast<const char*>(fileData.data()), HEADER_SIZE) != MAGIC_HEADER)
        return -1;

    size_t offset = HEADER_SIZE;

    // Version (uint32)
    uint32_t version = 0;
    if (!TryReadUInt32(fileData, offset, version))
        return -1;
    cvcie.Version = version;
    if (version != 1 && version != 2)
        return -1;

    // fileNameLength (int32)
    int32_t fileNameLen = 0;
    if (!TryReadInt32(fileData, offset, fileNameLen))
        return -1;
    if (fileNameLen < 0 || offset + static_cast<size_t>(fileNameLen) > fileData.size())
        return -1;

    // SrcFileName（按 GBK 解码，简单起见这里当作单字节编码处理）
    if (fileNameLen > 0)
    {
        cvcie.SrcFileName.assign(
            reinterpret_cast<const char*>(fileData.data() + offset),
            static_cast<size_t>(fileNameLen));
        offset += static_cast<size_t>(fileNameLen);
    }

    // Gain (float)
    float gain = 0.0f;
    if (!TryReadFloat(fileData, offset, gain))
        return -1;
    cvcie.Gain = gain;

    // Channels (uint32)
    uint32_t ch = 0;
    if (!TryReadUInt32(fileData, offset, ch))
        return -1;
    cvcie.Channels = static_cast<int>(ch);

    // Exp (float[ch])
    if (offset + cvcie.Channels * 4 > fileData.size())
        return -1;
    cvcie.Exp.resize(cvcie.Channels);
    for (int i = 0; i < cvcie.Channels; ++i)
    {
        float e;
        if (!TryReadFloat(fileData, offset, e))
            return -1;
        cvcie.Exp[i] = e;
    }

    // Cols (uint32)
    uint32_t cols = 0;
    if (!TryReadUInt32(fileData, offset, cols))
        return -1;
    cvcie.Cols = static_cast<int>(cols);

    // Rows (uint32)
    uint32_t rows = 0;
    if (!TryReadUInt32(fileData, offset, rows))
        return -1;
    cvcie.Rows = static_cast<int>(rows);



    // Bpp (uint32)
    uint32_t bpp = 0;
    if (!TryReadUInt32(fileData, offset, bpp))
        return -1;
    cvcie.Bpp = static_cast<int>(bpp);

    return static_cast<int>(offset);
}

/**
 * 读取数据部分（从字节数组）
 */
bool ReadCIEFileData(const std::vector<uint8_t>& fileData, CVCIEFile& fileInfo, int dataStartIndex)
{
    if (dataStartIndex < 0)
        return false;
    size_t offset = static_cast<size_t>(dataStartIndex);
    if (offset >= fileData.size())
        return false;

    // 至少要有长度字段
    if (fileInfo.Version == 2)
    {
        int64_t dataLen = 0;
        if (!TryReadInt64(fileData, offset, dataLen))
            return false;
        if (dataLen <= 0 || offset + static_cast<size_t>(dataLen) > fileData.size())
            return false;

        fileInfo.Data.resize(static_cast<size_t>(dataLen));
        std::memcpy(fileInfo.Data.data(), fileData.data() + offset, static_cast<size_t>(dataLen));
    }
    else
    {
        int32_t dataLen = 0;
        if (!TryReadInt32(fileData, offset, dataLen))
            return false;
        if (dataLen <= 0 || offset + static_cast<size_t>(dataLen) > fileData.size())
            return false;

        fileInfo.Data.resize(static_cast<size_t>(dataLen));
        std::memcpy(fileInfo.Data.data(), fileData.data() + offset, static_cast<size_t>(dataLen));
    }

    return true;
}

/**
 * 统一读取接口（从路径），仿 C# CVFileUtil.Read(string, out CVCIEFile)
 */
bool ReadCIEFile(const std::string& filePath, CVCIEFile& fileInfo)
{
    std::vector<uint8_t> bytes;
    if (!ReadAllBytes(filePath, bytes))
        return false;

    fileInfo.FilePath = filePath;

    int offset = ReadCIEFileHeader(bytes, fileInfo);
    if (offset <= 0)
        return false;

    if (!ReadCIEFileData(bytes, fileInfo, offset))
        return false;

    // 根据扩展名填充 FileExtType（和 C# 中类似）
    if (filePath.find(".cvraw") != std::string::npos)
        fileInfo.FileExtType = CVType::Raw;
    else if (filePath.find(".cvsrc") != std::string::npos)
        fileInfo.FileExtType = CVType::Src;
    else
        fileInfo.FileExtType = CVType::CIE;

    return true;
}

/**
 * 如果是 XYZ 三通道 CIE 文件，读取指定通道（0:X, 1:Y, 2:Z）
 * 类似 C# ReadCVCIEXYZ
 */
bool ReadCVCIEXYZChannel(const std::string& filePath, int channel, CVCIEFile& fileOut)
{
    CVCIEFile tmp;
    if (!ReadCIEFile(filePath, tmp))
        return false;

    if (tmp.Channels <= 1 || channel < 0 || channel >= tmp.Channels)
        return false;

    fileOut = tmp; // 先拷贝元信息
    fileOut.FileExtType = CVType::Raw;
    fileOut.Channels = 1;

    size_t singleLen = static_cast<size_t>(tmp.Cols) * tmp.Rows * (tmp.Bpp / 8);
    if (tmp.Data.size() < singleLen * tmp.Channels)
        return false;

    fileOut.Data.resize(singleLen);
    std::memcpy(fileOut.Data.data(),
        tmp.Data.data() + singleLen * channel,
        singleLen);

    return true;
}