#include <opencv2/opencv.hpp>
#include <string>
#include <iostream>
#include <sstream>
#include <fstream>
#include <cmath>
#ifdef _WIN32
#include <direct.h>
#else
#include <sys/stat.h>
#endif

int makeDir(const std::string& d) {
#ifdef _WIN32
    return _mkdir(d.c_str());
#else
    return mkdir(d.c_str(), 0777);
#endif
}

inline int divCeil(int a, int b) { return (a + b - 1) / b; }

#include "CVCIEFile.hpp"
bool ReadCIEFile(const std::string& filePath, CVCIEFile& fileInfo);

int main() {

    std::string path = "D:\\新建文件夹\\DEV.Camera.Default\\Data\\2025-02-26\\20250226T174538.8601002.cvraw"; // 或 .cvcie

    CVCIEFile fileInfo;
    if (!ReadCIEFile(path, fileInfo))
    {
        std::cerr << "Failed to read file: " << path << std::endl;
        return -1;
    }

    // 得到原始 Mat 视图
    cv::Mat mat = fileInfo.toMatView();
    if (mat.empty())
    {
        std::cerr << "Mat is empty" << std::endl;
        return -1;
    }

    // 得到可显示的 8U Mat（如果是 32 位浮点会归一化到 0-255）
    cv::Mat disp = fileInfo.toDisplayMat();

    // 如果是灰度/单通道，OpenCV 直接显示
    cv::imshow("disp", disp);
    cv::waitKey(0);

    // 若需要保存到 PNG/TIFF 等
    cv::imwrite("out.png", disp);

    return 0;


    //std::string inputPath = "C:\\Users\\17917\\xwechat_files\\wxid_htzn9mxqm4gw22_1d4c\\msg\\file\\2025 - 10\\CY - 1.tif";
    //int tileSize = 512;                 // 可改 256 / 512
    //bool limitMaxZoom = false;          // 若想手动限制层数，设 true
    //int forcedMaxZoom = 6;              // 只在 limitMaxZoom = true 时生效
    //bool enablePad = false;             // 若想所有瓦片都是 tileSize×tileSize，设 true

    //cv::Mat imgOriginal = cv::imread(inputPath, cv::IMREAD_UNCHANGED);
    //if (imgOriginal.empty()) {
    //    std::cout << "Cannot load image!" << std::endl;
    //    return -1;
    //}

    //int W = imgOriginal.cols;
    //int H = imgOriginal.rows;
    //int maxDim = std::max(W, H);

    //// 使用 ceil，保证第 0 级尺寸 <= tileSize（即最小级为全局缩略）
    //int autoMaxZoom = 0;
    //if (maxDim > tileSize) {
    //    double ratio = static_cast<double>(maxDim) / tileSize;
    //    autoMaxZoom = static_cast<int>(std::ceil(std::log2(ratio)));
    //}
    //int maxZoom = limitMaxZoom ? std::min(forcedMaxZoom, autoMaxZoom) : autoMaxZoom;

    //makeDir("tiles");

    //std::ostringstream manifest;
    //manifest << "Original " << W << "x" << H << "\n";
    //manifest << "TileSize " << tileSize << "\n";
    //manifest << "MaxZoom " << maxZoom << " (0.." << maxZoom << ")\n";
    //manifest << "Format zoom: levelWidth levelHeight tilesX tilesY totalTiles scale\n";

    //for (int z = 0; z <= maxZoom; ++z) {
    //    // 最高级 (z = maxZoom) scale = 1
    //    double scale = 1.0 / std::pow(2.0, maxZoom - z);
    //    int levelW = static_cast<int>(std::round(W * scale));
    //    int levelH = static_cast<int>(std::round(H * scale));
    //    if (levelW < 1) levelW = 1;
    //    if (levelH < 1) levelH = 1;

    //    cv::Mat levelImg;
    //    if (scale == 1.0) {
    //        levelImg = imgOriginal;
    //    }
    //    else {
    //        cv::resize(imgOriginal, levelImg, cv::Size(levelW, levelH), 0, 0, cv::INTER_AREA);
    //    }

    //    int tilesX = divCeil(levelW, tileSize);
    //    int tilesY = divCeil(levelH, tileSize);

    //    std::string zoomDir = "tiles/" + std::to_string(z);
    //    makeDir(zoomDir);

    //    std::cout << "Zoom " << z
    //        << " size: " << levelW << "x" << levelH
    //        << " tiles: " << tilesX << "x" << tilesY
    //        << " (" << tilesX * tilesY << ") scale=" << scale << "\n";

    //    manifest << z << " "
    //        << levelW << " " << levelH << " "
    //        << tilesX << " " << tilesY << " "
    //        << (tilesX * tilesY) << " "
    //        << scale << "\n";

    //    for (int ty = 0; ty < tilesY; ++ty) {
    //        for (int tx = 0; tx < tilesX; ++tx) {
    //            int x0 = tx * tileSize;
    //            int y0 = ty * tileSize;
    //            int wTile = std::min(tileSize, levelW - x0);
    //            int hTile = std::min(tileSize, levelH - y0);
    //            cv::Rect roi(x0, y0, wTile, hTile);
    //            cv::Mat tile = levelImg(roi);

    //            if (enablePad && (wTile != tileSize || hTile != tileSize)) {
    //                cv::Mat padded(tileSize, tileSize, tile.type(), cv::Scalar(0, 0, 0));
    //                tile.copyTo(padded(cv::Rect(0, 0, wTile, hTile)));
    //                tile = padded;
    //            }

    //            std::string outName = zoomDir + "/" + std::to_string(tx) + "_" + std::to_string(ty) + ".jpg";
    //            cv::imwrite(outName, tile);
    //        }
    //    }
    //}

    //// 写出简单 manifest
    //{
    //    std::ofstream ofs("tiles/manifest.txt");
    //    ofs << manifest.str();
    //}

    //std::cout << "Done. Manifest saved to tiles/manifest.txt\n";
    return 0;
}