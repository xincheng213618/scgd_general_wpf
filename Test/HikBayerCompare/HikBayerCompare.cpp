// Standalone x64 /O2 /MT console utility. Only the vendor headers are needed at build time.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <MvCameraControl.h>

#include <algorithm>
#include <array>
#include <chrono>
#include <cstdint>
#include <cstdlib>
#include <execution>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace fs = std::filesystem;
using Clock = std::chrono::steady_clock;
constexpr unsigned Width = 9568, Height = 6380;
constexpr size_t Pixels = size_t(Width) * Height;
constexpr unsigned Rounds = 12;
constexpr unsigned RowsPerStrip = 16;
static_assert(Width % 2 == 0 && Height % 2 == 0 && RowsPerStrip % 2 == 0);
static_assert(sizeof(void*) == 8, "Build the x64 utility for the MVS x64 runtime.");

static std::string Hex(unsigned value)
{
    std::ostringstream text;
    text << "0x" << std::hex << std::uppercase << std::setw(8) << std::setfill('0') << value;
    return text.str();
}

static void Check(int code, const char* operation)
{
    if (code != 0) throw std::runtime_error(std::string(operation) + " failed, MVS code " + Hex(code));
}

struct Mvs
{
    HMODULE module = nullptr;
    void* handle = nullptr;
    bool initialized = false;
    decltype(&MV_CC_Finalize) finalize = nullptr;
    decltype(&MV_CC_DestroyHandle) destroy = nullptr;
    decltype(&MV_CC_ConvertPixelTypeEx) convert = nullptr;

    template<typename Function> Function Load(const char* name)
    {
        auto address = GetProcAddress(module, name);
        if (!address) throw std::runtime_error(std::string("Installed MVS runtime does not export ") + name);
        return reinterpret_cast<Function>(address);
    }

    void Start(std::ostream& log, const fs::path& sdkDirectory)
    {
        DWORD lastError = ERROR_FILE_NOT_FOUND;
        std::vector<fs::path> directories;
        if (!sdkDirectory.empty()) directories.push_back(fs::absolute(sdkDirectory));
        for (auto variable : { L"CommonProgramFiles(x86)", L"CommonProgramFiles" })
        {
            const wchar_t* common = _wgetenv(variable);
            if (sdkDirectory.empty() && common && *common)
                directories.push_back(fs::path(common) / L"MVS/Runtime/Win64_x64");
        }
        // MediaProcess is loaded by the SDK on demand. Use the same runtime for both DLLs.
        if (!SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS))
            throw std::runtime_error("Could not set the diagnostic process DLL search path.");
        for (auto directory : directories)
        {
            directory.make_preferred();
            const auto file = directory / L"MvCameraControl.dll";
            if (!fs::exists(file)) continue;
            auto cookie = AddDllDirectory(directory.c_str());
            if (!cookie) throw std::runtime_error("Cannot register MVS runtime directory.");
            module = LoadLibraryExW(file.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS);
            if (module)
            {
                log << "MvsDll=" << file.u8string() << '\n';
                break;
            }
            lastError = GetLastError();
            RemoveDllDirectory(cookie);
        }
        if (!module) throw std::runtime_error("Cannot load installed x64 MVS runtime, Win32 error " + std::to_string(lastError));
        finalize = Load<decltype(finalize)>("MV_CC_Finalize");
        destroy = Load<decltype(destroy)>("MV_CC_DestroyHandle");
        convert = Load<decltype(convert)>("MV_CC_ConvertPixelTypeEx");
        const auto version = Load<decltype(&MV_CC_GetSDKVersion)>("MV_CC_GetSDKVersion")();
        log << "MvsVersion=" << Hex(version) << std::endl;
        Check(Load<decltype(&MV_CC_Initialize)>("MV_CC_Initialize")(), "Initialize");
        initialized = true;
        MV_CC_DEVICE_INFO descriptor{};
        descriptor.nTLayerType = MV_GIGE_DEVICE;
        descriptor.nMajorVer = 1;
        // The descriptor has no IP, MAC or serial number. Conversion only requires an unopened handle.
        Check(Load<decltype(&MV_CC_CreateHandle)>("MV_CC_CreateHandle")(&handle, &descriptor), "Create offline handle");
        Check(Load<decltype(&MV_CC_SetBayerCvtQuality)>("MV_CC_SetBayerCvtQuality")(handle, 3), "Set Bayer quality 3");
    }

    ~Mvs()
    {
        if (handle && destroy) destroy(handle);
        if (initialized) finalize();
        if (module) FreeLibrary(module);
    }
};

