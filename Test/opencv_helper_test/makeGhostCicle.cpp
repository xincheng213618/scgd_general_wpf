#include "function.h"


int makeGhostCicle(int width, int height, double smallRadius, double bigRadius, double backgroundColor[], double drawColor[],  const char* filePath, int flag)
{
    
    //背景颜色
    double B0 = backgroundColor[0];
    double G0 = backgroundColor[1];
    double R0 = backgroundColor[2];


    //绘图颜色
    double B1 = drawColor[0];
    double G1 = drawColor[1];
    double R1 = drawColor[2];
    
    
    // 创建背景图像[1,3,5](@ref)
    Mat img(height, width, CV_8UC3, Scalar(B0, G0, R0));  // 三通道黑色背景

    // 计算图像中心坐标[1,2,8](@ref)
    Point center(img.cols / 2, img.rows / 2);  // (960, 960)    
    int halfField = sqrt(pow(height / 2, 2) + pow(width / 2, 2));
    int outer_radius;
    int inner_radius;
    if (flag==0)
    {
        // 定义圆环参数
        outer_radius = bigRadius;   // 外圆半径
        inner_radius = smallRadius;   // 内圆半径 
    }
    if (flag==1)
    {
        // 定义圆环参数
        outer_radius =int(bigRadius* halfField);   // 外圆半径
        inner_radius = int(smallRadius* halfField);   // 内圆半径
    }
    Scalar white(B1, G1, R1);  // BGR格式白色[3,5](@ref)
   // 绘制外圆（白色填充）[1,3](@ref)
    circle(img, center, outer_radius, white, -1, LINE_AA);
    // 绘制内圆（黑色填充形成镂空）[2,3](@ref)
    circle(img, center, inner_radius, Scalar(B0, G0, R0), -1, LINE_AA);
    // 保存为BMP格式[6,7](@ref)
    imwrite(filePath, img);
    return 0;
}