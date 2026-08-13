#include "test_cuda_fusion.h"

#include <Windows.h>

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <iomanip>
#include <iostream>
#include <numeric>
#include <stdexcept>
#include <string>
#include <vector>

#include <nlohmann/json.hpp>
#include <opencv2/opencv.hpp>

#include "../../Native/include/custom_structs.h"

namespace
{
    namespace fs = std::filesystem;
    using json = nlohmann::json;
    using FusionFn = int(__cdecl*)(const char*, HImage*);
    using FusionBatchFn = int(__cdecl*)(const char*, HImage*, int, int*);
    using FreeHImageFn = void(__cdecl*)(unsigned char*);

    struct CapturedImage
    {
        int rows = 0;
        int cols = 0;
        int channels = 0;
        int depth = 0;
        std::vector<unsigned char> pixels;
    };

    struct CallResult
    {
        int code = -999;
        double milliseconds = 0.0;
        CapturedImage image;
    };

    class FusionDll
    {
    public:
        explicit FusionDll(const fs::path& path)
        {
            module_ = LoadLibraryW(path.c_str());
            if (module_ == nullptr) {
                throw std::runtime_error("LoadLibraryW failed for " + path.u8string()
                    + " (Win32 " + std::to_string(GetLastError()) + ")");
            }

            mFusion = load<FusionFn>("M_Fusion");
            cmFusion = load<FusionFn>("CM_Fusion");
            cmFusionAsync = load<FusionFn>("CM_Fusion_Async");
            cmFusionBatch = load<FusionBatchFn>("CM_Fusion_Batch");
            freeHImage = load<FreeHImageFn>("M_FreeHImageData");
        }

        FusionDll(const FusionDll&) = delete;
        FusionDll& operator=(const FusionDll&) = delete;

        ~FusionDll()
        {
            if (module_ != nullptr) {
                FreeLibrary(module_);
            }
        }

        FusionFn mFusion = nullptr;
        FusionFn cmFusion = nullptr;
        FusionFn cmFusionAsync = nullptr;
        FusionBatchFn cmFusionBatch = nullptr;
        FreeHImageFn freeHImage = nullptr;

    private:
        template<typename T>
        T load(const char* name)
        {
            FARPROC address = GetProcAddress(module_, name);
            if (address == nullptr) {
                throw std::runtime_error(std::string("Missing CUDA fusion export: ") + name);
            }
            return reinterpret_cast<T>(address);
        }

        HMODULE module_ = nullptr;
    };

    bool IsCleared(const HImage& image)
    {
        return image.rows == 0
            && image.cols == 0
            && image.channels == 0
            && image.depth == 0
            && image.stride == 0
            && image.pData == nullptr;
    }

    bool Capture(const HImage& source, CapturedImage& destination)
    {
        size_t rowBytes = 0;
        if (source.pData == nullptr
            || !HImageTryGetRowBytes(source.cols, source.channels, source.depth, &rowBytes)
            || source.rows <= 0
            || source.stride < static_cast<int>(rowBytes)) {
            return false;
        }

        destination.rows = source.rows;
        destination.cols = source.cols;
        destination.channels = source.channels;
        destination.depth = source.depth;
        destination.pixels.resize(static_cast<size_t>(source.rows) * rowBytes);

        for (int row = 0; row < source.rows; ++row) {
            std::memcpy(
                destination.pixels.data() + static_cast<size_t>(row) * rowBytes,
                source.pData + static_cast<size_t>(row) * source.stride,
                rowBytes);
        }
        return true;
    }

    CallResult CallFusion(FusionDll& dll, FusionFn function, const std::string& filesJson)
    {
        HImage nativeImage{};
        const auto start = std::chrono::steady_clock::now();
        const int code = function(filesJson.c_str(), &nativeImage);
        const auto end = std::chrono::steady_clock::now();

        CallResult result;
        result.code = code;
        result.milliseconds = std::chrono::duration<double, std::milli>(end - start).count();
        if (code == 0 && !Capture(nativeImage, result.image)) {
            result.code = -998;
        }
        if (nativeImage.pData != nullptr) {
            dll.freeHImage(nativeImage.pData);
        }
        return result;
    }

    uint64_t HashPixels(const CapturedImage& image)
    {
        uint64_t hash = 1469598103934665603ull;
        for (unsigned char value : image.pixels) {
            hash ^= value;
            hash *= 1099511628211ull;
        }
        return hash;
    }

