#include "rgb_cross.h"
#include <chrono>
#include <cmath>
#include <cstring>
#include <fstream>
#include <iostream>
#include <random>
#include <vector>
using namespace cvnative::rgb_cross;
using json = nlohmann::json;
void Check(bool b, const char *m)
{
    if (!b)
        throw std::runtime_error(m);
}
std::vector<std::uint8_t> Synthetic(int size, bool missing = false)
{
    std::vector<std::uint8_t> p(static_cast<std::size_t>(size) * size * 6);
    for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            for (int channel = 0; channel < 3; channel++)
            {
                bool lit = false;
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        if (missing && r == 1 && c == 2 && channel == 0)
                            continue;
                        int dx = x - (100 + c * 100 + channel - 1), dy = y - (100 + r * 100);
                        lit |= (std::abs(dx) <= 1 && std::abs(dy) <= 16) || (std::abs(dy) <= 1 && std::abs(dx) <= 16);
                    }
                unsigned v = lit ? 50000 : 500;
                auto at = (static_cast<std::size_t>(y) * size + x) * 6 + channel * 2;
                p[at] = v & 255;
                p[at + 1] = v >> 8;
            }
    return p;
}
std::string NewId()
{
    std::random_device random;
    const char *hex = "0123456789abcdef";
    std::string id;
    for (int i = 0; i < 16; i++)
    {
        unsigned b = random() & 255;
        if (i == 6)
            b = (b & 15) | 64;
        if (i == 8)
            b = (b & 63) | 128;
        if (i == 4 || i == 6 || i == 8 || i == 10)
            id += '-';
        id += hex[b >> 4];
        id += hex[b & 15];
    }
    return id;
}
int main(int argc, char **argv)
{
    try
    {
        if (argc >= 2 && std::string(argv[1]) == "--self-test")
        {
            auto data = Synthetic(400);
            Image image{data.data(), data.size(), 2400, 400, 400, 16, 3};
            auto run = [&](Rectangle roi = {}) {
                return Measure(image, roi, {}, "11111111-1111-4111-8111-111111111111", "synthetic");
            };
            auto result = run();
            Check(result["summary"]["validPointCount"] == 9, "valid count");
            for (auto &p : result["points"])
                Check(std::abs(p["separation"]["maximumEdgeSeparationPx"].get<double>() - 2) < 1e-9, "known shift");
            Check(run({50, 50, 300, 300})["points"] == result["points"], "ROI global coordinates");
            Options singleOptions; singleOptions.rows = 1; singleOptions.columns = 1;
            auto single = Measure(image, {50,50,100,100}, singleOptions, NewId(), "single");
            Check(single["points"].size() == 1 && single["summary"]["complete"] == true && single["grid"]["rows"] == 1, "single layout");
            Check(single["points"][0]["channels"] == result["points"][0]["channels"], "single ROI global coordinates");
            auto wrongLayout = Measure(image, {}, singleOptions, NewId(), "too-many");
            Check(wrongLayout["summary"]["complete"] == false && wrongLayout["points"].size() == 1, "extra crosses rejected");
            Options rectangleOptions; rectangleOptions.rows = 2; rectangleOptions.columns = 3;
            auto rectangle = Measure(image, {50,50,300,200}, rectangleOptions, NewId(), "2x3");
            Check(rectangle["points"].size() == 6 && rectangle["summary"]["complete"] == true, "rectangular layout");
            std::vector<std::uint8_t> bytes8(400 * 400 * 3), bytes32(400 * 400 * 12);
            for (std::size_t i = 0; i < bytes8.size(); i++)
            {
                unsigned value = data[2 * i] + (static_cast<unsigned>(data[2 * i + 1]) << 8);
                bytes8[i] = static_cast<std::uint8_t>(value / 257);
                float f = value / 65535.f;
                std::memcpy(bytes32.data() + 4 * i, &f, 4);
            }
            Check(Measure({bytes8.data(), bytes8.size(), 1200, 400, 400, 8, 3}, {}, {}, NewId(),
                          "8-bit")["summary"]["maximumEdgeSeparationPx"] == 2,
                  "8-bit shift");
            Check(Measure({bytes32.data(), bytes32.size(), 4800, 400, 400, 32, 3}, {}, {}, NewId(),
                          "float")["summary"]["maximumEdgeSeparationPx"] == 2,
                  "float shift");
            for (bool connected : {true, false})
            {
                data = Synthetic(400);
                for (int y = 87; y <= 91; y++)
                    for (int x = 101; x <= 103; x++)
                    {
                        unsigned value = x == 102 ? (connected ? 26000 : 500) : 39000;
                        auto at = (static_cast<std::size_t>(y) * 400 + x) * 6;
                        data[at] = value & 255; data[at + 1] = value >> 8;
                    }
                image.data = data.data();
                auto shoulders = run();
                Check((shoulders["points"][0]["status"] == "VALID") == connected, "connected shoulder versus dark gap");
                if (!connected) Check(shoulders["points"][0]["separation"].is_null(), "dark gap cannot become zero");
            }
            data = Synthetic(400, true);
            image.data = data.data();
            auto invalid = run();
            Check(invalid["points"][5]["status"] == "INVALID" && invalid["points"][5]["separation"].is_null(),
                  "missing blue channel");
            std::fill(data.begin(), data.end(), 0);
            Check(run()["summary"]["validPointCount"] == 0, "black image");
            int rejected = 0;
            try
            {
                run({-1, 0, 100, 100});
            }
            catch (const std::invalid_argument &)
            {
                rejected++;
            }
            auto bad = image;
            bad.size = 1;
            try
            {
                Measure(bad, {}, {}, "a", "b");
            }
            catch (const std::invalid_argument &)
            {
                rejected++;
            }
            Options o;
            o.minimumArmCoverage = 2;
            try
            {
                Measure(image, {}, o, "a", "b");
            }
            catch (const std::invalid_argument &)
            {
                rejected++;
            }
            Check(rejected == 3, "invalid inputs rejected");
            if (argc == 3)
            {
                std::ofstream file(argv[2]);
                file << result.dump(2);
            }
            std::cout << "PASS: 15 native checks (16-bit shift, ROI, 8-bit, float32, missing channel, black image, ROI "
                         "bounds, buffer, options, connected shoulder, dark gap, single layout, single ROI, extra crosses, rectangular layout)\n";
            return 0;
        }
        if (argc != 9 && argc != 13)
        {
            std::cerr << "Usage: rgb_cross_cli input.bgr output.json width height bits stride offset imageId [x y "
                         "width height]\n";
            return 2;
        }
        int width = std::stoi(argv[3]), height = std::stoi(argv[4]), bits = std::stoi(argv[5]);
        auto stride = std::stoull(argv[6]), offset = std::stoull(argv[7]);
        Check(width >= 32 && height >= 32 && stride <= 512ULL * 1024 * 1024 / height, "input exceeds memory limit");
        std::vector<std::uint8_t> data(stride * height);
        std::ifstream input(argv[1], std::ios::binary);
        input.seekg(offset);
        input.read(reinterpret_cast<char *>(data.data()), data.size());
        Check(static_cast<std::size_t>(input.gcount()) == data.size(), "short input");
        Rectangle roi{};
        if (argc == 13)
            roi = {std::stoi(argv[9]), std::stoi(argv[10]), std::stoi(argv[11]), std::stoi(argv[12])};
        auto start = std::chrono::steady_clock::now();
        auto result = Measure({data.data(), data.size(), stride, width, height, bits, 3}, roi, {}, NewId(), argv[8]);
        auto duration =
            std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now() - start).count();
        std::ofstream output(argv[2]);
        output << result.dump(2);
        Check(output.good(), "write failed");
        std::cout << "elapsed_ms=" << duration << " summary=" << result["summary"].dump() << "\n";
        return 0;
    }
    catch (const std::exception &e)
    {
        std::cerr << e.what() << "\n";
        return 1;
    }
}