static std::vector<uint16_t> Input(const fs::path& raw)
{
    std::vector<uint16_t> plane(Pixels);
    if (!raw.empty())
    {
        if (fs::file_size(raw) != Pixels * sizeof(uint16_t))
            throw std::runtime_error("Raw input must be exactly 9568 x 6380 little-endian BayerRG16, without header or padding.");
        std::ifstream stream(raw, std::ios::binary);
        if (!stream.read(reinterpret_cast<char*>(plane.data()), std::streamsize(Pixels * sizeof(uint16_t))))
            throw std::runtime_error("Cannot read BayerRG16 input.");
        return plane;
    }
    // RGGB colour bars with unequal channels, low bits, clipped edges and deterministic full-range noise.
    const uint16_t bars[4][3] = { { 65535, 103, 7 }, { 19, 65003, 37 }, { 5, 71, 65533 }, { 32771, 1531, 49159 } };
    uint32_t random = 0xbba708f3;
    for (unsigned y = 0; y < Height; ++y)
        for (unsigned x = 0; x < Width; ++x)
        {
            const unsigned channel = (y & 1) + (x & 1);
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            uint16_t value;
            if (y < Height / 4) value = bars[(x / 47 + y / 53) % 4][channel];
            else if (y < Height / 2) value = static_cast<uint16_t>((x * 13 + y * 7 + channel * 3) & 1023);
            else if (y < Height * 3 / 4) value = ((x / 5 + y / 3 + channel) & 1) ? 65535 : 0;
            else value = static_cast<uint16_t>(random);
            plane[size_t(y) * Width + x] = value;
        }
    return plane;
}

struct Timing { double sdk, swap, total; };

static Timing Convert(const Mvs& mvs, std::vector<uint16_t>& source, std::vector<uint16_t>& output, bool directBgr)
{
    MV_CC_PIXEL_CONVERT_PARAM_EX args{};
    args.nWidth = Width;
    args.nHeight = Height;
    args.enSrcPixelType = PixelType_Gvsp_BayerRG16;
    args.pSrcData = reinterpret_cast<unsigned char*>(source.data());
    args.nSrcDataLen = static_cast<unsigned>(source.size() * sizeof(uint16_t));
    args.enDstPixelType = directBgr ? PixelType_Gvsp_BGR16_Packed : PixelType_Gvsp_RGB16_Packed;
    args.pDstBuffer = reinterpret_cast<unsigned char*>(output.data());
    args.nDstBufferSize = static_cast<unsigned>(output.size() * sizeof(uint16_t));
    const auto begin = Clock::now();
    Check(mvs.convert(mvs.handle, &args), directBgr ? "Convert to BGR16" : "Convert to RGB16");
    if (args.nDstLen != args.nDstBufferSize) throw std::runtime_error("SDK returned unexpected output length.");
    const auto converted = Clock::now();
    if (!directBgr)
        for (size_t i = 0; i < output.size(); i += 3) std::swap(output[i], output[i + 2]);
    const auto end = directBgr ? converted : Clock::now();
    using Ms = std::chrono::duration<double, std::milli>;
    return { Ms(converted - begin).count(), Ms(end - converted).count(), Ms(end - begin).count() };
}