    void PrintTimingSummary(const std::string& label, std::vector<double> times)
    {
        std::sort(times.begin(), times.end());
        const double median = times[times.size() / 2];
        const size_t p95Index = static_cast<size_t>(std::ceil(0.95 * times.size())) - 1;
        const double mean = std::accumulate(times.begin(), times.end(), 0.0) / times.size();
        std::cout << label << "_ms=min:" << times.front()
            << ", median:" << median
            << ", mean:" << mean
            << ", p95:" << times[p95Index]
            << ", max:" << times.back() << std::endl;
    }

    std::string BuildJson(const std::vector<fs::path>& files)
    {
        json paths = json::array();
        for (const fs::path& file : files) {
            paths.push_back(file.u8string());
        }
        return paths.dump();
    }

    cv::Mat CreateBaseImage(int width, int height, int channels)
    {
        cv::Mat image(height, width, channels == 1 ? CV_8UC1 : CV_8UC3);
        cv::RNG rng(0x50801209);
        rng.fill(image, cv::RNG::UNIFORM, 0, 256);

        for (int row = 0; row < height; ++row) {
            for (int col = 0; col < width; ++col) {
                const unsigned char checker = ((row / 13 + col / 17) & 1) ? 53 : 0;
                if (channels == 1) {
                    image.at<unsigned char>(row, col) = static_cast<unsigned char>(
                        (static_cast<int>(image.at<unsigned char>(row, col)) / 2
                            + col * 7 + row * 11 + checker) & 0xff);
                }
                else {
                    cv::Vec3b& pixel = image.at<cv::Vec3b>(row, col);
                    pixel[0] = static_cast<unsigned char>((pixel[0] / 2 + col * 3 + checker) & 0xff);
                    pixel[1] = static_cast<unsigned char>((pixel[1] / 2 + row * 5 + checker) & 0xff);
                    pixel[2] = static_cast<unsigned char>((pixel[2] / 2 + col * 2 + row * 3 + checker) & 0xff);
                }
            }
        }
        return image;
    }

    std::vector<fs::path> CreateFixture(int width, int height, int imageCount, int channels = 3)
    {
        if (width <= 0 || height <= 0 || imageCount <= 0 || (channels != 1 && channels != 3)) {
            throw std::invalid_argument("Invalid CUDA fusion fixture dimensions");
        }

        const fs::path root = fs::temp_directory_path()
            / ("colorvision_cuda_fusion_" + std::to_string(width) + "x" + std::to_string(height)
                + "x" + std::to_string(imageCount) + "c" + std::to_string(channels));
        fs::create_directories(root);

        const cv::Mat base = CreateBaseImage(width, height, channels);
        std::vector<fs::path> files;
        files.reserve(imageCount);

        for (int imageIndex = 0; imageIndex < imageCount; ++imageIndex) {
            cv::Mat blurred;
            const double sigma = 1.4 + static_cast<double>((imageIndex * 3) % 5) * 0.35;
            cv::GaussianBlur(base, blurred, cv::Size(), sigma, sigma, cv::BORDER_REPLICATE);

            const int bandStart = imageIndex * width / imageCount;
            const int bandEnd = (imageIndex + 1) * width / imageCount;
            if (bandEnd > bandStart) {
                base(cv::Rect(bandStart, 0, bandEnd - bandStart, height))
                    .copyTo(blurred(cv::Rect(bandStart, 0, bandEnd - bandStart, height)));
            }

            const fs::path file = root / ("focus_" + std::to_string(imageIndex) + ".bmp");
            if (!cv::imwrite(file.u8string(), blurred)) {
                throw std::runtime_error("Failed to write CUDA fusion fixture: " + file.u8string());
            }
            files.push_back(file);
        }
        return files;
    }

    bool CompareImages(const CapturedImage& reference, const CapturedImage& candidate, const std::string& label)
    {
        if (reference.rows != candidate.rows
            || reference.cols != candidate.cols
            || reference.channels != candidate.channels
            || reference.depth != candidate.depth
            || reference.pixels.size() != candidate.pixels.size()) {
            std::cerr << label << ": metadata mismatch" << std::endl;
            return false;
        }

        uint64_t different = 0;
        uint64_t absoluteDifference = 0;
        int maximumDifference = 0;
        for (size_t i = 0; i < reference.pixels.size(); ++i) {
            const int difference = std::abs(
                static_cast<int>(reference.pixels[i]) - static_cast<int>(candidate.pixels[i]));
            different += difference != 0 ? 1 : 0;
            absoluteDifference += static_cast<uint64_t>(difference);
            maximumDifference = std::max(maximumDifference, difference);
        }

        const double meanDifference = reference.pixels.empty()
            ? 0.0
            : static_cast<double>(absoluteDifference) / static_cast<double>(reference.pixels.size());
        std::cout << label
            << ": differing=" << different << "/" << reference.pixels.size()
            << ", max_abs=" << maximumDifference
            << ", mean_abs=" << std::fixed << std::setprecision(9) << meanDifference
            << std::endl;
        return different == 0;
    }

