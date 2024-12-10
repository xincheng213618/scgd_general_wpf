// OpenCVHelper_test.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//
#include <chrono>
#include <iostream>
#include <opencv.hpp>
#include <stack>


int NoFilterCalcCircleAverage(int nPos_x, int nPos_y, double nRadius)
{
    int nCountPoint = 0;

    if (nRadius > 0)
    {
        for (int i = int(nPos_y - nRadius); i <= int(nPos_y + nRadius); ++i)
        {
            for (int j = int(nPos_x - nRadius); j <= int(nPos_x + nRadius); ++j)
            {
                double dDistance = (i - nPos_y) * (i - nPos_y) + (j - nPos_x) * (j - nPos_x);

                if (dDistance < nRadius * nRadius)
                {
                    ++nCountPoint;
                    printf("dDistance(%f), nRadius(%f), Point: (%d, %d)\n", dDistance, nRadius, j, i);
                }
            }
        }
    }
    return nCountPoint;
}

int main()
{
    std::cout << "Average distance for radius 1: " << NoFilterCalcCircleAverage(0, 0, 1) << std::endl;
    std::cout << "Average distance for radius 2: " << NoFilterCalcCircleAverage(0, 0, 2) << std::endl;
    std::cout << "Average distance for radius 2.5: " << NoFilterCalcCircleAverage(0, 0, 2.5) << std::endl;
    std::cout << "Average distance for radius 3: " << NoFilterCalcCircleAverage(0, 0, 3) << std::endl;


    return 0;
}

