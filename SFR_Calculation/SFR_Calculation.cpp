// SFR_Calculation.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//
#include <opencv2/opencv.hpp>
#include <iostream>
#include <iostream>
#include <sfr/general.h>
#include <sfr/slanted.h>
#include <sfr/cylinder.h>
#include <opencv2/core/utils/logger.hpp>
#include <numeric>

#include <vector>
#include <cmath>
#include <fstream>




void test_slanted() {
    std::string path = R"(C:\\Users\\17917\\Documents\\MATLAB\\mm\\Example_Images\\Test_edge1.tif)";
    cv::Mat img = cv::imread(path, cv::IMREAD_GRAYSCALE);
    if (img.empty()) {
        std::cerr << "Failed to load image: " << path << std::endl;
        return;
    }	
    
    cv::Mat imgs[3];    //对单通道数组进行赋值

    cv::split(img, imgs);
    img = imgs[0];

    cv::rotate(img, img, cv::ROTATE_90_CLOCKWISE);

    // 1. 多项式边缘拟合（例如 5 次，和 sfrmat5 默认一致）
    std::vector<double> loc;
    int npol = 5;
    auto fitme = sfr::poly_edge_fit(img, npol, &loc);


    // 4. 在图像上绘制拟合的边缘线
// loc[i] 代表第 i 行的边缘列坐标（可能是浮点），用一个彩色图叠加显示
    cv::Mat colorImg;
    cv::cvtColor(img, colorImg, cv::COLOR_GRAY2BGR);

    for (int y = 0; y < static_cast<int>(loc.size()); ++y) {
        double x = loc[y];
        int xi = static_cast<int>(std::round(x));
        if (xi >= 0 && xi < colorImg.cols) {
            // 你可以画点，也可以画一条小横线，下面画单像素点
            colorImg.at<cv::Vec3b>(y, xi) = cv::Vec3b(0, 0, 255); // 红色
        }
    }

    // 也可以用 poly 系数重新采样画线（效果更平滑），例如：
    
    for (int y = 0; y < img.rows; ++y) {
        double x = sfr::polyval(std::vector<double>{static_cast<double>(y)}, fitme)[0];
        int xi = static_cast<int>(std::round(x));
        if (xi >= 0 && xi < colorImg.cols) {
            colorImg.at<cv::Vec3b>(y, xi) = cv::Vec3b(0, 255, 0); // 绿色
        }
    }
    

    // 2. 用多项式拟合结果投影 ESF
    int nbin = sfr::factor; // 4
    auto esf = sfr::esf(img, fitme, nbin);

    // 3. LSF / MTF / MTF10 与之前相同
    auto lsf = sfr::lsf(esf);
    auto mtf = sfr::mtf(lsf);
    double mtf10 = sfr::mtf10(mtf);


    std::ofstream ofs("sfr_result.csv");
    if (!ofs) {
        std::cerr << "Failed to open sfr_result.csv for writing\n";
    }
    else {
        ofs << "Freq(norm),SFR\n";   // 表头
        const int N = static_cast<int>(mtf.size());
        for (int i = 0; i < N; ++i) {
            // 这里频率用归一化的 0~1（相当于从 DC 到 Nyquist）
            double f = static_cast<double>(i) / N;
            ofs << f << "," << mtf[i] << "\n";
        }
        ofs.close();
        std::cout << "SFR table written to sfr_result.csv\n";
    }


    std::cout << "Index\tFreq(norm)\tMTF\n";
    const int N = static_cast<int>(mtf.size());
    for (int i = 0; i < N; ++i) {
        double f = static_cast<double>(i) / N;
        std::cout << i << '\t' << f << '\t' << mtf[i] << '\n';
    }
    std::cout << "=============================\n";
    std::cout << "MTF10 (norm freq, poly edge): " << mtf10 << '\n';
}

int main()
{
    cv::utils::logging::setLogLevel(cv::utils::logging::LOG_LEVEL_ERROR);
    test_slanted();
    std::cout << "Hello World!\n";
}

// 运行程序: Ctrl + F5 或调试 >“开始执行(不调试)”菜单
// 调试程序: F5 或调试 >“开始调试”菜单

// 入门使用技巧: 
//   1. 使用解决方案资源管理器窗口添加/管理文件
//   2. 使用团队资源管理器窗口连接到源代码管理
//   3. 使用输出窗口查看生成输出和其他消息
//   4. 使用错误列表窗口查看错误
//   5. 转到“项目”>“添加新项”以创建新的代码文件，或转到“项目”>“添加现有项”以将现有代码文件添加到项目
//   6. 将来，若要再次打开此项目，请转到“文件”>“打开”>“项目”并选择 .sln 文件