    FusionFn SelectFunction(FusionDll& dll, const std::string& name)
    {
        if (name == "M_Fusion") {
            return dll.mFusion;
        }
        if (name == "CM_Fusion_Async") {
            return dll.cmFusionAsync;
        }
        if (name == "CM_Fusion") {
            return dll.cmFusion;
        }
        throw std::invalid_argument("Unknown CUDA fusion function: " + name);
    }

    int RunBenchmark(int argc, char* argv[])
    {
        if (argc < 7 || argc > 9) {
            std::cerr << "usage: --cuda-fusion-benchmark <dll> <width> <height> <images> <warm-iterations> [prewarm] [function]" << std::endl;
            return 2;
        }

        const fs::path dllPath = fs::u8path(argv[2]);
        const int width = std::stoi(argv[3]);
        const int height = std::stoi(argv[4]);
        const int imageCount = std::stoi(argv[5]);
        const int iterations = std::stoi(argv[6]);
        const bool prewarm = argc >= 8 && std::string(argv[7]) == "prewarm";
        const std::string functionName = argc >= 9 ? argv[8] : "CM_Fusion";
        if (iterations <= 0) {
            throw std::invalid_argument("warm-iterations must be positive");
        }

        const std::vector<fs::path> files = CreateFixture(width, height, imageCount);
        const std::string filesJson = BuildJson(files);
        FusionDll dll(dllPath);
        FusionFn function = SelectFunction(dll, functionName);

        std::cout << "benchmark_dll=" << dllPath.u8string() << std::endl;
        std::cout << "benchmark_input=" << width << "x" << height << "x3, images=" << imageCount
            << ", warm_iterations=" << iterations << ", function=" << functionName << std::endl;

        if (prewarm) {
            const std::string prewarmJson = BuildJson(CreateFixture(64, 64, std::max(imageCount, 6)));
            const CallResult prewarmResult = CallFusion(dll, function, prewarmJson);
            std::cout << "cold_context_jit_small_ms=" << std::fixed << std::setprecision(3)
                << prewarmResult.milliseconds << ", code=" << prewarmResult.code << std::endl;
            if (prewarmResult.code != 0) {
                return 1;
            }
        }

        const CallResult first = CallFusion(dll, function, filesJson);
        std::cout << "first_target_ms=" << std::fixed << std::setprecision(3) << first.milliseconds
            << ", code=" << first.code << ", hash=0x" << std::hex << HashPixels(first.image) << std::dec << std::endl;
        if (first.code != 0) {
            return 1;
        }

        std::vector<double> warmTimes;
        warmTimes.reserve(iterations);
        for (int iteration = 0; iteration < iterations; ++iteration) {
            const CallResult result = CallFusion(dll, function, filesJson);
            if (result.code != 0 || HashPixels(result.image) != HashPixels(first.image)) {
                std::cerr << "warm iteration " << iteration << " failed or changed output" << std::endl;
                return 1;
            }
            warmTimes.push_back(result.milliseconds);
            std::cout << "warm_ms[" << iteration << "]=" << std::fixed << std::setprecision(3)
                << result.milliseconds << std::endl;
        }

        PrintTimingSummary("warm_summary", warmTimes);
        return 0;
    }

