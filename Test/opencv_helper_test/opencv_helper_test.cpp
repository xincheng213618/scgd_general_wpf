// OpenCVHelper_test.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//
#include <chrono>
#include <iostream>
#include <opencv.hpp>
#include <stack>
#include <opencv2/opencv.hpp>
#include <string>
#include <iostream>
#include <filesystem>
#ifdef _WIN32
#include <direct.h>
#else
#include <sys/stat.h>
#endif

using namespace std;
int makeDir(const std::string& dir)
{
#ifdef _WIN32
    return _mkdir(dir.c_str());
#else
    return mkdir(dir.c_str(), 0777);
#endif
}

int main()
{
    string inputPath = "C:\\Users\\17917\\Desktop\\tiff.tiff";

    cv::Mat img = cv::imread(inputPath);
    if (img.empty()) {
        std::cout << "Cannot load image!" << std::endl;
        return -1;
    }

    int numTilesX = 10;
    int numTilesY = 10;

    for (int zoom = 0; zoom <= 1; ++zoom) {
        cv::Mat imgZoom;
        if (zoom == 0) {
            imgZoom = img;
        }
        else {
            cv::resize(img, imgZoom, cv::Size(img.cols / 2, img.rows / 2), 0, 0, cv::INTER_AREA);
        }

        int tileW = imgZoom.cols / numTilesX;
        int tileH = imgZoom.rows / numTilesY;

        std::string zoomDir = "tiles/" + std::to_string(zoom);
        makeDir("tiles");
        makeDir(zoomDir);

        for (int y = 0; y < numTilesY; ++y) {
            for (int x = 0; x < numTilesX; ++x) {
                int px = x * tileW;
                int py = y * tileH;
                cv::Rect roi(px, py, tileW, tileH);
                cv::Mat subImg = imgZoom(roi);
                std::string outName = zoomDir + "/" + std::to_string(x) + "_" + std::to_string(y) + ".jpg";
                cv::imwrite(outName, subImg);
            }
        }
        std::cout << "Tiles saved in " << zoomDir << std::endl;
    }
}