static double Median(std::vector<double> values)
{
    std::sort(values.begin(), values.end());
    return (values[values.size() / 2] + values[(values.size() - 1) / 2]) / 2.0;
}

static double ConvertRggbPairs(const std::vector<uint16_t>& plane, std::vector<uint16_t>& output, const std::vector<unsigned>& firstRows)
{
    // Observed for BayerRG16, Q3, even dimensions, and default ISP settings on the offline handle.
    // Use the vendor RGB16 path as the independent reference for this experiment.
    const auto start = Clock::now();
    std::for_each(std::execution::par, firstRows.begin(), firstRows.end(), [&](unsigned firstRow) {
        const unsigned end = std::min(Height, firstRow + RowsPerStrip);
        for (unsigned y = firstRow; y < end; y += 2)
        {
            const uint16_t* upper = plane.data() + size_t(y) * Width;
            const uint16_t* lower = upper + Width;
            uint16_t* outUpper = output.data() + size_t(y) * Width * 3;
            uint16_t* outLower = outUpper + Width * 3;
            for (unsigned x = 0; x < Width; x += 2)
            {
                const uint16_t red = upper[x], greenUpper = upper[x + 1];
                const uint16_t greenLower = lower[x], blue = lower[x + 1];
                outUpper[0] = outUpper[3] = blue;
                outUpper[1] = outUpper[4] = greenUpper;
                outUpper[2] = outUpper[5] = red;
                outLower[0] = outLower[3] = blue;
                outLower[1] = outLower[4] = greenLower;
                outLower[2] = outLower[5] = red;
                outUpper += 6;
                outLower += 6;
            }
        }
    });
    return std::chrono::duration<double, std::milli>(Clock::now() - start).count();
}

static bool VerifyPixels(std::ostream& log, const char* candidate, const std::vector<uint16_t>& output, const std::vector<uint16_t>& expected)
{
    size_t different = 0;
    int maxDifference = 0;
    for (size_t i = 0; i < output.size(); ++i)
    {
        if (output[i] == expected[i]) continue;
        if (different++ == 0)
            log << "Candidate=" << candidate << " FirstMismatch X=" << i / 3 % Width << " Y=" << i / 3 / Width
                << " BGRChannel=" << i % 3 << " Expected=" << expected[i] << " Actual=" << output[i] << '\n';
        maxDifference = std::max(maxDifference, std::abs(int(output[i]) - int(expected[i])));
    }
    log << "Candidate=" << candidate << " ComparedValues=" << output.size() << " DifferentValues=" << different
        << " MaxAbsDifference=" << maxDifference << std::endl;
    return different == 0;
}