    int RunAbBenchmark(int argc, char* argv[])
    {
        if (argc != 8) {
            std::cerr << "usage: --cuda-fusion-ab-benchmark <reference-dll> <candidate-dll> <width> <height> <images> <iterations>" << std::endl;
            return 2;
        }

        const fs::path referencePath = fs::u8path(argv[2]);
        const fs::path candidatePath = fs::u8path(argv[3]);
        const int width = std::stoi(argv[4]);
        const int height = std::stoi(argv[5]);
        const int imageCount = std::stoi(argv[6]);
        const int iterations = std::stoi(argv[7]);
        if (iterations <= 0) {
            throw std::invalid_argument("iterations must be positive");
        }

        const std::string filesJson = BuildJson(CreateFixture(width, height, imageCount));
        FusionDll reference(referencePath);
        FusionDll candidate(candidatePath);

        const CallResult referencePrewarm = CallFusion(reference, reference.cmFusion, filesJson);
        const CallResult candidatePrewarm = CallFusion(candidate, candidate.cmFusion, filesJson);
        if (referencePrewarm.code != 0 || candidatePrewarm.code != 0
            || !CompareImages(referencePrewarm.image, candidatePrewarm.image, "A/B prewarm output")) {
            return 1;
        }

        std::vector<double> referenceTimes;
        std::vector<double> candidateTimes;
        referenceTimes.reserve(iterations);
        candidateTimes.reserve(iterations);
        const uint64_t expectedHash = HashPixels(referencePrewarm.image);

        auto measure = [&](FusionDll& dll, std::vector<double>& times, const char* label, int iteration) {
            const CallResult result = CallFusion(dll, dll.cmFusion, filesJson);
            if (result.code != 0 || HashPixels(result.image) != expectedHash) {
                throw std::runtime_error(std::string(label) + " output changed at iteration " + std::to_string(iteration));
            }
            times.push_back(result.milliseconds);
            std::cout << label << "_warm_ms[" << iteration << "]="
                << std::fixed << std::setprecision(3) << result.milliseconds << std::endl;
        };

        for (int iteration = 0; iteration < iterations; ++iteration) {
            if ((iteration & 1) == 0) {
                measure(reference, referenceTimes, "reference", iteration);
                measure(candidate, candidateTimes, "candidate", iteration);
            }
            else {
                measure(candidate, candidateTimes, "candidate", iteration);
                measure(reference, referenceTimes, "reference", iteration);
            }
        }

        std::vector<double> referenceSorted = referenceTimes;
        std::vector<double> candidateSorted = candidateTimes;
        std::sort(referenceSorted.begin(), referenceSorted.end());
        std::sort(candidateSorted.begin(), candidateSorted.end());
        const double referenceMedian = referenceSorted[referenceSorted.size() / 2];
        const double candidateMedian = candidateSorted[candidateSorted.size() / 2];
        PrintTimingSummary("reference_warm_summary", referenceTimes);
        PrintTimingSummary("candidate_warm_summary", candidateTimes);
        std::cout << "median_speedup=" << std::fixed << std::setprecision(4)
            << referenceMedian / candidateMedian << "x, reduction="
            << (1.0 - candidateMedian / referenceMedian) * 100.0 << "%" << std::endl;
        return 0;
    }

    int RunComparison(int argc, char* argv[])
    {
        if (argc < 7 || argc > 8) {
            std::cerr << "usage: --cuda-fusion-compare <reference-dll> <candidate-dll> <width> <height> <images> [channels]" << std::endl;
            return 2;
        }

        const fs::path referencePath = fs::u8path(argv[2]);
        const fs::path candidatePath = fs::u8path(argv[3]);
        const int width = std::stoi(argv[4]);
        const int height = std::stoi(argv[5]);
        const int imageCount = std::stoi(argv[6]);
        const int channels = argc == 8 ? std::stoi(argv[7]) : 3;
        const std::string filesJson = BuildJson(CreateFixture(width, height, imageCount, channels));

        FusionDll reference(referencePath);
        FusionDll candidate(candidatePath);
        const CallResult oldCm = CallFusion(reference, reference.cmFusion, filesJson);
        const CallResult newCm = CallFusion(candidate, candidate.cmFusion, filesJson);
        const CallResult newM = CallFusion(candidate, candidate.mFusion, filesJson);
        const CallResult newAsync = CallFusion(candidate, candidate.cmFusionAsync, filesJson);

        if (oldCm.code != 0 || newCm.code != 0 || newM.code != 0 || newAsync.code != 0) {
            std::cerr << "comparison call failed: old=" << oldCm.code << ", CM=" << newCm.code
                << ", M=" << newM.code << ", Async=" << newAsync.code << std::endl;
            return 1;
        }

        bool success = true;
        success = CompareImages(oldCm.image, newCm.image, "old GPU vs candidate CM_Fusion") && success;
        success = CompareImages(newCm.image, newM.image, "candidate CM_Fusion vs M_Fusion") && success;
        success = CompareImages(newCm.image, newAsync.image, "candidate CM_Fusion vs CM_Fusion_Async") && success;
        return success ? 0 : 1;
    }

    bool ExpectFailureClears(FusionDll& dll, FusionFn function, const std::string& input, const std::string& label)
    {
        HImage output{};
        output.rows = 11;
        output.cols = 22;
        output.channels = 3;
        output.depth = 8;
        output.stride = 99;
        output.pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
        const int code = function(input.c_str(), &output);
        const bool success = code < 0 && IsCleared(output);
        std::cout << label << ": code=" << code << ", cleared=" << (IsCleared(output) ? "yes" : "no") << std::endl;
        return success;
    }

    int RunVerification(int argc, char* argv[])
    {
        if (argc != 3) {
            std::cerr << "usage: --cuda-fusion-verify <candidate-dll>" << std::endl;
            return 2;
        }

        FusionDll dll(fs::u8path(argv[2]));
        bool success = true;
        success = ExpectFailureClears(dll, dll.cmFusion, "{", "invalid JSON") && success;
        success = ExpectFailureClears(dll, dll.cmFusion, "[]", "empty list") && success;

        const fs::path missing = fs::temp_directory_path() / "colorvision_cuda_fusion_missing.bmp";
        success = ExpectFailureClears(dll, dll.cmFusion, BuildJson({ missing }), "missing input") && success;
        success = ExpectFailureClears(dll, dll.cmFusionAsync, BuildJson({ missing }), "async missing input") && success;

        HImage batchOutputs[2]{};
        batchOutputs[0].rows = 1;
        batchOutputs[0].pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(1));
        batchOutputs[1].cols = 1;
        batchOutputs[1].pData = reinterpret_cast<unsigned char*>(static_cast<uintptr_t>(2));
        int outputCount = 7;
        const int batchCode = dll.cmFusionBatch(nullptr, batchOutputs, 2, &outputCount);
        const bool batchCleared = batchCode == -1 && outputCount == 0
            && IsCleared(batchOutputs[0]) && IsCleared(batchOutputs[1]);
        std::cout << "batch failure cleanup: code=" << batchCode
            << ", cleared=" << (batchCleared ? "yes" : "no") << std::endl;
        success = batchCleared && success;

        const std::string sixJson = BuildJson(CreateFixture(192, 128, 6));
        const CallResult sixCm = CallFusion(dll, dll.cmFusion, sixJson);
        const CallResult sixM = CallFusion(dll, dll.mFusion, sixJson);
        const CallResult sixAsync = CallFusion(dll, dll.cmFusionAsync, sixJson);
        success = sixCm.code == 0 && sixM.code == 0 && sixAsync.code == 0
            && CompareImages(sixCm.image, sixM.image, "six-image alias consistency")
            && CompareImages(sixCm.image, sixAsync.image, "six-image async consistency")
            && success;

        const std::string grayJson = BuildJson(CreateFixture(97, 65, 7, 1));
        const CallResult grayCm = CallFusion(dll, dll.cmFusion, grayJson);
        const CallResult grayM = CallFusion(dll, dll.mFusion, grayJson);
        const CallResult grayAsync = CallFusion(dll, dll.cmFusionAsync, grayJson);
        success = grayCm.code == 0 && grayM.code == 0 && grayAsync.code == 0
            && CompareImages(grayCm.image, grayM.image, "grayscale alias consistency")
            && CompareImages(grayCm.image, grayAsync.image, "grayscale async consistency")
            && success;

        const fs::path mismatchRoot = fs::temp_directory_path() / "colorvision_cuda_fusion_mismatch";
        fs::create_directories(mismatchRoot);
        const fs::path mismatchA = mismatchRoot / "a.bmp";
        const fs::path mismatchB = mismatchRoot / "b.bmp";
        cv::imwrite(mismatchA.u8string(), CreateBaseImage(64, 64, 3));
        cv::imwrite(mismatchB.u8string(), CreateBaseImage(65, 64, 3));
        success = ExpectFailureClears(dll, dll.cmFusion, BuildJson({ mismatchA, mismatchB }), "mismatched dimensions") && success;

        for (int attempt = 0; attempt < 3; ++attempt) {
            success = ExpectFailureClears(dll, dll.cmFusion, BuildJson({ missing }), "repeated failure cleanup") && success;
        }

        std::cout << "CUDA fusion verification: " << (success ? "PASS" : "FAIL") << std::endl;
        return success ? 0 : 1;
    }
}

int RunCudaFusionCommand(int argc, char* argv[])
{
    try {
        const std::string command = argc > 1 ? argv[1] : "";
        if (command == "--cuda-fusion-benchmark") {
            return RunBenchmark(argc, argv);
        }
        if (command == "--cuda-fusion-compare") {
            return RunComparison(argc, argv);
        }
        if (command == "--cuda-fusion-ab-benchmark") {
            return RunAbBenchmark(argc, argv);
        }
        if (command == "--cuda-fusion-verify") {
            return RunVerification(argc, argv);
        }
        return 2;
    }
    catch (const std::exception& exception) {
        std::cerr << "CUDA fusion test error: " << exception.what() << std::endl;
        return 1;
    }
}