static bool Compare(std::ostream& log, const fs::path& raw, const fs::path& sdkDirectory)
{
    log << "Width=" << Width << " Height=" << Height << " Source=BayerRG16 Quality=3 Input="
        << (raw.empty() ? "synthetic-rggb-v1" : raw.filename().u8string()) << std::endl;
    Mvs mvs;
    mvs.Start(log, sdkDirectory);
    auto plane = Input(raw);
    std::vector<unsigned> firstRows;
    for (unsigned y = 0; y < Height; y += RowsPerStrip) firstRows.push_back(y);
    // Distinct sentinels also detect a converter that leaves a border unwritten.
    std::vector<uint16_t> output(Pixels * 3, 0x5678);
    bool directExact, pairsExact;
    {
        std::vector<uint16_t> expected(output.size(), 0x1234);
        Convert(mvs, plane, expected, false);
        Convert(mvs, plane, output, true);
        directExact = VerifyPixels(log, "SdkDirectBgr", output, expected);
        std::fill(output.begin(), output.end(), 0x9abc);
        ConvertRggbPairs(plane, output, firstRows);
        pairsExact = VerifyPixels(log, "ExperimentalRggbPairs", output, expected);
    }
    // After releasing the comparison buffer, warm every path on the output used for timed rounds.
    Convert(mvs, plane, output, false);
    Convert(mvs, plane, output, true);
    ConvertRggbPairs(plane, output, firstRows);
    std::vector<double> rgb, swap, original, direct, savings, pairs, pairSavings;
    std::array<char, 3> order = { 'A', 'B', 'C' };
    log << "A=SdkRgbThenSwap B=SdkDirectBgr C=ExperimentalRggbPairs OfflineHandle=default-ISP" << std::endl;
    log << std::fixed << std::setprecision(3);
    for (unsigned i = 0; i < Rounds; ++i)
    {
        Timing a{}, b{};
        double pairMs = 0;
        for (const char path : order)
        {
            if (path == 'A') a = Convert(mvs, plane, output, false);
            else if (path == 'B') b = Convert(mvs, plane, output, true);
            else pairMs = ConvertRggbPairs(plane, output, firstRows);
        }
        rgb.push_back(a.sdk); swap.push_back(a.swap); original.push_back(a.total);
        direct.push_back(b.total); savings.push_back(a.total - b.total);
        pairs.push_back(pairMs); pairSavings.push_back(a.total - pairMs);
        log << "Round=" << i + 1 << " Order=" << std::string(order.begin(), order.end()) << " SdkRgbMs=" << a.sdk
            << " SwapMs=" << a.swap << " OriginalMs=" << a.total << " DirectBgrMs=" << b.total << " SavedMs=" << savings.back()
            << " ExperimentalPairsMs=" << pairMs << " ExperimentalSavedMs=" << pairSavings.back() << std::endl;
        std::next_permutation(order.begin(), order.end());
    }
    log << "Median SdkRgbMs=" << Median(rgb) << " SwapMs=" << Median(swap) << " OriginalMs=" << Median(original)
        << " DirectBgrMs=" << Median(direct) << " PairedSavedMs=" << Median(savings) << " ExperimentalPairsMs=" << Median(pairs)
        << " ExperimentalPairedSavedMs=" << Median(pairSavings) << std::endl;
    return directExact && pairsExact;
}

int wmain(int argc, wchar_t** argv)
{
    std::ofstream log;
    try
    {
        fs::path directory, raw, sdkDirectory;
        for (int i = 1; i < argc; ++i)
        {
            std::wstring option = argv[i];
            if (i + 1 == argc) throw std::runtime_error("Missing option value.");
            const wchar_t* value = argv[++i];
            if (option == L"--log-dir") directory = value;
            else if (option == L"--raw") raw = value;
            else if (option == L"--sdk-dir") sdkDirectory = value;
            else throw std::runtime_error("Usage: HikBayerCompare.exe [--log-dir directory] [--raw file] [--sdk-dir directory]");
        }
        if (directory.empty())
        {
            const auto roaming = _wgetenv(L"APPDATA");
            if (!roaming || !*roaming) throw std::runtime_error("APPDATA is unavailable.");
            directory = fs::path(roaming) / L"ColorVision/Log";
        }
        fs::create_directories(directory);
        SYSTEMTIME now{};
        GetLocalTime(&now);
        wchar_t name[96]{};
        swprintf_s(name, L"HikBayerCompare_%04u%02u%02u_%02u%02u%02u_%03u_%lu.log", now.wYear, now.wMonth, now.wDay,
            now.wHour, now.wMinute, now.wSecond, now.wMilliseconds, GetCurrentProcessId());
        const auto path = directory / name;
        log.exceptions(std::ios::failbit | std::ios::badbit);
        log.open(path);
        std::wcout << L"Running offline Bayer comparison. Log: " << path.c_str() << std::endl;
        const bool exact = Compare(log, raw, sdkDirectory);
        log << (exact ? "Status=ok" : "Status=pixel-mismatch") << std::endl;
        return exact ? 0 : 2;
    }
    catch (const std::exception& e)
    {
        std::cerr << "Bayer comparison failed: " << e.what() << std::endl;
        if (log.is_open() && log.good()) log << "Status=error Error=" << e.what() << std::endl;
        return 1;
    }
}
